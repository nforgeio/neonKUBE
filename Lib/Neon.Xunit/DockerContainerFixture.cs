//-----------------------------------------------------------------------------
// FILE:	    DockerContainerFixture.cs
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
    public sealed class DockerContainerFixture : IDisposable
    {
        private object  syncRoot   = new object();
        private bool    isDisposed = false;

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public DockerContainerFixture()
        {
        }

        /// <summary>
        /// Starts the container if it's not already running.
        /// </summary>
        /// <param name="image">Specifies the container Docker image.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker run ...</b> command.</param>
        /// <param name="containerArgs">Optional arguments to be passed to the container.</param>
        public void RunContainer(string image, string[] dockerArgs = null, string[] containerArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));

            lock (syncRoot)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(DockerContainerFixture));
                }

                if (ContainerId != null)
                {
                    return;     // Container is already running
                }

                var argsString = NeonHelper.NormalizeExecArgs("run", dockerArgs, image, containerArgs);
                var result     = NeonHelper.ExecuteCaptureStreams($"docker", argsString);

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

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;

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

        /// <summary>
        /// Returns the running container's short ID or <c>null</c> if the container
        /// is not running.
        /// </summary>
        public string ContainerId { get; private set; }
    }
}
