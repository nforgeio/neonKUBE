//-----------------------------------------------------------------------------
// FILE:	    ContainerFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

using Neon.Common;

namespace Xunit
{
    /// <summary>
    /// Used to run a Docker container on the current machine as a test 
    /// fixture while tests are being performed and then deletes the
    /// container when the fixture is disposed.
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
    public class ContainerFixture : TestFixture
    {
        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public ContainerFixture()
        {
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~ContainerFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns the running container's short ID or <c>null</c> if the container
        /// is not running.
        /// </summary>
        public string ContainerId { get; private set; }

        /// <summary>
        /// Starts the container.  This must be called only from within the 
        /// <see cref="Action"/> passed to <see cref="ITestFixture.Initialize(Action)"/>.
        /// </summary>
        /// <param name="name">Specifies the container name.</param>
        /// <param name="image">Specifies the container Docker image.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker run ...</b> command.</param>
        /// <param name="containerArgs">Optional arguments to be passed to the container.</param>
        /// <param name="env">Optional environment variables to be passed to the Couchbase container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Initialize(Action)"/>
        /// </exception>
        /// <remarks>
        /// <note>
        /// You must specify a valid container <paramref name="name"/>so that the fixure
        /// can remove any existing container with the same name before starting the new container.
        /// This is very useful during test debugging when the test might be interrupted during 
        /// debugging before ensuring that the container is stopped.
        /// </note>
        /// </remarks>
        public void RunContainer(string name, string image, string[] dockerArgs = null, string[] containerArgs = null, string[] env = null)
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

                // Handle the special case where an earlier run of this contaainer was
                // not stopped because the developer was debugging and interrupted the
                // the unit tests before the fixture was disposed or a container with
                // the same name is already running for some other reason.
                //
                // We're going to look for a existing container with the same name
                // and remove it if its ID doesn't match the current container.

                var args   = new string[] { "ps", "-a", "--filter", $"name={name}", "--format", "{{.ID}}" };
                var result = NeonHelper.ExecuteCaptureStreams($"docker", args);

                if (result.ExitCode == 0)
                {
                    var existingId = result.OutputText.Trim();

                    if (!string.IsNullOrEmpty(existingId))
                    {
                        NeonHelper.Execute("docker", new object[] { "rm", "--force", existingId });
                    }
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

                var argsString = NeonHelper.NormalizeExecArgs("run", dockerArgs, extraArgs.ToArray(), image, containerArgs);

                result = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot launch container [{image}]: {result.ErrorText}");
                }
                else
                {
                    ContainerId = result.OutputText.Trim().Substring(0, 12);
                }
            }
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected override void Dispose(bool disposing)
        {
            if (!base.IsDisposed)
            {
                if (ContainerId != null)
                {
                    try
                    {
                        var args   = new string[] { "rm", "--force", ContainerId };
                        var result = NeonHelper.ExecuteCaptureStreams($"docker", args);

                        if (result.ExitCode != 0)
                        {
                            throw new Exception($"Cannot remove container [{ContainerId}.");
                        }
                    }
                    finally
                    {
                        ContainerId = null;
                    }
                }
            }
        }
    }
}
