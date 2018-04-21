//-----------------------------------------------------------------------------
// FILE:	    DockerFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading;

using YamlDotNet.RepresentationModel;

using Neon.Common;

namespace Xunit
{
    /// <summary>
    /// An Xunit test fixture used to manage a local Docker daemon within unit tests.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Couchbase Docker container, modifying the local DNS <b>hosts</b>
    /// file or managing a Docker Swarm or neonCLUSTER.
    /// </para>
    /// <para>
    /// You should explicitly disable parallel execution in all test assemblies that
    /// rely on test fixtures by adding a C# file called <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true)]
    /// </code>
    /// </note>
    /// <para>
    /// This fixture resets the state of the local Docker daemon before and after
    /// the test runner executes the tests in a test class by removing all containers
    /// and services as well as swarm items such as secrets, configs and networks.
    /// </para>
    /// <note>
    /// This fixture works only for local Docker instances that <b>ARE NOT</b>
    /// members of a multi-node cluster as a safety measure to help avoid the
    /// possiblity of accidentially wiping out a production cluster.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public class DockerFixture : TestFixtureSet
    {
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Holds information about a Docker secret.
        /// </summary>
        public class SecretInfo
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            internal SecretInfo()
            {
            }

            /// <summary>
            /// Returns the secret ID.
            /// </summary>
            public string Id { get; internal set; }

            /// <summary>
            /// Returns the secret name.
            /// </summary>
            public string Name { get; internal set; }
        }

        /// <summary>
        /// Holds information about a Docker network.
        /// </summary>
        public class NetworkInfo
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            internal NetworkInfo()
            {
            }

            /// <summary>
            /// Returns the network ID.
            /// </summary>
            public string Id { get; internal set; }

