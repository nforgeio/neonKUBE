//-----------------------------------------------------------------------------
// FILE:	    KubeService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;

namespace Neon.Kube.Service
{
    /// <summary>
    /// Base class for Kubernetes services that don't expose an ASP.NET endpoint.
    /// Use the derived <see cref="AspNetKubeService"/> for ASP.NET services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Basing your service implementations on the <see cref="Service"/> class will
    /// make them easier to test via integration with the <b>ServiceFixture</b> from
    /// the <b>Neon.Xunit</b> library by providing some useful abstractions over 
    /// service configuration, startup and shutdown including a <see cref="ProcessTerminator"/>
    /// to handle termination signals from Kubernetes.
    /// </para>
    /// <para>
    /// This class is pretty easy to use.  Simply derive your service class from <see cref="KubeService"/>
    /// and optionally implement the <see cref="OnRunAsync"/> method.  <see cref="OnRunAsync"/> will be
    /// called when your service is started.  This is where you'll implement your service.  Note that your 
    /// <see cref="OnRunAsync"/> method should not return until the <see cref="Terminator"/> signals a stop.
    /// </para>
    /// <note>
    /// All services must properly handle <see cref="Terminator"/> stop signals so unit tests will work. 
    /// Your <see cref="OnRunAsync"/> method must return within a brief period of time (30 seconds by default) 
    /// to avoid having your tests being forced to stop.  This is probably the trickiest implementation
    /// task.  For truly asynchronous service implementations, you should consider passing
    /// the <see cref="ProcessTerminator.CancellationToken"/> to all async methods you
    /// call and then handle any <see cref="TaskCanceledException"/> exceptions thrown by
    /// returning from <see cref="OnRunAsync"/>.
    /// </note>
    /// <note>
    /// This class uses the <b>DEV_WORKSTATION</b> environment variable to determine whether
    /// the service is running in test mode or not.  This variable will typically be defined
    /// on developer workstations as well as CI/CD machines.  This variable must never be
    /// defined for production environments.
    /// </note>
    /// <para>
    /// Services are generally configured using environment variables and/or configuration
    /// files.  In production, environment variables will actually come from the environment
    /// after having been initialized by the container image or set by Kubernetes when
    /// starting the service container.  Environment variables are retrieved by name
    /// (case sensitive).
    /// </para>
    /// <para>
    /// Configuration files work the same way.  They are either present in the service 
    /// container image or passed to the container as a secret or config file by Kubernetes. 
    /// Configuration files are specified by their path (case sensitive) as located within
    /// the running container.
    /// </para>
    /// <para>
    /// This class provides some abstractions for managing environment variables and 
    /// configuration files so that services running in production and services running
    /// in a local unit test can configure themselves using the same code for both
    /// environments. 
    /// </para>
    /// <para>
    /// Services should use the <see cref="GetEnvironmentVariable(string)"/> method to 
    /// retrieve important environment variables rather than using <see cref="Environment.GetEnvironmentVariable(string)"/>.
    /// In production, this simply returns the variable directly from the current process.
    /// For test, the environment variable will be returned from a local dictionary
    /// that was expicitly initialized by calls to <see cref="SetEnvironmentVariable(string, string)"/>.
    /// This local dictionary allows the testing of multiple services at the same
    /// time with each being presented their own environment variables.
    /// </para>
    /// <para>
    /// Configuration files work similarily.  You'll use <see cref="GetConfigFilePath(string)"/>
    /// to map a logical file path to a physical path.  The logical file path is typically
    /// specified as the path where the configuration file will be located in production.
    /// This can be any valid path with in a running production container and since we're
    /// currently Linux centric, will typically be a Linux file path like <c>/myconfig.yaml</c>
    /// or <c>/var/run/myconfig.yaml</c>.
    /// </para>
    /// <para>
    /// For production, <see cref="GetConfigFilePath(string)"/> will simply return the file
    /// path passed so that the configuration file located there will referenced.  For
    /// testing, <see cref="GetConfigFilePath(string)"/> will return the path specified by
    /// an earlier call to <see cref="SetConfigFilePath(string, string)"/> or to a
    /// temporary file initialized by previous calls to <see cref="SetConfigFile(string, string)"/>
    /// or <see cref="SetConfigFile(string, byte[])"/>.  This indirection provides a 
    /// consistent way to run services in production as well as in tests, including tests
    /// running multiple services simultaneously.
    /// </para>
    /// </remarks>
    public abstract class KubeService : IDisposable
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Holds information about configuration files.
        /// </summary>
        private sealed class FileInfo : IDisposable
        {
            /// <summary>
            /// The physical path to the configuration file.
            /// </summary>
            public string PhysicalPath { get; set; }

