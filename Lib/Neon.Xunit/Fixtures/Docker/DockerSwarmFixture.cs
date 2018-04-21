//-----------------------------------------------------------------------------
// FILE:	    DockerSwarmFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using Neon.Common;

namespace Xunit
{
    /// <summary>
    /// Used to manage local Swarm state in unit tests.
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
    /// </remarks>
    /// <threadsafety instance="false"/>
    public class DockerSwarmFixture : TestFixture
    {
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
        public DockerSwarmFixture()
        {
            if (RefCount++ == 0)
            {
                ResetSwarm();
            }
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~DockerSwarmFixture()
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
                    ResetSwarm();
                }

                Covenant.Assert(RefCount >= 0, "Reference count underflow.");
            }
        }

        /// <summary>
        /// Resets the local Docker swarm by removing all swarm state.
        /// </summary>
        private void ResetSwarm()
        {
            // We're going to accomplish this by leaving the (one node) swarm 
            // if we're running in swarm mode and then initializing the swarm.
            // Leaving the swarm removes all swarm state including, services,
            // secrets, configs, etc.

            var result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "info" });

            if (result.ExitCode != 0)
            {
                throw new Exception(result.ErrorText);
            }

            if (result.OutputText.Contains("Swarm: active"))
            {
                result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "swarm", "leave", "--force" });
                
                if (result.ExitCode != 0)
                {
                    throw new Exception(result.ErrorText);
                }
            }

            result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "swarm", "init" });
                
            if (result.ExitCode != 0)
            {
                throw new Exception(result.ErrorText);
            }

            // We also need to remove any running containers.

            var args   = new string[] { "ps", "-a", "--format", "{{.ID}}" };

            result = NeonHelper.ExecuteCaptureStreams($"docker", args);

            if (result.ExitCode == 0)
            {
                using (var reader = new StringReader(result.AllText))
                {
                    foreach (var line in reader.Lines())
                    {
                        var containerId = line.Trim();

                        if (!string.IsNullOrEmpty(containerId))
                        {
                            NeonHelper.Execute("docker", new object[] { "rm", "--force", containerId });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Starts Docker service.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="image">Specifies the service image.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker service create ...</b> command.</param>
        /// <param name="serviceArgs">Optional arguments to be passed to the service.</param>
        /// <param name="env">Optional environment variables to be passed to the service, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Initialize(Action)"/>
        /// </exception>
        public void StartService(string name, string image, string[] dockerArgs = null, string[] serviceArgs = null, string[] env = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            base.CheckWithinAction();

            lock (base.SyncRoot)
            {
                if (IsInitialized)
                {
                    return;
                }

                // Start the container.

                var extraArgs = new List<string>();

                if (!string.IsNullOrEmpty(name))
                {
                    extraArgs.Add("--name");
                    extraArgs.Add(name);
                }

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
        /// Creates a Docker secret from text.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="secretText">The secret text.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker secret create ...</b> command.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Initialize(Action)"/>
        /// </exception>
        public void CreateSecret(string name, string secretText, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(secretText != null);

            base.CheckWithinAction();

            lock (base.SyncRoot)
            {
                if (IsInitialized)
                {
                    return;
                }

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
        /// Deploys a Docker stack.
        /// </summary>
        /// <param name="name">The stack name.</param>
        /// <param name="composeText">The compose-file text.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker secret create ...</b> command.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Initialize(Action)"/>
        /// </exception>
        public void DeployStack(string name, string composeText, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(composeText));

            base.CheckWithinAction();

            lock (base.SyncRoot)
            {
                if (IsInitialized)
                {
                    return;
                }

                using (var tempFolder = new TempFolder())
                {
                    var path = Path.Combine(tempFolder.Path, "docker-compose.yaml");

                    File.WriteAllText(path, composeText);

                    var argsString = NeonHelper.NormalizeExecArgs("stack", "deploy", dockerArgs, "--compose-file", path, name);
                    var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                    if (result.ExitCode != 0)
                    {
                        throw new Exception($"Cannot deploy Docker stack [{name}]: {result.ErrorText}");
                    }
                }
            }
        }

        /// <summary>
        /// Waits for a stack task to start.
        /// </summary>
        /// <param name="serviceName">The stack service name</param>
        /// <param name="timeout">The optional timeout (defaults to 30 seconds).</param>
        /// <exception cref="TimeoutException">Thrown if the operation timed out.</exception>
        /// <remarks>
        /// <para>
        /// Unlike the <b>docker service create ...</b> command, <b>docker stack deploy ...</b>
        /// does not wait for the services to be deployed and stablize before returning.  In an ideal
        /// world, this method would parse the compose file to determine the names of the services
        /// that will be deployed and wait for them automatically, but that's probably more
        /// trouble than it's worth.
        /// </para>
        /// <para>
        /// This method may be called after <see cref="DeployStack(string, string, string[])"/>
        /// within the fixture initialization action in your test class's constructor to wait
        /// for a stack task to start.  Pass <paramref name="serviceName"/> as the <b>full service
        /// name</b> for the desired service.
        /// </para>
        /// <para>
        /// This will be formatted like <b>STACKNAME_SERVICE</b> where <b>STACKNAME</b> is name 
        /// of the stack and SERVICE is the name of the target service within the compose file.
        /// </para>
        /// </remarks>
        public void WaitForStackTask(string serviceName, TimeSpan timeout = default(TimeSpan))
        {
            if (timeout == default(TimeSpan))
            {
                timeout = TimeSpan.FromSeconds(30);
            }

            NeonHelper.WaitFor(
                () =>
                {
                    var result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "ps" });

                    Assert.Equal(0, result.ExitCode);

                    return result.OutputText.Contains(serviceName + ".");
                },
                timeout: timeout, 
                pollTime: TimeSpan.FromSeconds(0.5));
        }

        /// <summary>
        /// Creates a Docker secret from bytes.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="secretBytes">The secret bytes.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker secret create ...</b> command.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Initialize(Action)"/>
        /// </exception>
        public void CreateSecret(string name, byte[] secretBytes, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(secretBytes != null);

            base.CheckWithinAction();

            lock (base.SyncRoot)
            {
                if (IsInitialized)
                {
                    return;
                }

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
        /// Creates a Docker config from text.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="configText">The secret text.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker secret create ...</b> command.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Initialize(Action)"/>
        /// </exception>
        public void CreateConfig(string name, string configText, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(configText != null);

            base.CheckWithinAction();

            lock (base.SyncRoot)
            {
                if (IsInitialized)
                {
                    return;
                }

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
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker secret create ...</b> command.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Initialize(Action)"/>
        /// </exception>
        public void CreateConfig(string name, byte[] configBytes, string[] dockerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(configBytes != null);

            base.CheckWithinAction();

            lock (base.SyncRoot)
            {
                if (IsInitialized)
                {
                    return;
                }

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
    }
}
