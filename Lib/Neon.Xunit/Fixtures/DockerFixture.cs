//-----------------------------------------------------------------------------
// FILE:	    DockerFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.RepresentationModel;
using Xunit;

using Neon.Common;
using Neon.Docker;
using Neon.IO;

namespace Neon.Xunit
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
    /// file or managing a Docker Swarm or cluster.
    /// </para>
    /// <para>
    /// You should explicitly disable parallel execution in all test assemblies that
    /// rely on test fixtures by adding a C# file called <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
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
    /// <note>
    /// The fixture <see cref="Reset"/> method does not purge images from the target
    /// test node for performance reasons.  This can be a problem if you're testing
    /// container and you need to ensure that the latest image is downloaded from
    /// the registry first.  You can call <see cref="ClearImages"/> to accomplish
    /// this or <see cref="PullImage(String)"/> to pull a specific image from the registry.
    /// </note>
    /// <para>
    /// This fixture is pretty easy to use.  Simply have your test class inherit
    /// from <see cref="IClassFixture{DockerFixture}"/> and add a public constructor
    /// that accepts a <see cref="DockerFixture"/> as the only argument.  Then
    /// you can call it's <see cref="TestFixture.Start(Action)"/> method
    /// within the constructor and optionally have your custom <see cref="Action"/>
    /// use the fixture to initialize swarm services, networks, secrets, etc.
    /// </para>
    /// <para>
    /// This fixture provides several methods for managing the cluster state.
    /// These may be called within the test class constructor's action method,
    /// within the test constructor but outside of tha action, or within
    /// the test methods:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>Local Machine DNS</b></term>
    ///     <description>
    ///     <see cref="LocalMachineHosts"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Docker</b></term>
    ///     <description>
    ///     <see cref="DockerExecute(string)"/><br/>
    ///     <see cref="DockerExecute(object[])"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Configs</b></term>
    ///     <description>
    ///     <see cref="ClearConfigs(bool)"/><br/>
    ///     <see cref="CreateConfig(string, byte[], string[])"/><br/>
    ///     <see cref="CreateConfig(string, string, string[])"/><br/>
    ///     <see cref="ListConfigs(bool)"/><br/>
    ///     <see cref="RemoveConfig(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Containers</b></term>
    ///     <description>
    ///     <see cref="ClearContainers(bool)"/><br/>
    ///     <see cref="ListContainers(bool)"/><br/>
    ///     <see cref="RemoveContainer(string)"/>
    ///     <see cref="RunContainer(string, string, string[], string[], string[])"/><br/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Images</b></term>
    ///     <description>
    ///     <see cref="ClearImages()"/><br/>
    ///     <see cref="PullImage(string)"/><br/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Networks</b></term>
    ///     <description>
    ///     <see cref="ClearNetworks(bool)"/><br/>
    ///     <see cref="CreateNetwork(string, string[])"/><br/>
    ///     <see cref="ListNetworks(bool)"/><br/>
    ///     <see cref="RemoveNetwork(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Secrets</b></term>
    ///     <description>
    ///     <see cref="ClearSecrets(bool)"/><br/>
    ///     <see cref="CreateSecret(string, byte[], string[])"/><br/>
    ///     <see cref="CreateSecret(string, string, string[])"/><br/>
    ///     <see cref="ListSecrets(bool)"/><br/>
    ///     <see cref="RemoveSecret(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Services</b></term>
    ///     <description>
    ///     <see cref="ClearServices(bool)"/><br/>
    ///     <see cref="CreateService(string, string, string[], string[], string[])"/><br/>
    ///     <see cref="ListServices(bool)"/><br/>
    ///     <see cref="InspectService(string, bool)"/><br/>
    ///     <see cref="RemoveService(string)"/><br/>
    ///     <see cref="RestartService(string)"/><br/>
    ///     <see cref="RollbackService(String)"/><br/>
    ///     <see cref="UpdateService(string, string[])"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Stacks</b></term>
    ///     <description>
    ///     <see cref="ClearStacks(bool)"/><br/>
    ///     <see cref="DeployStack(string, string, string[], TimeSpan, TimeSpan)"/><br/>
    ///     <see cref="ListStacks(bool)"/><br/>
    ///     <see cref="RemoveStack(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Volumes</b></term>
    ///     <description>
    ///     <see cref="ClearVolumes(bool)"/>
    ///     </description>
    /// </item>
    /// </list>
    /// <note>
    /// <see cref="DockerFixture"/> derives from <see cref="ComposedFixture"/> so you can
    /// use <see cref="ComposedFixture.AddFixture{TFixture}(string, TFixture, Action{TFixture})"/>
    /// to add additional fixtures within your custom initialization action for advanced scenarios.
    /// </note>
    /// <para>
    /// There are two basic patterns for using this fixture.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>initialize once</b></term>
    ///     <description>
    ///     <para>
    ///     The basic idea here is to have your test class initialize the swarm
    ///     once within the test class constructor inside of the initialize action
    ///     with common state and services that all of the tests can access.
    ///     </para>
    ///     <para>
    ///     This will be quite a bit faster than reconfiguring the swarm at the
    ///     beginning of every test and can work well for many situations but it
    ///     assumes that your test methods guarantee that running any test in 
    ///     any order will not impact the results of subsequent tests.  A good 
    ///     example of this is a series of read-only tests against a service
    ///     or database.
    ///     </para>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>initialize every test</b></term>
    ///     <description>
    ///     For common scenarios where the swarm must be reset before every test,
    ///     you can call <see cref="Reset()"/> within the test class constructor
    ///     (but outside of the custom initialization <see cref="Action"/> to
    ///     reset the swarm state before the next test method is invoked.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class DockerFixture : ComposedFixture
    {
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Holds information about a Docker secret.
        /// </summary>
        public class SecretInfo
        {
            /// <summary>
            /// Returns the secret ID.
            /// </summary>
            public string ID { get; set; }

            /// <summary>
            /// Returns the secret name.
            /// </summary>
            public string Name { get; set; }
        }

        /// <summary>
        /// Holds information about a Docker network.
        /// </summary>
        public class NetworkInfo
        {
            /// <summary>
            /// Returns the network ID.
            /// </summary>
            public string ID { get; set; }

            /// <summary>
            /// Returns the network name.
            /// </summary>
            public string Name { get; set; }
        }

        /// <summary>
        /// Holds information about a Docker config.
        /// </summary>
        public class ConfigInfo
        {
            /// <summary>
            /// Returns the config ID.
            /// </summary>
            public string ID { get; set; }

            /// <summary>
            /// Returns the config name.
            /// </summary>
            public string Name { get; set; }
        }

        /// <summary>
        /// Holds information about a Docker service.
        /// </summary>
        public class ServiceInfo
        {
            /// <summary>
            /// Returns the service ID.
            /// </summary>
            public string ID { get; set; }

            /// <summary>
            /// Returns the service name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Returns the number of replicas desired.
            /// </summary>
            public int ReplicasDesired { get; set; }

            /// <summary>
            /// Returns the number of replicas actually deployed.
            /// </summary>
            public int ReplicasDeployed { get; set; }
        }

        /// <summary>
        /// Holds information about a Docker stack.
        /// </summary>
        public class StackInfo
        {
            /// <summary>
            /// Returns the stack name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Returns the number of services deployed by the stack.
            /// </summary>
            public int ServiceCount { get; set; }
        }

        /// <summary>
        /// Holds information about a Docker container.
        /// </summary>
        public class ContainerInfo
        {
            /// <summary>
            /// Returns the container ID.
            /// </summary>
            public string ID { get; set; }

            /// <summary>
            /// Returns the container name.
            /// </summary>
            public string Name { get; set; }
        }

        /// <summary>
        /// Describes a Docker stack service.
        /// </summary>
        public class StackService
        {
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
        public class StackDefinition
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
                //          image: nkubeio/test
                //          command: sleep 100000
                //          deploy:
                //            replicas: 1
                //
                //        service2:
                //          image: nkubeio/test
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

            /// <summary>
            /// Returns the service name Docker will assign to a stack service.
            /// </summary>
            /// <param name="service"></param>
            /// <returns>The service name.</returns>
            public string GetServiceName(StackService service)
            {
                Covenant.Requires<ArgumentException>(Services.Contains(service), nameof(service));

                return $"{Name}_{service.Name}";
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Used to track how many fixture instances for the current test run
        /// remain so we can determine when reset the Docker Swarm.
        /// </summary>
        private static int RefCount = 0;

        /// <summary>
        /// Identifies the built-in Docker networks.  These networks will not
        /// be returned by <see cref="ListNetworks"/> and cannot be deleted.
        /// </summary>
        protected static HashSet<string> DockerNetworks = 
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                "bridge",
                "docker_gwbridge",
                "host",
                "ingress",
                "none"
            };

        /// <summary>
        /// Called by <see cref="TestFixture"/> to ensure that Docker is
        /// reset after an interrupted test run.
        /// </summary>
        public static void EnsureReset()
        {
            if (RefCount > 0)
            {
                new DockerFixture(Stub.Param).Reset();
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private bool ignoreDispose;

        /// <summary>
        /// Special private constructor that doesn't do reference counting
        /// or automatically reset the fixture state.
        /// </summary>
        /// <param name="param">Not used.</param>
        private DockerFixture(Stub.Value param)
        {
            ignoreDispose = true;
        }

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public DockerFixture()
        {
            this.LocalMachineHosts = new HostsFixture();

            if (RefCount++ == 0)
            {
                Reset();
            }
        }

        /// <summary>
        /// Used for derived classes that need to disable the <see cref="Reset()"/>
        /// call on construction
        /// </summary>
        /// <param name="reset">Optionally calls <see cref="Reset()"/> when the reference count is zero.</param>
        protected DockerFixture(bool reset = false)
        {
            this.LocalMachineHosts = new HostsFixture();
            
            if (RefCount++ == 0)
            {
                if (reset)
                {
                    Reset();
                }
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
            if (disposing)
            {
                if (!base.IsDisposed)
                {
                    GC.SuppressFinalize(this);

                    if (ignoreDispose)
                    {
                        return;
                    }

                    if (--RefCount <= 0)
                    {
                        Reset();
                    }

                    Covenant.Assert(RefCount >= 0, "Reference count underflow.");
                }
            }
        }

        //---------------------------------------------------------------------
        // Local Hosts

        /// <summary>
        /// Returns an integrated <see cref="HostsFixture"/> that can be used to manage
        /// DNS entries in the local machine's DNS <b>hosts</b> file.
        /// </summary>
        public HostsFixture LocalMachineHosts { get; private set; }

        //---------------------------------------------------------------------
        // Docker commands

        /// <summary>
        /// Some Docker clear operations appear to take a few moments to complete.
        /// This delay will be added afterwards in an attempt to address this.
        /// </summary>
        public static TimeSpan ClearDelay { get; private set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Executes an arbitrary <b>docker</b> CLI command passing unformatted
        /// arguments and returns the results.
        /// </summary>
        /// <param name="args">The <b>docker</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResponse"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method formats any arguments passed so they will be suitable 
        /// for passing on the command line by quoting and escaping them
        /// as necessary.
        /// </para>
        /// <note>
        /// This method is defined as <c>virtual</c> so that derived classes
        /// can modify how Docker is called.  For example, the <c>HiveFixture</c>
        /// class implemented in another assembly will override this to run
        /// the <b>docker</b> within a cluster using <b>neon-cli</b>.
        /// </note>
        /// </remarks>
        public virtual ExecuteResponse DockerExecute(params object[] args)
        {
            base.CheckDisposed();

            return NeonHelper.ExecuteCapture("docker", args);
        }

        /// <summary>
        /// Executes an arbitrary <b>docker</b> CLI command passing a pre-formatted
        /// argument string and returns the results.
        /// </summary>
        /// <param name="argString">The <b>docker</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResponse"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method assumes that the single string argument passed is already
        /// formatted as required to pass on the command line.
        /// </para>
        /// <note>
        /// This method is defined as <c>virtual</c> so that derived classes
        /// can modify how Docker is called.  For example, the <c>HiveFixture</c>
        /// class implemented in another assembly will override this to run
        /// the <b>docker</b> within a cluster using <b>neon-cli</b>.
        /// </note>
        /// </remarks>
        public virtual ExecuteResponse DockerExecute(string argString)
        {
            base.CheckDisposed();

            return NeonHelper.ExecuteCapture("docker", argString);
        }

        /// <summary>
        /// Resets the local Docker daemon by clearing all swarm services and state
        /// as well as removing all containers.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method does not reset the Docker images on the test node for
        /// performance reasons.  You can call <see cref="ClearImages"/> from
        /// your tests if required.
        /// </note>
        /// <note>
        /// As a safety measure, this method ensures that the local Docker instance
        /// <b>IS NOT</b> a member of a multi-node swarm to avoid wiping out production
        /// clusters by accident.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the local Docker instance is a member of a multi-node swarm.</exception>
        public override void Reset()
        {
            // Reset any local host changes.

            LocalMachineHosts?.Reset();

            // We're going to accomplish this by leaving the (one node) swarm 
            // if we're running in swarm mode and then initializing the swarm.
            // Leaving the swarm removes all swarm state including, services,
            // secrets, configs, etc.
            //
            // We're also going to list and remove all networks.
            
            // $todo(jefflill):
            //
            // Consider using [docker system prune --force] as an option to
            // purge cached images, etc.  I'm not sure that this is a great
            // idea because it will cause images to be downloaded for every
            // test class run and will also purge the image build cache.

            base.CheckDisposed();

            var result = DockerExecute(new object[] { "info" });

            if (result.ExitCode != 0)
            {
                throw new Exception(result.AllText);
            }

            if (result.OutputText.Contains("Swarm: active"))
            {
                // Ensure that this is a single node swarm.

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
                    throw new InvalidOperationException("Cannot reset the swarm because it has more than one node.  Testing on multi-node clusters is not allowed as a safety measure to avoid accidentially wiping out a production cluster.");
                }

                // $hack(jefflill):
                //
                // I've seen situations where leaving the swarm below when one or more stacks with networks
                // are still running, effectively orphans these networks.  The networks can be listed but
                // cannot be deleted or purged.  This can only be corrected by resetting the local Docker
                // instance running on the unit testing machine.
                //
                // I'm going to work around this by listing and explicitly removing all stacks before
                // leaving swarm mode.

                result = DockerExecute("stack", "ls", "--format", "{{.ID}}");

                var stackIDs = new List<string>();

                using (var reader = new StringReader(result.OutputText))
                {
                    foreach (var line in reader.Lines(ignoreBlank: true))
                    {
                        stackIDs.Add(line);
                    }
                }

                if (stackIDs.Count > 0)
                {
                    result = DockerExecute("stack", "rm", stackIDs);

                    if (result.ExitCode != 0)
                    {
                        throw new Exception(result.AllText);
                    }
                }

                // Leave the swarm, effectively reseting all swarm state.

                result = DockerExecute(new object[] { "swarm", "leave", "--force" });

                if (result.ExitCode != 0)
                {
                    throw new Exception(result.AllText);
                }
            }

            // Initialize swarm mode.

            result = DockerExecute(new object[] { "swarm", "init" });

            if (result.ExitCode != 0)
            {
                throw new Exception(result.AllText);
            }

            // We also need to remove any running containers except for
            // any containers belonging to child ContainerFixtures.

            var subContainerFixtureIds = new HashSet<string>();

            foreach (ContainerFixture fixture in base.Children.Where(f => f is ContainerFixture))
            {
                subContainerFixtureIds.Add(fixture.ContainerId);
            }

            var containerIds = new List<string>();

            foreach (var container in ListContainers())
            {
                if (!subContainerFixtureIds.Contains(container.ID))
                {
                    containerIds.Add(container.ID);
                }
            }

            if (containerIds.Count > 0)
            {
                DockerExecute("rm", "--force", containerIds.ToArray());
            }

            // Finally, prune the volumes and networks.  Note that since 
            // we've already removed all services and containers, this 
            // effectively removes all of these.

            DockerExecute("volume", "prune", "--force");
            DockerExecute("network", "prune", "--force");
        }

        /// <summary>
        /// Removes all unreferenced images from the target test node.  <see cref="Reset"/>
        /// does not do this for performance reasonse but tests may use this method
        /// if necessary.
        /// </summary>
        /// <remarks>
        /// <note>
        /// <para>
        /// Using this may result in very slow test performance, especially since
        /// it will purge a local copy of <b>neon-cli</b> if present.  This means
        /// this and any other test images (like Couchbase) will need to be
        /// downloaded again after every reset.
        /// </para>
        /// <para>
        /// We highly recommend that you use <see cref="PullImage(string)"/> to
        /// ensure that the desired images are up-to-date rather than using
        /// <see cref="ClearImages"/>.
        /// </para>
        /// </note>
        /// </remarks>
        public virtual void ClearImages()
        {
            base.CheckDisposed();

            DockerExecute("image", "prune", "--all", "--force");
        }

        /// <summary>
        /// Pulls a specific image to the target test node.
        /// </summary>
        /// <param name="image">The image name.</param>
        public virtual void PullImage(string image)
        {
            base.CheckDisposed();

            DockerExecute("pull", image).EnsureSuccess();
        }

        //---------------------------------------------------------------------
        // Services

        /// <summary>
        /// Creates a Docker service.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="image">Specifies the service image.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker service create ...</b> command.</param>
        /// <param name="serviceArgs">Optional arguments to be passed to the service.</param>
        /// <param name="env">Optional environment variables to be passed to the service, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void CreateService(string name, string image, string[] dockerArgs = null, string[] serviceArgs = null, string[] env = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image), nameof(image));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            base.CheckDisposed();

            // Start the service.

            var extraArgs = new List<string>();

            extraArgs.Add("--name");
            extraArgs.Add(name);
#if TODO
            // This requires Docker experimental features.

            extraArgs.Add("--platform");
            extraArgs.Add("linux");
#endif
            if (env != null)
            {
                foreach (var variable in env)
                {
                    extraArgs.Add("--env");
                    extraArgs.Add(variable);
                }
            }

            // Add the DEV_WORKSTATION=1 environment variable if it's set on the local workstation.

            if (NeonHelper.IsDevWorkstation)
            {
                extraArgs.Add("--env");
                extraArgs.Add("DEV_WORKSTATION=1");
            }

            var argsString = NeonHelper.NormalizeExecArgs("service", "create", extraArgs.ToArray(), dockerArgs, image, serviceArgs);
            var result     = DockerExecute(argsString);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot launch service [{image}]: {result.AllText}");
            }
        }

        /// <summary>
        /// Returns information about the current swarm services.
        /// </summary>
        /// <param name="includeSystem">Optionally include core built-in cluster services whose names start with <b>neon-</b>.</param>
        /// <returns>A list of <see cref="ServiceInfo"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public List<ServiceInfo> ListServices(bool includeSystem = false)
        {
            base.CheckDisposed();

            var result = DockerExecute("service", "ls", "--format", "{{.ID}}~{{.Name}}~{{.Replicas}}");

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot list Docker services: {result.AllText}");
            }

            var services = new List<ServiceInfo>();

            using (var reader = new StringReader(result.OutputText))
            {
                foreach (var line in reader.Lines(ignoreBlank: true))
                {
                    var fields   = line.Split('~');
                    var replicas = fields[2].Split('/');

                    if (!includeSystem && fields[1].StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;   // Ignore built-in cluster services.
                    }

                    services.Add(
                        new ServiceInfo()
                        {
                            ID               = fields[0],
                            Name             = fields[1],
                            ReplicasDesired  = int.Parse(replicas[0]),
                            ReplicasDeployed = int.Parse(replicas[1])
                        });
                }
            }

            return services;
        }

        /// <summary>
        /// Inspects a service, returning details about its current state.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="strict">Optionally specify strict JSON parsing.</param>
        /// <returns>The <see cref="ServiceDetails"/>.</returns>
        public ServiceDetails InspectService(string name, bool strict = false)
        {
            var response = DockerExecute("service", "inspect", name);

            if (response.ExitCode != 0)
            {
                throw new Exception($"Cannot inspect service [{name}]: {response.AllText}");
            }

            // The inspection response is actually an array with a single
            // service details element, so we'll need to extract that element
            // and then parse it.

            var jArray      = JArray.Parse(response.OutputText);
            var jsonDetails = jArray[0].ToString(Formatting.Indented);
            var details     = NeonHelper.JsonDeserialize<ServiceDetails>(jsonDetails, strict);

            details.Normalize();

            return details;
        }

        /// <summary>
        /// Removes a Docker service.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void RemoveService(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            base.CheckDisposed();

            // Remove the service.

            var argsString = NeonHelper.NormalizeExecArgs("service", "rm", name);
            var result     = DockerExecute(argsString);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot remove service [{name}]: {result.AllText}");
            }
        }

        /// <summary>
        /// Restarts a Docker service.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void RestartService(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            base.CheckDisposed();

            // Restart the service by updating it with [--force]..

            var argsString = NeonHelper.NormalizeExecArgs("service", "update", "--force", name);
            var result     = DockerExecute(argsString);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot restart service [{name}]: {result.AllText}");
            }
        }

        /// <summary>
        /// Rolls back a Docker service.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void RollbackService(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            base.CheckDisposed();

            // Rollback the service.

            var argsString = NeonHelper.NormalizeExecArgs("service", "rollback", name);
            var result     = DockerExecute(argsString);

            if (result.ExitCode != 0)
            {
                // Docker returns [exitcode=0] even when the rollback was successful.
                // Seems weird, but we'll try to examine the error text to detect
                // actual problems.

                if (!result.ErrorText.Contains("rollback completed"))
                {
                    throw new Exception($"Cannot rollback service [{name}]: {result.AllText}");
                }
            }
        }

        /// <summary>
        /// Updates a Docker service.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="dockerArgs">Arguments to be passed to the <b>docker service update ...</b> command.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void UpdateService(string name, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(dockerArgs != null, nameof(dockerArgs));

            base.CheckDisposed();

            // Update the service.

            var argsString = NeonHelper.NormalizeExecArgs("service", "update", dockerArgs, name);
            var result     = DockerExecute(argsString);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot update service [{name}]: {result.AllText}");
            }
        }

        /// <summary>
        /// Removes all deployed services.
        /// </summary>
        /// <param name="removeSystem">Optionally remove core cluster services as well.</param>
        /// <remarks>
        /// By default, this method will not remove core cluster services
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public void ClearServices(bool removeSystem = false)
        {
            // Note that we're always going to remove any [neon-secret-retriever-*] 
            // services regardless of [removeSystem].

            var names = new List<string>();

            foreach (var service in ListServices(includeSystem: true))
            {
                if (removeSystem || !service.Name.StartsWith("neon-") || service.Name.StartsWith("neon-secret-retriever-"))
                {
                    names.Add(service.Name);
                }
            }

            if (names.Count > 0)
            {
                DockerExecute("service", "rm", names.ToArray());
            }
        }

        //---------------------------------------------------------------------
        // Containers

        /// <summary>
        /// Creates a Docker container.
        /// </summary>
        /// <param name="name">The container name.</param>
        /// <param name="image">Specifies the container image.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker service create ...</b> command.</param>
        /// <param name="containerArgs">Optional arguments to be passed to the service.</param>
        /// <param name="env">Optional environment variables to be passed to the container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void RunContainer(string name, string image, string[] dockerArgs = null, string[] containerArgs = null, string[] env = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image), nameof(image));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            base.CheckDisposed();

            // Start the container.

            var extraArgs = new List<string>();

            extraArgs.Add("--name");
            extraArgs.Add(name);