            /// <summary>
            /// Returns the network name.
            /// </summary>
            public string Name { get; internal set; }
        }

        /// <summary>
        /// Holds information about a Docker config.
        /// </summary>
        public class ConfigInfo
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            internal ConfigInfo()
            {
            }

            /// <summary>
            /// Returns the config ID.
            /// </summary>
            public string Id { get; internal set; }

            /// <summary>
            /// Returns the config name.
            /// </summary>
            public string Name { get; internal set; }
        }

        /// <summary>
        /// Holds information about a Docker service.
        /// </summary>
        public class ServiceInfo
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            internal ServiceInfo()
            {
            }

            /// <summary>
            /// Returns the service ID.
            /// </summary>
            public string Id { get; internal set; }

            /// <summary>
            /// Returns the service name.
            /// </summary>
            public string Name { get; internal set; }
        }

        /// <summary>
        /// Holds information about a Docker stack.
        /// </summary>
        public class StackInfo
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            internal StackInfo()
            {
            }

            /// <summary>
            /// Returns the stack name.
            /// </summary>
            public string Name { get; internal set; }

            /// <summary>
            /// Returns the number of services deployed by the stack.
            /// </summary>
            public int ServiceCount { get; internal set; }
        }

        /// <summary>
        /// Holds information about a Docker container.
        /// </summary>
        public class ContainerInfo
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            internal ContainerInfo()
            {
            }

            /// <summary>
            /// Returns the container ID.
            /// </summary>
            public string Id { get; internal set; }

            /// <summary>
            /// Returns the container name.
            /// </summary>
            public string Name { get; internal set; }
        }

        /// <summary>
        /// Describes a Docker stack service.
        /// </summary>
        private class StackService
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            public StackService()
            {
            }

            /// <summary>
            /// The service name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The number of service replicas.
            /// </summary>
            public int Replicas { get; set; }
        }

        /// <summary>
        /// Parses useful information from a Docker YAML compose file.
        /// </summary>
        private class StackDefinition
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="name">The stack name.</param>
            /// <param name="composeYaml">The stack compose YAML definition.</param>
            public StackDefinition(string name, string composeYaml)
            {
                this.Name     = name;
                this.Services = new List<StackService>();

                // For now, all we need to do is to parse the service names
                // and the number of replicas to be deployed for each service.
                //
                // A simple compose file looks like:
                //
                //      version: '3'
                //
                //      services:
                //
                //        service1:
                //          image: alpine
                //          command: sleep 100000
                //          deploy:
                //            replicas: 1
                //
                //        service2:
                //          image: alpine
                //          command: sleep 100000
                //          deploy:
                //            replicas: 2

                using (var reader = new StringReader(composeYaml))
                {
                    var yaml = new YamlStream();

                    yaml.Load(reader);

                    var yamlRoot     = (YamlMappingNode)yaml.Documents.First().RootNode;
                    var yamlServices = (YamlMappingNode)yamlRoot.Children["services"];

                    foreach (var yamlService in yamlServices.Children)
                    {
                        var service = new StackService();

                        service.Name     = (string)yamlService.Key;
                        service.Replicas = 1;   // The default

                        var serviceProperties = (YamlMappingNode)yamlService.Value;

                        if (serviceProperties.Children.TryGetValue("deploy", out var yamlDeployPropertiesNode))
                        {
                            var yamlDeployProperties = (YamlMappingNode)yamlDeployPropertiesNode;

                            if (yamlDeployProperties.Children.TryGetValue("replicas", out var replicas))
                            {
                                if (replicas.NodeType == YamlNodeType.Scalar)
                                {
                                    service.Replicas = int.Parse(replicas.ToString());
                                }
                            }
                        }

                        Services.Add(service);
                    }
                }
            }

            /// <summary>
            /// Returns the stack name.
            /// </summary>
            public string Name { get; private set; }

            /// <summary>
            /// Returns information about the stack's services.
            /// </summary>
            public List<StackService> Services { get; private set; }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Used to track how many fixture instances for the current test run
        /// remain so we can determine when to ensure that all temporary DNS 
        /// records have been removed.
        /// </summary>
        private static int RefCount = 0;

        //---------------------------------------------------------------------
        // Instance members
        
        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public DockerFixture()
        {
            if (RefCount++ == 0)
            {
                Reset();
            }
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~DockerFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected override void Dispose(bool disposing)
        {
            if (!base.IsDisposed)
            {
                if (--RefCount <= 0)
                {
                    Reset();
                }

                Covenant.Assert(RefCount >= 0, "Reference count underflow.");
            }
        }

        /// <summary>
        /// Executes an arbitrary Docker command and returns the results.
        /// </summary>
        /// <param name="args">The <b>docker</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        public ExecuteResult DockerExecute(params object[] args)
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                return NeonHelper.ExecuteCaptureStreams("docker", args);
            }
        }

        /// <summary>
        /// Resets the local Docker daemon by clearing all swarm services and state
        /// as well as removing all containers.
        /// </summary>
        /// <remarks>
        /// <note>
        /// As a safety measure, this method ensures that the local Docker instance
        /// <b>IS NOT</b> a member of a multi-node swarm to avoid wiping out production
        /// clusters by accident.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        /// <exception cref="InvalidOperationException">Thrown if the local Docker instance is a member of a multi-node swarm.</exception>
        public void Reset()
        {
            // We're going to accomplish this by leaving the (one node) swarm 
            // if we're running in swarm mode and then initializing the swarm.
            // Leaving the swarm removes all swarm state including, services,
            // secrets, configs, etc.
            //
            // We're also going to list and remove all networks.
            
            // $todo(jeff.lill):
            //
            // Consider using [docker system prune --force] as an option to
            // purge cached images, etc.  I'm not sure that this is a great
            // idea because it will cause images to be downloaded for every
            // test class run and will also purge the image build cache.

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                var result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "info" });

                if (result.ExitCode != 0)
                {
                    throw new Exception(result.ErrorText);
                }

                if (result.OutputText.Contains("Swarm: active"))
                {
                    // Ensure that this is a single node cluster.

                    var isSingleNode = false;

                    using (var reader = new StringReader(result.OutputText))
                    {
                        foreach (var line in reader.Lines())
                        {
                            if (line.Trim().Equals("Nodes: 1", StringComparison.InvariantCultureIgnoreCase))
                            {
                                isSingleNode = true;
                                break;
                            }
                        }
                    }

                    if (!isSingleNode)
                    {
                        throw new InvalidOperationException("Cannot reset the cluster because it has more than one node.  Testing on multi-node clusters is not allowed as a safety measure to avoid accidentially wiping out a production cluster.");
                    }

                    // Leave the swarm, effectively reseting all swarm state.

                    result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "swarm", "leave", "--force" });

                    if (result.ExitCode != 0)
                    {
                        throw new Exception(result.ErrorText);
                    }
                }

                // Initialize swam mode.

                result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "swarm", "init" });

                if (result.ExitCode != 0)
                {
                    throw new Exception(result.ErrorText);
                }

                // We also need to remove any running containers.

                foreach (var container in ListContainers())
                {
                    DockerExecute("rm", "--force", container.Id);
                }

                // Finally, prune the networks.  Note that since we've already
                // removed all services and containers, this will effectively
                // remove all networks.

                DockerExecute("network", "prune", "--force");
            }
        }

        /// <summary>
        /// Creates a Docker service.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="image">Specifies the service image.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker service create ...</b> command.</param>
        /// <param name="serviceArgs">Optional arguments to be passed to the service.</param>
        /// <param name="env">Optional environment variables to be passed to the service, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void CreateService(string name, string image, string[] dockerArgs = null, string[] serviceArgs = null, string[] env = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                // Start the service.

                var extraArgs = new List<string>();

                extraArgs.Add("--name");
                extraArgs.Add(name);

                if (env != null)
                {
                    foreach (var variable in env)
                    {
                        extraArgs.Add("--env");
                        extraArgs.Add(variable);
                    }
                }

                var argsString = NeonHelper.NormalizeExecArgs("service", "create", extraArgs.ToArray(), dockerArgs, image, serviceArgs);
                var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot launch service [{image}]: {result.ErrorText}");
                }
            }
        }

        /// <summary>
        /// Returns information about the current swarm services.
        /// </summary>
        /// <returns>A list of <see cref="ServiceInfo"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public List<ServiceInfo> ListServices()
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                var result = DockerExecute("service", "ls", "--format", "{{.ID}}\\t{{.Name}}");

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot list Docker services: {result.ErrorText}");
                }

                var services = new List<ServiceInfo>();

                using (var reader = new StringReader(result.OutputText))
                {
                    foreach (var line in reader.Lines(ignoreBlank: true))
                    {
                        var fields = line.Split('\t');

                        services.Add(
                            new ServiceInfo()
                            {
                                Id = fields[0],
                                Name = fields[1]
                            });
                    }
                }

                return services;
            }
        }

        /// <summary>
        /// Removes a Docker service.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void RemoveService(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                // Remove the service.

                var extraArgs = new List<string>();

                extraArgs.Add("--name");
                extraArgs.Add(name);

                var argsString = NeonHelper.NormalizeExecArgs("service", "rm", extraArgs.ToArray());
                var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot remove service [{name}]: {result.ErrorText}");
                }
            }
        }

        /// <summary>
        /// Creates a Docker container.
        /// </summary>
        /// <param name="name">The container name.</param>
        /// <param name="image">Specifies the container image.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker service create ...</b> command.</param>
        /// <param name="containerArgs">Optional arguments to be passed to the service.</param>
        /// <param name="env">Optional environment variables to be passed to the container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void CreateContainer(string name, string image, string[] dockerArgs = null, string[] containerArgs = null, string[] env = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                // Start the container.

                var extraArgs = new List<string>();

                extraArgs.Add("--name");
                extraArgs.Add(name);

                extraArgs.Add("--detach");

                if (env != null)
                {
                    foreach (var variable in env)
                    {
                        extraArgs.Add("--env");
                        extraArgs.Add(variable);
                    }
                }

                var argsString = NeonHelper.NormalizeExecArgs("run", extraArgs.ToArray(), dockerArgs, image, containerArgs);
                var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot launch container [{image}]: {result.ErrorText}");
                }
            }
        }

        /// <summary>
        /// Returns information about the current Docker containers.
        /// </summary>
        /// <returns>A list of <see cref="ContainerInfo"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public List<ContainerInfo> ListContainers()
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                var result = DockerExecute("ps", "--format", "{{.ID}}\\t{{.Names}}");

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot list Docker containers: {result.ErrorText}");
                }

                var containers = new List<ContainerInfo>();

                using (var reader = new StringReader(result.OutputText))
                {
                    foreach (var line in reader.Lines(ignoreBlank: true))
                    {
                        var fields = line.Split('\t');

                        containers.Add(
                            new ContainerInfo()
                            {
                                Id   = fields[0],
                                Name = fields[1]
                            });
                    }
                }

                return containers;
            }
        }

        /// <summary>
        /// Removes a Docker container.
        /// </summary>
        /// <param name="name">The container name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void RemoveContainer(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                // Remove the container.

                var extraArgs = new List<string>();

                extraArgs.Add("--name");
                extraArgs.Add(name);

                extraArgs.Add("--force");

                var argsString = NeonHelper.NormalizeExecArgs("rm", extraArgs.ToArray());
                var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot remove container [{name}]: {result.ErrorText}");
                }
            }
        }

        /// <summary>
        /// Deploys a Docker stack.
        /// </summary>
        /// <param name="name">The stack name.</param>
        /// <param name="composeYaml">The compose-file YAML text.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker secret create ...</b> command.</param>
        /// <param name="timeout">Optionally specifies the maximum time to wait for service tasks to start (defaults to <b>5 minutes</b>).</param>
        /// <param name="convergeTime">
        /// Optionally specifies the time to wait after the service tasks 
        /// have been started for the tasks to initialize.  This defaults 
        /// to <b>5 seconds</b> which is the same time that Docker waits
        /// for Swarm services to converge.
        /// </param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        /// <exception cref="TimeoutException">Thrown if the stack tasks were not deployed after waiting <paramref name="timeout"/>.</exception>
        public void DeployStack(string name, string composeYaml, string[] dockerArgs = null, TimeSpan timeout = default(TimeSpan), TimeSpan convergeTime = default(TimeSpan))
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(composeYaml));

            if (timeout == default(TimeSpan))
            {
                timeout = TimeSpan.FromMinutes(5);
            }

            if (convergeTime == default(TimeSpan))
            {
                convergeTime = TimeSpan.FromSeconds(5);
            }

            var stackDefinition = new StackDefinition(name, composeYaml);

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                using (var tempFolder = new TempFolder())
                {
                    var path = Path.Combine(tempFolder.Path, "docker-compose.yaml");

                    File.WriteAllText(path, composeYaml);

                    var argsString = NeonHelper.NormalizeExecArgs("stack", "deploy", dockerArgs, "--compose-file", path, name);
                    var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                    if (result.ExitCode != 0)
                    {
                        throw new Exception($"Cannot deploy Docker stack [{name}]: {result.ErrorText}");
                    }
                }

                // Docker should be starting the composed services.  We're going to wait 
                // for up to [timeout] for the service tasks to start.  They might not
                // start within this limit if there's been trouble loading the service
                // images and perhaps for other reasons.
                //
                // We'll need to parse the compose YAML to determine which services
                // will be started and how many relicas for each there should be.
                // We'll then loop until we see the correct number of tasks running.

                var stopwatch = new Stopwatch();

                while (true)
                {
                    // $hack(jeff.lill):
                    //
                    // We're going to look for task container names that start with:
                    //
                    //      STACK_SERVICE.
                    //
                    // to identify the stack's service tasks.

                    var containers = ListContainers();

                    foreach (var service in stackDefinition.Services)
                    {
                        var taskPrefix = $"{stackDefinition.Name}_{service.Name}.";

                        if (containers.Count(c => c.Name.StartsWith(taskPrefix, StringComparison.InvariantCultureIgnoreCase)) < service.Replicas)
                        {
                            // The number of containers with names matching the service
                            // task name prefix is less than required replicas so break
                            // out to continue waiting for the tasks to spin up.

                            goto notReady;
                        }
                    }

                    // All service tasks are ready if we get here.

                    break;

                    // Check for timeout.

                notReady:

                    if (stopwatch.Elapsed >= timeout)
                    {
                        throw new TimeoutException($"Stack [{name}] tasks are not running after waiting [{timeout}].");
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }

                // Wait for the services to converge.

                Thread.Sleep(convergeTime);
            }
        }

        /// <summary>
        /// Returns information about the current swarm stacks.
        /// </summary>
        /// <returns>A list of <see cref="StackInfo"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public List<StackInfo> ListStacks()
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                var result = DockerExecute("stack", "ls", "--format", "{{.Name}}\\t{{.Services}}");

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot list Docker stacks: {result.ErrorText}");
                }

                var stacks = new List<StackInfo>();

                using (var reader = new StringReader(result.OutputText))
                {
                    foreach (var line in reader.Lines(ignoreBlank: true))
                    {
                        var fields = line.Split('\t');

                        stacks.Add(
                            new StackInfo()
                            {
                                Name         = fields[0],
                                ServiceCount = int.Parse(fields[1])
                            });
                    }
                }

                return stacks;
            }
        }

        /// <summary>
        /// Removes a Docker stack.
        /// </summary>
        /// <param name="name">The stack name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void RemoveStack(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                // Remove the service.

                var extraArgs = new List<string>();

                extraArgs.Add("--name");
                extraArgs.Add(name);

                var argsString = NeonHelper.NormalizeExecArgs("stack", "rm", extraArgs.ToArray());
                var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot remove stack [{name}]: {result.ErrorText}");
                }
            }
        }

        /// <summary>
        /// Creates a Docker secret from text.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="secretText">The secret text.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker secret create ...</b> command.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void CreateSecret(string name, string secretText, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(secretText != null);

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                using (var tempFolder = new TempFolder())
                {
                    var path = Path.Combine(tempFolder.Path, "secret.txt");

                    File.WriteAllText(path, secretText);

                    var argsString = NeonHelper.NormalizeExecArgs("secret", "create", dockerArgs, name, path);
                    var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                    if (result.ExitCode != 0)
                    {
                        throw new Exception($"Cannot create Docker secret [{name}]: {result.ErrorText}");
                    }
                }
            }
        }

        /// <summary>
        /// Creates a Docker secret from bytes.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="secretBytes">The secret bytes.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker secret create ...</b> command.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void CreateSecret(string name, byte[] secretBytes, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(secretBytes != null);

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                using (var tempFolder = new TempFolder())
                {
                    var path = Path.Combine(tempFolder.Path, "secret.dat");

                    File.WriteAllBytes(path, secretBytes);

                    var argsString = NeonHelper.NormalizeExecArgs("secret", "create", dockerArgs, name, path);
                    var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                    if (result.ExitCode != 0)
                    {
                        throw new Exception($"Cannot create Docker secret [{name}]: {result.ErrorText}");
                    }
                }
            }
        }

        /// <summary>
        /// Returns information about the current swarm secrets.
        /// </summary>
        /// <returns>A list of <see cref="SecretInfo"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public List<SecretInfo> ListSecrets()
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                var result = DockerExecute("secret", "ls", "--format", "{{.ID}}\\t{{.Name}}");

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot list Docker secrets: {result.ErrorText}");
                }

                var secrets = new List<SecretInfo>();

                using (var reader = new StringReader(result.OutputText))
                {
                    foreach (var line in reader.Lines(ignoreBlank: true))
                    {
                        var fields = line.Split('\t');

                        secrets.Add(
                            new SecretInfo()
                            {
                                Id   = fields[0],
                                Name = fields[1]
                            });
                    }
                }

                return secrets;
            }
        }

        /// <summary>
        /// Removes a Docker secret.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void RemoveSecret(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                // Remove the secret.

                var argsString = NeonHelper.NormalizeExecArgs("secret", "rm", name);
                var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot remove secret [{name}]: {result.ErrorText}");
                }
            }
        }

        /// <summary>
        /// Creates a Docker config from text.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="configText">The secret text.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker config create ...</b> command.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void CreateConfig(string name, string configText, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(configText != null);

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                using (var tempFolder = new TempFolder())
                {
                    var path = Path.Combine(tempFolder.Path, "config.txt");

                    File.WriteAllText(path, configText);

                    var argsString = NeonHelper.NormalizeExecArgs("config", "create", dockerArgs, name, path);
                    var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                    if (result.ExitCode != 0)
                    {
                        throw new Exception($"Cannot create Docker config [{name}]: {result.ErrorText}");
                    }
                }
            }
        }

        /// <summary>
        /// Creates a Docker config from bytes.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="configBytes">The secret bytes.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker config create ...</b> command.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void CreateConfig(string name, byte[] configBytes, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(configBytes != null);

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                using (var tempFolder = new TempFolder())
                {
                    var path = Path.Combine(tempFolder.Path, "config.dat");

                    File.WriteAllBytes(path, configBytes);

                    var argsString = NeonHelper.NormalizeExecArgs("config", "create", dockerArgs, name, path);
                    var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                    if (result.ExitCode != 0)
                    {
                        throw new Exception($"Cannot create Docker config [{name}]: {result.ErrorText}");
                    }
                }
            }
        }

        /// <summary>
        /// Returns information about the current swarm configs.
        /// </summary>
        /// <returns>A list of <see cref="ConfigInfo"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public List<ConfigInfo> ListConfigs()
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                var result = DockerExecute("config", "ls", "--format", "{{.ID}}\\t{{.Name}}");

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot list Docker configs: {result.ErrorText}");
                }

                var configs = new List<ConfigInfo>();

                using (var reader = new StringReader(result.OutputText))
                {
                    foreach (var line in reader.Lines(ignoreBlank: true))
                    {
                        var fields = line.Split('\t');

                        configs.Add(
                            new ConfigInfo()
                            {
                                Id   = fields[0],
                                Name = fields[1]
                            });
                    }
                }

                return configs;
            }
        }

        /// <summary>
        /// Removes a Docker config.
        /// </summary>
        /// <param name="name">The config name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void RemoveConfig(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                // Remove the secret.

                var argsString = NeonHelper.NormalizeExecArgs("config", "rm", name);
                var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot remove config [{name}]: {result.ErrorText}");
                }
            }
        }

        /// <summary>
        /// Creates a Docker network.
        /// </summary>
        /// <param name="name">The network name.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker network create ...</b> command.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void CreateNetwork(string name, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                var argsString = NeonHelper.NormalizeExecArgs("network", "create", dockerArgs, name);
                var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot create Docker network [{name}]: {result.ErrorText}");
                }
            }
        }

        /// <summary>
        /// Returns information about the current swarm networks.
        /// </summary>
        /// <returns>A list of <see cref="NetworkInfo"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public List<NetworkInfo> ListNetworks()
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                var result = DockerExecute("network", "ls", "--format", "{{.ID}}\\t{{.Name}}");

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot list Docker networks: {result.ErrorText}");
                }

                var networks = new List<NetworkInfo>();

                using (var reader = new StringReader(result.OutputText))
                {
                    foreach (var line in reader.Lines(ignoreBlank: true))
                    {
                        var fields = line.Split('\t');

                        networks.Add(
                            new NetworkInfo()
                            {
                                Id   = fields[0],
                                Name = fields[1]
                            });
                    }
                }

                return networks;
            }
        }

        /// <summary>
        /// Removes a Docker network.
        /// </summary>
        /// <param name="name">The network name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public void RemoveNetwork(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                // Remove the secret.

                var argsString = NeonHelper.NormalizeExecArgs("network", "rm", name);
                var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot remove network [{name}]: {result.ErrorText}");
                }
            }
        }
    }
}