            /// <summary>
            /// The file data as bytes or as a string encoded as UTF-8 encode bytes.
            /// </summary>
            public byte[] Data { get; set; }

            /// <summary>
            /// Set if the physical file is temporary.
            /// </summary>
            public TempFile TempFile { get; set; }

            /// <summary>
            /// Dispose the file.
            /// </summary>
            public void Dispose()
            {
                if (TempFile != null)
                {
                    TempFile.Dispose();
                    TempFile = null;
                }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private object                          syncLock = new object();
        private bool                            isRunning;
        private bool                            isDisposed;
        private bool                            stopPending;
        private Dictionary<string, string>      environmentVariables;
        private Dictionary<string, FileInfo>    configFiles;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="description">The service description.</param>
        public KubeService(KubeServiceDescription description)
        {
            Covenant.Requires<ArgumentNullException>(description != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(description.Name));

            this.Description          = description;
            this.InProduction         = Environment.GetEnvironmentVariable("DEV_WORKSTATION") == null;
            this.Terminator           = new ProcessTerminator();
            this.environmentVariables = new Dictionary<string, string>();
            this.configFiles          = new Dictionary<string, FileInfo>();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~KubeService()
        {
            Dispose(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;

                foreach (var item in configFiles.Values)
                {
                    item.Dispose();
                }

                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> when the service is running in production,
        /// when the <b>DEV_WORKSTATION</b> environment variable is
        /// <b>not defined</b>.
        /// </summary>
        public bool InProduction { get; private set; }

        /// <summary>
        /// Returns <c>true</c> when the service is running in development
        /// or test mode, when the <b>DEV_WORKSTATION</b> environment variable is
        /// <b>defined</b>.
        /// </summary>
        public bool InDevelopment => !InProduction;

        /// <summary>
        /// Returns the service description.
        /// </summary>
        public KubeServiceDescription Description { get; private set; }

        /// <summary>
        /// Returns the service name.
        /// </summary>
        public string Name => Description?.Name;

        /// <summary>
        /// <para>
        /// For services with only a single network endpoint, this returns the base
        /// URI to be used to access the service.
        /// </para>
        /// <note>
        /// This will throw a <see cref="InvalidOperationException"/> if the service
        /// defines no endpoints or more than one endpoint.
        /// </note>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the service does not define a single endpoint or <see cref="Description"/> is not set.
        /// </exception>
        public string BaseUri
        {
            get
            {
                if (Description == null)
                {
                    throw new InvalidOperationException($"The {nameof(BaseUri)} property requires that [{nameof(Description)} be set.");
                }

                if (Description.Endpoints.Count == 1)
                {
                    return Description.Endpoints.First().Value.Uri;
                }
                else
                {
                    throw new InvalidOperationException($"The {nameof(BaseUri)} property requires that the service be defined with exactly one endpoint.");
                }
            }
        }

        /// <summary>
        /// The service logger.
        /// </summary>
        public INeonLogger Log { get; set; }

        /// <summary>
        /// Returns the service's <see cref="ProcessTerminator"/>.  This can be used
        /// to handle termination signals.
        /// </summary>
        public ProcessTerminator Terminator { get; private set; }

        /// <summary>
        /// <para>
        /// Starts the service if it's not already running.  This will call <see cref="OnRunAsync"/>,
        /// where the service will be actually be implemented.
        /// </para>
        /// <note>
        /// For production, this method will not return until the service is expicitly 
        /// stopped via a call to <see cref="Stop"/> or the <see cref="Terminator"/> 
        /// handles a stop signal.  For test environments, this method will call
        /// <see cref="OnRunAsync"/> on a new thread and returns immediately while the
        /// service continues to run in parallel.
        /// </note>
        /// <para>
        /// Service implementations must honor <see cref="Terminator"/> termination
        /// signals exiting the <see cref="OnRunAsync"/> method reasonably quickly (within
        /// 30 seconds by default) when these occur.  They can do this by passing 
        /// <see cref="ProcessTerminator.CancellationToken"/> for <c>async</c> calls
        /// and then catching the <see cref="TaskCanceledException"/> and returning
        /// from <see cref="OnRunAsync"/>.
        /// </para>
        /// <para>
        /// Another technique for synchronous code is to explicitly check the 
        /// <see cref="ProcessTerminator.CancellationToken"/> token's  
        /// <see cref="CancellationToken.IsCancellationRequested"/> property and 
        /// return from your <see cref="OnRunAsync"/> method when this is <c>true</c>.
        /// This You'll need to perform this check frequently so you may need
        /// to use timeouts to prevent blocking code from blocking for too long.
        /// </para>
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// It is not possible to restart a service after it's been stopped.
        /// </note>
        /// </remarks>
        public async virtual Task RunAsync()
        {
            lock (syncLock)
            {
                if (isRunning)
                {
                    throw new InvalidOperationException($"Service [{Name}] is already running.");
                }

                if (isDisposed)
                {
                    throw new InvalidOperationException($"Service [{Name}] cannot be restarted after it's been stopped.");
                }

                isRunning = true;
            }

            // This call actually implements the service.

            try
            {
                await OnRunAsync();

                ExitCode = 0;
            }
            catch (ProgramExitException e)
            {
                ExitCode = e.ExitCode;
            }
        }

        /// <summary>
        /// Stops the service if it's not already stopped.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if another stop request is already pending.</exception>
        /// <exception cref="TimeoutException">
        /// Thrown if the service did not exit gracefully in time before it would have 
        /// been killed (e.g. by Kubernetes or Docker).
        /// </exception>
        /// <remarks>
        /// <note>
        /// It is not possible to restart a service after it's been stopped.
        /// </note>
        /// <para>
        /// This is intended for managing unit test execution and is not intended 
        /// for use by the service to stop itself.
        /// </para>
        /// </remarks>
        public virtual void Stop()
        {
            lock (syncLock)
            {
                if (stopPending)
                {
                    throw new InvalidOperationException($"A stop request for [{Name}] is already pending.");
                }

                if (!isRunning)
                {
                    return;
                }
            }

            Terminator.Signal();
        }

        /// <summary>
        /// Returns the exit code returned by the service.
        /// </summary>
        public int ExitCode { get; private set; }

        /// <summary>
        /// Called to actually implement the service.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        protected abstract Task OnRunAsync();

        /// <summary>
        /// Sets or deletes a service environment variable.
        /// </summary>
        /// <param name="name">The variable name (case sensitive).</param>
        /// <param name="value">The variable value or <c>null</c> to remove the variable.</param>
        /// <remarks>
        /// <note>
        /// Environment variable names are to be considered to be case sensitive since
        /// this is how Linux treats them and it's very common to be deploying services
        /// to Linux.
        /// </note>
        /// </remarks>
        public void SetEnvironmentVariable(string name, string value)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            lock (syncLock)
            {
                if (value == null)
                {
                    if (environmentVariables.ContainsKey(name))
                    {
                        environmentVariables.Remove(name);
                    }
                }
                else
                {
                    environmentVariables[name] = value;
                }
            }
        }

        /// <summary>
        /// Returns the value of an environment variable.
        /// </summary>
        /// <param name="name">The environment variable name (case sensitive).</param>
        /// <returns>The variable value or <c>null</c> if the variable doesn't exist.</returns>
        public string GetEnvironmentVariable(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            lock (syncLock)
            {
                if (InProduction)
                {
                    return Environment.GetEnvironmentVariable(name);
                }

                if (environmentVariables.TryGetValue(name, out var value))
                {
                    return value;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Maps a logical configuration file path to an actual file on the
        /// local machine.  This is used for unit testing to map a file on
        /// the local workstation to the path where the service expects the
        /// find to be.
        /// </summary>
        /// <param name="logicalPath">The logical file path (typically expressed as a Linux path).</param>
        /// <param name="physicalPath">The physical path to the file on the local workstation.</param>
        /// <exception cref="FileNotFoundException">Thrown if there's no file at <paramref name="physicalPath"/>.</exception>
        public void SetConfigFilePath(string logicalPath, string physicalPath)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(logicalPath));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(physicalPath));

            if (!File.Exists(physicalPath))
            {
                throw new FileNotFoundException($"Physical configuration file [{physicalPath}] does not exist.");
            }

            lock (syncLock)
            {
                if (configFiles.TryGetValue(logicalPath, out var fileInfo))
                {
                    fileInfo.Dispose();
                }

                configFiles[logicalPath] = new FileInfo() { PhysicalPath = physicalPath };
            }
        }

        /// <summary>
        /// Maps a logical configuration file path to a temporary file holding the
        /// string contents passed encoded as UTF-8.  This is typically used for
        /// initializing confguration files for unit testing.
        /// </summary>
        /// <param name="logicalPath">The logical file path (typically expressed as a Linux path).</param>
        /// <param name="contents">The content string.</param>
        public void SetConfigFile(string logicalPath, string contents)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(logicalPath));
            Covenant.Requires<ArgumentNullException>(contents != null);

            lock (syncLock)
            {
                if (configFiles.TryGetValue(logicalPath, out var fileInfo))
                {
                    fileInfo.Dispose();
                }

                var tempFile = new TempFile();

                File.WriteAllText(tempFile.Path, contents);

                configFiles[logicalPath] = new FileInfo()
                {
                    PhysicalPath = tempFile.Path,
                    TempFile     = tempFile
                };
            }
        }

        /// <summary>
        /// Maps a logical configuration file path to a temporary file holding the
        /// byte contents passed.  This is typically used initializing confguration
        /// files for unit testing.
        /// </summary>
        /// <param name="logicalPath">The logical file path (typically expressed as a Linux path).</param>
        /// <param name="contents">The contebnt bytes.</param>
        public void SetConfigFile(string logicalPath, byte[] contents)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(logicalPath));
            Covenant.Requires<ArgumentNullException>(contents != null);

            lock (syncLock)
            {
                if (configFiles.TryGetValue(logicalPath, out var fileInfo))
                {
                    fileInfo.Dispose();
                }

                var tempFile = new TempFile();

                File.WriteAllBytes(tempFile.Path, contents);

                configFiles[logicalPath] = new FileInfo()
                {
                    PhysicalPath = tempFile.Path,
                    TempFile     = tempFile
                };
            }
        }

        /// <summary>
        /// Returns the physical path for the confguration file whose logical path
        /// is specified.
        /// </summary>
        /// <param name="logicalPath">The logical file path (typically expressed as a Linux path).</param>
        /// <returns>The physical path for the configuration file.</returns>
        /// <exception cref="FileNotFoundException">Thrown if there's no file configured at <paramref name="logicalPath"/>.</exception>
        public string GetConfigFilePath(string logicalPath)
        {
            lock (syncLock)
            {
                if (InProduction)
                {
                    return logicalPath;
                }

                if (!configFiles.TryGetValue(logicalPath, out var fileInfo))
                {
                    throw new FileNotFoundException($"Configuration file at logical path [{logicalPath}] not found.");
                }

                return fileInfo.PhysicalPath;
            }
        }
    }
}