#if TODO
            // This requires Docker experimental features.

            extraArgs.Add("--platform");
            extraArgs.Add("linux");
#endif
            extraArgs.Add("--detach");

            if (env != null)
            {
                foreach (var variable in env)
                {
                    extraArgs.Add("--env");
                    extraArgs.Add(variable);
                }
            }

            // Add the DEV_WORKSTATION=1 environment variable if it's set on the local workstation.

            if (NeonHelper.IsDevWorkstation)
            {
                extraArgs.Add("--env");
                extraArgs.Add("DEV_WORKSTATION=1");
            }

            var argsString = NeonHelper.NormalizeExecArgs("run", extraArgs.ToArray(), dockerArgs, image, containerArgs);
            var result     = DockerExecute(argsString);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot launch container [{image}]: {result.AllText}");
            }
        }

        /// <summary>
        /// Returns information about the current Docker containers.
        /// </summary>
        /// <param name="includeSystem">Optionally include core built-in cluster containers whose names start with <b>neon-</b>.</param>
        /// <returns>A list of <see cref="ContainerInfo"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public List<ContainerInfo> ListContainers(bool includeSystem = false)
        {
            base.CheckDisposed();

            var result = DockerExecute("ps", "--format", "{{.ID}}~{{.Names}}");

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot list Docker containers: {result.AllText}");
            }

            var containers = new List<ContainerInfo>();

            using (var reader = new StringReader(result.OutputText))
            {
                foreach (var line in reader.Lines(ignoreBlank: true))
                {
                    var fields = line.Split('~');

                    if (!includeSystem && fields[1].StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;   // Ignore built-in cluster containers.
                    }

                    containers.Add(
                        new ContainerInfo()
                        {
                            ID   = fields[0],
                            Name = fields[1]
                        });
                }
            }

            return containers;
        }

        /// <summary>
        /// Removes a Docker container.
        /// </summary>
        /// <param name="name">The container name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void RemoveContainer(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            base.CheckDisposed();

            // Remove the container.

            var extraArgs = new List<string>();

            extraArgs.Add("--name");
            extraArgs.Add(name);

            extraArgs.Add("--force");

            var argsString = NeonHelper.NormalizeExecArgs("rm", extraArgs.ToArray());
            var result     = DockerExecute(argsString);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot remove container [{name}]: {result.AllText}");
            }
        }

        /// <summary>
        /// Removes all running containers.
        /// </summary>
        /// <param name="removeSystem">Optionally remove core cluster containers as well.</param>
        /// <remarks>
        /// By default, this method will not remove core cluster containers
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public void ClearContainers(bool removeSystem = false)
        {
            var ids = new List<string>();

            foreach (var container in ListContainers(removeSystem))
            {
                ids.Add(container.ID);
            }

            if (ids.Count > 0)
            {
                DockerExecute("container", "rm", ids.ToArray());
            }
        }

        //---------------------------------------------------------------------
        // Stacks

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
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        /// <exception cref="TimeoutException">Thrown if the stack tasks were not deployed after waiting <paramref name="timeout"/>.</exception>
        public void DeployStack(string name, string composeYaml, string[] dockerArgs = null, TimeSpan timeout = default, TimeSpan convergeTime = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(composeYaml), nameof(composeYaml));

            if (timeout == default(TimeSpan))
            {
                timeout = TimeSpan.FromMinutes(5);
            }

            if (convergeTime == default(TimeSpan))
            {
                convergeTime = TimeSpan.FromSeconds(5);
            }

            var stackDefinition = new StackDefinition(name, composeYaml);

            base.CheckDisposed();

            using (var tempFolder = new TempFolder())
            {
                var path = Path.Combine(tempFolder.Path, "docker-compose.yaml");

                File.WriteAllText(path, composeYaml);

                var argsString = NeonHelper.NormalizeExecArgs("stack", "deploy", dockerArgs, "--compose-file", path, name);
                var result     = DockerExecute(argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot deploy Docker stack [{name}]: {result.AllText}");
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
                var services = ListServices(includeSystem: true);

                foreach (var stackService in stackDefinition.Services)
                {
                    var serviceName = stackDefinition.GetServiceName(stackService);
                    var service     = services.SingleOrDefault(s => s.Name.Equals(serviceName, StringComparison.InvariantCultureIgnoreCase));

                    if (service != null && service.ReplicasDesired < service.ReplicasDeployed)
                    {
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

        /// <summary>
        /// Returns information about the current swarm stacks.
        /// </summary>
        /// <param name="includeSystem">Optionally include core built-in cluster stacks whose names start with <b>neon-</b>.</param>
        /// <returns>A list of <see cref="StackInfo"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public List<StackInfo> ListStacks(bool includeSystem = false)
        {
            base.CheckDisposed();

            var result = DockerExecute("stack", "ls", "--format", "{{.Name}}~{{.Services}}");

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot list Docker stacks: {result.AllText}");
            }

            var stacks = new List<StackInfo>();

            using (var reader = new StringReader(result.OutputText))
            {
                foreach (var line in reader.Lines(ignoreBlank: true))
                {
                    var fields = line.Split('~');

                    if (!includeSystem && fields[1].StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;   // Ignore built-in cluster stacks.
                    }

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

        /// <summary>
        /// Removes a Docker stack.
        /// </summary>
        /// <param name="name">The stack name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void RemoveStack(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            base.CheckDisposed();

            // Remove the service.

            var extraArgs = new List<string>();

            extraArgs.Add(name);

            var argsString = NeonHelper.NormalizeExecArgs("stack", "rm", extraArgs.ToArray());
            var result     = DockerExecute(argsString);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot remove stack [{name}]: {result.AllText}");
            }
        }

        /// <summary>
        /// Removes all deployed stacks.
        /// </summary>
        /// <param name="removeSystem">Optionally remove core cluster stacks as well.</param>
        /// <remarks>
        /// By default, this method will not remove core cluster stacks
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public void ClearStacks(bool removeSystem = false)
        {
            var names = new List<string>();

            foreach (var stack in ListStacks(removeSystem))
            {
                names.Add(stack.Name);
            }

            if (names.Count > 0)
            {
                DockerExecute("stack", "rm", names.ToArray());
            }
        }

        //---------------------------------------------------------------------
        // Secrets

        /// <summary>
        /// Creates a Docker secret from text.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="secretText">The secret text.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker secret create ...</b> command.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void CreateSecret(string name, string secretText, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(secretText != null, nameof(secretText));

            base.CheckDisposed();

            using (var tempFolder = new TempFolder())
            {
                var path = Path.Combine(tempFolder.Path, "secret.txt");

                File.WriteAllText(path, secretText);

                var argsString = NeonHelper.NormalizeExecArgs("secret", "create", dockerArgs, name, path);
                var result     = DockerExecute(argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot create Docker secret [{name}]: {result.AllText}");
                }
            }
        }

        /// <summary>
        /// Creates a Docker secret from bytes.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="secretBytes">The secret bytes.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker secret create ...</b> command.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void CreateSecret(string name, byte[] secretBytes, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(secretBytes != null, nameof(secretBytes));

            base.CheckDisposed();

            using (var tempFolder = new TempFolder())
            {
                var path = Path.Combine(tempFolder.Path, "secret.dat");

                File.WriteAllBytes(path, secretBytes);

                var argsString = NeonHelper.NormalizeExecArgs("secret", "create", dockerArgs, name, path);
                var result     = DockerExecute(argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot create Docker secret [{name}]: {result.AllText}");
                }
            }
        }

        /// <summary>
        /// Returns information about the current swarm secrets.
        /// </summary>
        /// <param name="includeSystem">Optionally include core built-in cluster secrets whose names start with <b>neon-</b>.</param>
        /// <returns>A list of <see cref="SecretInfo"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public List<SecretInfo> ListSecrets(bool includeSystem = false)
        {
            base.CheckDisposed();

            var result = DockerExecute("secret", "ls", "--format", "{{.ID}}~{{.Name}}");

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot list Docker secrets: {result.AllText}");
            }

            var secrets = new List<SecretInfo>();

            using (var reader = new StringReader(result.OutputText))
            {
                foreach (var line in reader.Lines(ignoreBlank: true))
                {
                    var fields = line.Split('~');

                    if (!includeSystem && fields[1].StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;   // Ignore built-in cluster secrets.
                    }

                    secrets.Add(
                        new SecretInfo()
                        {
                            ID   = fields[0],
                            Name = fields[1]
                        });
                }
            }

            return secrets;
        }

        /// <summary>
        /// Removes a Docker secret.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void RemoveSecret(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            base.CheckDisposed();

            // Remove the secret.

            var argsString = NeonHelper.NormalizeExecArgs("secret", "rm", name);
            var result     = DockerExecute(argsString);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot remove secret [{name}]: {result.AllText}");
            }
        }

        /// <summary>
        /// Removes all swarm secrets.
        /// </summary>
        /// <param name="removeSystem">Optionally remove core cluster secrets as well.</param>
        /// <remarks>
        /// By default, this method will not remove cluster cluster secrets
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public void ClearSecrets(bool removeSystem = false)
        {
            var names = new List<string>();

            foreach (var secret in ListSecrets(removeSystem))
            {
                names.Add(secret.Name);
            }

            if (names.Count > 0)
            {
                DockerExecute("secret", "rm", names.ToArray());
            }
        }

        //---------------------------------------------------------------------
        // Configs

        /// <summary>
        /// Creates a Docker config from text.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="configText">The secret text.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker config create ...</b> command.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void CreateConfig(string name, string configText, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(configText != null, nameof(configText));

            base.CheckDisposed();

            using (var tempFolder = new TempFolder())
            {
                var path = Path.Combine(tempFolder.Path, "config.txt");

                File.WriteAllText(path, configText);

                var argsString = NeonHelper.NormalizeExecArgs("config", "create", dockerArgs, name, path);
                var result     = DockerExecute(argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot create Docker config [{name}]: {result.AllText}");
                }
            }
        }

        /// <summary>
        /// Creates a Docker config from bytes.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="configBytes">The secret bytes.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker config create ...</b> command.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void CreateConfig(string name, byte[] configBytes, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(configBytes != null, nameof(configBytes));

            base.CheckDisposed();

            using (var tempFolder = new TempFolder())
            {
                var path = Path.Combine(tempFolder.Path, "config.dat");

                File.WriteAllBytes(path, configBytes);

                var argsString = NeonHelper.NormalizeExecArgs("config", "create", dockerArgs, name, path);
                var result     = DockerExecute(argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot create Docker config [{name}]: {result.AllText}");
                }
            }
        }

        /// <summary>
        /// Returns information about the current swarm configs.
        /// </summary>
        /// <param name="includeSystem">Optionally include core built-in cluster configs whose names start with <b>neon-</b>.</param>
        /// <returns>A list of <see cref="ConfigInfo"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public List<ConfigInfo> ListConfigs(bool includeSystem = false)
        {
            base.CheckDisposed();

            var result = DockerExecute("config", "ls", "--format", "{{.ID}}~{{.Name}}");

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot list Docker configs: {result.AllText}");
            }

            var configs = new List<ConfigInfo>();

            using (var reader = new StringReader(result.OutputText))
            {
                foreach (var line in reader.Lines(ignoreBlank: true))
                {
                    var fields = line.Split('~');

                    if (!includeSystem && fields[1].StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;   // Ignore built-in cluster configs.
                    }

                    configs.Add(
                        new ConfigInfo()
                        {
                            ID   = fields[0],
                            Name = fields[1]
                        });
                }
            }

            return configs;
        }

        /// <summary>
        /// Removes a Docker config.
        /// </summary>
        /// <param name="name">The config name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void RemoveConfig(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            base.CheckDisposed();

            // Remove the secret.

            var argsString = NeonHelper.NormalizeExecArgs("config", "rm", name);
            var result     = DockerExecute(argsString);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot remove config [{name}]: {result.AllText}");
            }
        }

        /// <summary>
        /// Removes all swarm configs.
        /// </summary>
        /// <param name="removeSystem">Optionally remove core cluster configs as well.</param>
        /// <remarks>
        /// By default, this method will not remove core cluster configs
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public void ClearConfigs(bool removeSystem = false)
        {
            var names = new List<string>();

            foreach (var config in ListConfigs(removeSystem))
            {
                names.Add(config.Name);
            }

            if (names.Count > 0)
            {
                DockerExecute("config", "rm", names.ToArray());
                Thread.Sleep(ClearDelay);
            }
        }

        //---------------------------------------------------------------------
        // Networks

        /// <summary>
        /// Creates a Docker network.
        /// </summary>
        /// <param name="name">The network name.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker network create ...</b> command.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        public void CreateNetwork(string name, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            base.CheckDisposed();

            var argsString = NeonHelper.NormalizeExecArgs("network", "create", "--driver", "overlay", "--opt", "encrypt", dockerArgs, name);
            var result     = DockerExecute(argsString);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot create Docker network [{name}]: {result.AllText}");
            }
        }

        /// <summary>
        /// Returns information about the current swarm networks.
        /// </summary>
        /// <param name="includeSystem">Optionally include core built-in cluster networks whose names start with <b>neon-</b>.</param>
        /// <returns>A list of <see cref="NetworkInfo"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        /// <remarks>
        /// <note>
        /// This method <b>DOES NOT</b> include built-in Docker networks such as
        /// <b>bridge</b>, <b>docker_gwbridge</b>, <b>host</b>, <b>ingress</b>,
        /// or <b>none</b> in the listed networks.
        /// </note>
        /// </remarks>
        public List<NetworkInfo> ListNetworks(bool includeSystem = false)
        {
            base.CheckDisposed();

            var result = DockerExecute("network", "ls", "--format", "{{.ID}}~{{.Name}}");

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot list Docker networks: {result.AllText}");
            }

            var networks = new List<NetworkInfo>();

            using (var reader = new StringReader(result.OutputText))
            {
                foreach (var line in reader.Lines(ignoreBlank: true))
                {
                    var fields = line.Split('~');

                    if (DockerNetworks.Contains(fields[1]))
                    {
                        continue;   // Ignore built-in Docker networks.
                    }

                    if (!includeSystem && fields[1].StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;   // Ignore built-in cluster networks.
                    }

                    networks.Add(
                        new NetworkInfo()
                        {
                            ID   = fields[0],
                            Name = fields[1]
                        });
                }
            }

            return networks;
        }

        /// <summary>
        /// Removes a Docker network.
        /// </summary>
        /// <param name="name">The network name.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown for built-in Docker networks.</exception>
        /// <remarks>
        /// <note>
        /// This method <b>DOES NOT</b> allow the removal of built-in Docker networks 
        /// such as <b>bridge</b>, <b>docker_gwbridge</b>, <b>host</b>, <b>ingress</b>,
        /// or <b>none</b> in the listed networks.
        /// </note>
        /// </remarks>
        public void RemoveNetwork(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            if (DockerNetworks.Contains(name))
            {
                throw new NotSupportedException($"Cannot remove the built-in Docker network [{name}].");
            }

            base.CheckDisposed();

            // Remove the secret.

            var argsString = NeonHelper.NormalizeExecArgs("network", "rm", name);
            var result     = DockerExecute(argsString);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot remove network [{name}]: {result.AllText}");
            }
        }

        /// <summary>
        /// Removes all swarm networks.
        /// </summary>
        /// <param name="removeSystem">Optionally remove core cluster networks as well.</param>
        /// <remarks>
        /// By default, this method will not remove core cluster networks
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public void ClearNetworks(bool removeSystem = false)
        {
            var names = new List<string>();

            foreach (var network in ListNetworks(removeSystem))
            {
                names.Add(network.Name);
            }

            if (names.Count > 0)
            {
                DockerExecute("network", "rm", names.ToArray());
            }
        }

        //---------------------------------------------------------------------
        // Volumes

        /// <summary>
        /// Removes all swarm volumes.
        /// </summary>
        /// <param name="removeSystem">Optionally remove core cluster volumes as well.</param>
        /// <remarks>
        /// By default, this method will not remove core cluster volumes
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public void ClearVolumes(bool removeSystem = false)
        {
            base.CheckDisposed();

            var result = DockerExecute("volume", "ls", "--format", "{{.Name}}");

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot list Docker volumes: {result.AllText}");
            }

            var volumes = new List<string>();

            using (var reader = new StringReader(result.OutputText))
            {
                foreach (var line in reader.Lines(ignoreBlank: true))
                {
                    if (!removeSystem && line.StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    volumes.Add(line.Trim());
                }
            }

            if (volumes.Count > 0)
            {
                result = DockerExecute("volume", "rm", volumes);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot remove Docker volumes: {result.AllText}");
                }
            }
        }
    }
}
