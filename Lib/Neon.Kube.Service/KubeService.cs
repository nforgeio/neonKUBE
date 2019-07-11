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
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Service;

namespace Neon.Kube.Service
{
    /// <summary>
    /// Base class for Kubernetes services that wish to use the neonKUBE unit testing conventions.
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
    /// and implement the <see cref="OnRunAsync"/> method.  <see cref="OnRunAsync"/> will be called when 
    /// your service is started.  This is where you'll implement your service.  You should perform any
    /// initialization and then call <see cref="SetRunning"/> to indicate that the service is ready for
    /// business.
    /// </para>
    /// <note>
    /// Note that calling <see cref="SetRunning()"/> after your service has initialized is very important
    /// because the <b>KubeServiceFixture</b> requires won't allow tests to proceed until the service
    /// indicates that it's ready.  This is necessary to avoid unit test race conditions.
    /// </note>
    /// <para>
    /// Note that your  <see cref="OnRunAsync"/> method should generally not return until the 
    /// <see cref="Terminator"/> signals it to stop.  Alternatively, you can throw a <see cref="ProgramExitException"/>
    /// with an optional process exit code to proactively exit your service.
    /// </para>
    /// <note>
    /// All services should properly handle <see cref="Terminator"/> stop signals so unit tests will terminate
    /// promptly.  Your terminate handler method must return within a set period of time (30 seconds by default) 
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
    /// defined for production environments.  You can use the <see cref="InProduction"/>
    /// or <see cref="InDevelopment"/> properties to check this.
    /// </note>
    /// <para><b>CONFIGURATION</b></para>
    /// <para>
    /// Services are generally configured using environment variables and/or configuration
    /// files.  In production, environment variables will actually come from the environment
    /// after having been initialized by the container image or passed by Kubernetes when
    /// starting the service container.  Environment variables are retrieved by name
    /// (case sensitive).
    /// </para>
    /// <para>
    /// Configuration files work the same way.  They are either present in the service 
    /// container image or mounted to the container as a secret or config file by Kubernetes. 
    /// Configuration files are specified by their path (case sensitive) within the
    /// running container.
    /// </para>
    /// <para>
    /// This class provides some abstractions for managing environment variables and 
    /// configuration files so that services running in production and services running
    /// in a local unit test can configure themselves using the same code for both
    /// environments. 
    /// </para>
    /// <para>
    /// Services should use the <see cref="GetEnvironmentVariable(string, string)"/> method to 
    /// retrieve important environment variables rather than using <see cref="Environment.GetEnvironmentVariable(string)"/>.
    /// In production, this simply returns the variable directly from the current process.
    /// For tests, the environment variable will be returned from a local dictionary
    /// that was expicitly initialized by calls to <see cref="SetEnvironmentVariable(string, string)"/>.
    /// This local dictionary allows the testing of multiple services at the same
    /// time with each being presented their own environment variables.
    /// </para>
    /// <para>
    /// You may also use the <see cref="LoadEnvironmentVariables(string, Func{string, string})"/>
    /// method to load environment variables from a text file (potentially encrypted via
    /// <see cref="NeonVault"/>).  This will typically be done only for unit tests.
    /// </para>
    /// <para>
    /// Configuration files work similarily.  You'll use <see cref="GetConfigFilePath(string)"/>
    /// to map a logical file path to a physical path.  The logical file path is typically
    /// specified as the path where the configuration file will be located in production.
    /// This can be any valid path with in a running production container and since we're
    /// currently Linux centric, will typically be a Linux file path like <c>/etc/MYSERVICE.yaml</c>
    /// or <c>/etc/MYSERVICE/config.yaml</c>.
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
    /// <para>
    /// You can also use <see cref="LoadConfigFile(string, string, Func{string, string})"/>
    /// during unit testing to load a potentially encrypted configuration file.
    /// </para>
    /// <para><b>SERVICE TERMINATION</b></para>
    /// <para>
    /// All services, especially those that create unmanaged resources like ASP.NET services,
    /// sockets, NATS clients, HTTP clients, thread etc. should override and implement 
    /// <see cref="Dispose(bool)"/>  to ensure that any of these resources are proactively 
    /// disposed.  Your method should call the base class version of the method first before 
    /// disposing these resources.
    /// </para>
    /// <code language="C#">
    /// protected override Dispose(bool disposing)
    /// {
    ///     base.Dispose(disposing);
    ///     
    ///     if (appHost != null)
    ///     {
    ///         appHost.Dispose();
    ///         appHost = null;
    ///     }
    /// }
    /// </code>
    /// <para>
    /// The <b>disposing</b> parameter is passed as <c>true</c> when the base <see cref="KubeService.Dispose()"/>
    /// method was called or <c>false</c> if the garbage collector is finalizing the instance
    /// before discarding it.  The difference is subtle and most services can safely ignore
    /// this parameter (other than passing it through to the base <see cref="Dispose(bool)"/>
    /// method).
    /// </para>
    /// <para>
    /// In the example above, the service implements an ASP.NET web service where <c>appHost</c>
    /// was initialized as the <c>IWebHost</c> actually implementing the web service.  The code
    /// ensures that the <c>appHost</c> isn't already disposed before disposing it.  This will
    /// stop the web service and release the underlying listening socket.  You'll want to do
    /// something like this for any other unmanaged resources your service might hold.
    /// </para>
    /// <note>
    /// <para>
    /// It's very important that you take care to dispose things like running web services and
    /// listening sockets within your <see cref="Dispose(bool)"/> method.  You also need to
    /// ensure that any threads you've created are terminated.  This means that you'll need
    /// a way to signal threads to exit and then wait for them to actually exit.
    /// </para>
    /// <para>
    /// This is important when testing your services with a unit testing framework like
    /// Xunit because frameworks like this run all tests within the same Test Runner
    /// process and leaving something like a listening socket open on a port (say port 80)
    /// may prevent a subsequent test from running successfully due to it not being able 
    /// to open its listening socket on port 80. 
    /// </para>
    /// </note>
    /// <para><b>LOGGING</b></para>
    /// <para>
    /// Each <see cref="KubeService"/> instance maintains its own <see cref="LogManager"/>
    /// instance with the a default logger created at <see cref="Log"/>.  The log manager
    /// is initialized using the <b>LOG_LEVEL</b> environment variable value which defaults
    /// to <b>info</b> when not present.  <see cref="LogLevel"/> for the possible values.
    /// </para>
    /// <para>
    /// Note that the <see cref="Neon.Diagnostics.LogManager.Default"/> log manager will
    /// also be initialized with the log level when the service is running in a production
    /// environment so that logging in production works completely as expected.
    /// </para>
    /// <para>
    /// For development environments, the <see cref="Neon.Diagnostics.LogManager.Default"/>
    /// instance's log level will not be modified.  This means that loggers created from
    /// <see cref="Neon.Diagnostics.LogManager.Default"/> may not use the same log
    /// level as the service itself.  This means that library classes that create their
    /// own loggers won't honor the service log level.  This is an unfortunate consequence
    /// of running emulated services in the same process.
    /// </para>
    /// <para>
    /// There are two ways to mitigate this.  First, any source code defined within the 
    /// service project should be designed to create loggers from the service's <see cref="LogManager"/>
    /// rather than using the global one.  Second, you can configure your unit test to
    /// set the desired log level like:
    /// </para>
    /// <code language="C#">
    /// LogManager.Default.SetLogLevel(LogLevel.Debug));
    /// </code>
    /// <note>
    /// Setting the global default log level like this will impact loggers created for all
    /// emulated services, but this shouldn't be a problem for more situations.
    /// </note>
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
        // Static members

        /// <summary>
        /// This controls whether any <see cref="KubeService"/> instances will use the global
        /// <see cref="LogManager.Default"/> log manager for logging or maintain its own
        /// log manager.  This defaults to <c>true</c> which will be appropriate for most
        /// production situations.  It may be useful to disable this for some unit tests.
        /// </summary>
        public static bool GlobalLogging = true;

        //---------------------------------------------------------------------
        // Instance members

        private object                          syncLock = new object();
        private bool                            isRunning;
        private bool                            isDisposed;
        private bool                            stopPending;
        private Dictionary<string, string>      environmentVariables;
        private Dictionary<string, FileInfo>    configFiles;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceMap">The service map describing this service and potentially other services.</param>
        /// <param name="name">The name of this service within <see cref="ServiceMap"/>.</param>
        /// <param name="branch">Optionally specifies the build branch.</param>
        /// <param name="commit">Optionally specifies the branch commit.</param>
        /// <param name="isDirty">Optionally specifies whether there are uncommit changes to the branch.</param>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if there is no service description for <paramref name="name"/>
        /// within <see cref="serviceMap"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// For those of you using Git for source control, you'll want to pass the
        /// information about the branch and latest commit you're you service was
        /// built from here.  We use the <a href="https://www.nuget.org/packages/GitInfo/">GitInfo</a>
        /// nuget package to obtain this information from the local Git repository.
        /// </para>
        /// <para>
        /// Alternatively, you could try to map properties from your source
        /// control environment to these parameters, pass your version string as 
        /// <paramref name="branch"/>, or simply ignore these parameters.
        /// </para>
        /// </remarks>
        public KubeService(
            ServiceMap  serviceMap, 
            string      name, 
            string      branch        = null, 
            string      commit        = null, 
            bool        isDirty       = false,
            bool        noProcessExit = false)
        {
            Covenant.Requires<ArgumentNullException>(serviceMap != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            if (!serviceMap.TryGetValue(name, out var description))
            {
                throw new KeyNotFoundException($"The service map does not include a service definition for [{name}].");
            }

            this.ServiceMap           = serviceMap;
            this.Description          = description;
            this.InProduction         = !NeonHelper.IsDevWorkstation;
            this.Terminator           = new ProcessTerminator();
            this.environmentVariables = new Dictionary<string, string>();
            this.configFiles          = new Dictionary<string, FileInfo>();

            // Git version info:

            this.GitVersion = null;

            if (!string.IsNullOrEmpty(branch))
            {
                this.GitVersion = branch;

                if (!string.IsNullOrEmpty(commit))
                {
                    this.GitVersion += $"-{commit}";
                }

                if (isDirty)
                {
                    this.GitVersion += $"-dirty";
                }
            }
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
            }

            Stop();

            lock(syncLock)
            {
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
        /// Returns the service name.
        /// </summary>
        public string Name => Description.Name;

        /// <summary>
        /// Returns the service map.
        /// </summary>
        public ServiceMap ServiceMap { get; private set; }

        /// <summary>
        /// Returns the service description for this service.
        /// </summary>
        public ServiceDescription Description { get; private set; }

        /// <summary>
        /// Returns GIT branch and commit the service was built from as
        /// well as an optional indication the the build branch had 
        /// uncomitted changes (e.g. was dirty).
        /// </summary>
        public string GitVersion { get; private set; }

        /// <summary>
        /// Returns the dictionary mapping case sensitive service endpoint names to endpoint information.
        /// </summary>
        public Dictionary<string, ServiceEndpoint> Endpoints => Description.Endpoints;

        /// <summary>
        /// <para>
        /// For services with exactly one network endpoint, this returns the base
        /// URI to be used to access the service.
        /// </para>
        /// <note>
        /// This will throw a <see cref="InvalidOperationException"/> if the service
        /// defines no endpoints or has multiple endpoints.
        /// </note>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the service does not define exactly one endpoint or <see cref="Description"/> is not set.
        /// </exception>
        public Uri BaseUri
        {
            get
            {
                if (Description == null)
                {
                    throw new InvalidOperationException($"The {nameof(BaseUri)} property requires that [{nameof(Description)} be set and have exactly one endpoint.");
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
        /// Returns the service's log manager.
        /// </summary>
        public ILogManager LogManager { get; private set; }

        /// <summary>
        /// Returns the service's default logger.
        /// </summary>
        public INeonLogger Log { get; private set; }

        /// <summary>
        /// Returns the service's <see cref="ProcessTerminator"/>.  This can be used
        /// to handle termination signals.
        /// </summary>
        public ProcessTerminator Terminator { get; private set; }

        /// <summary>
        /// Returns the list of command line arguments passed to the service.  This
        /// defaults to an empty list.
        /// </summary>
        public List<string> Arguments { get; private set; } = new List<string>();

        /// <summary>
        /// Returns the service current running status.
        /// </summary>
        public KubeServiceStatus Status { get; private set; }

        /// <summary>
        /// Returns the exit code returned by the service.
        /// </summary>
        public int ExitCode { get; private set; }

        /// <summary>
        /// Returns any abnormal exception thrown by the derived <see cref="OnRunAsync"/> method.
        /// </summary>
        public Exception ExitException { get; private set; }

        /// <summary>
        /// Initializes <see cref="Arguments"/> with the command line arguments passed.
        /// </summary>
        /// <param name="args">The arguments.</param>
        public void SetArguments(IEnumerable<string> args)
        {
            Covenant.Requires<ArgumentNullException>(args != null);

            Arguments.Clear();

            foreach (var arg in args)
            {
                Arguments.Add(arg);
            }
        }

        /// <summary>
        /// Called by <see cref="OnRunAsync"/> implementation after they've completed any
        /// initialization and are ready for traffic.  This sets <see cref="Status"/> to
        /// <see cref="KubeServiceStatus.Running"/>.
        /// </summary>
        public void SetRunning()
        {
            Status = KubeServiceStatus.Running;
        }

        /// <summary>
        /// Starts the service if it's not already running.  This will call <see cref="OnRunAsync"/>,
        /// which actually implements the service.
        /// </summary>
        /// <param name="disableProcessExit">
        /// Optionally specifies that the hosting process should not be terminated 
        /// when the service exists.  This is typically used for testing or debugging.
        /// This defaults to <c>false</c>.
        /// </param>
        /// <remarks>
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
        /// </remarks>
        /// <returns>The service exit code.</returns>
        /// <remarks>
        /// <note>
        /// It is not possible to restart a service after it's been stopped.
        /// </note>
        /// </remarks>
        public async virtual Task<int> RunAsync(bool disableProcessExit = false)
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

            // [disableProcessExit] will be typically passed as true when testing or
            // debugging.  We'll let the terminator know so it won't do this.

            if (disableProcessExit)
            {
                Terminator.DisableProcessExit = true;
            }

            // Initialize the logger.

            if (GlobalLogging)
            {
                LogManager = global::Neon.Diagnostics.LogManager.Default;
            }
            else
            {
                LogManager = new LogManager(parseLogLevel: false);
            }

            LogManager.SetLogLevel(GetEnvironmentVariable("LOG_LEVEL", "info"));

            Log = LogManager.GetLogger();
            Log.LogInfo(() => $"Starting [{Name}:{GitVersion}]");

            // Start and run the service.

            try
            {
                await OnRunAsync();

                ExitCode = 0;
            }
            catch (TaskCanceledException)
            {
                // Ignore these as a normal consequence of a service
                // being signalled to terminate.

                ExitCode = 0;
            }
            catch (ProgramExitException e)
            {
                // Don't override a non-zero ExitCode that was set earlier
                // with a zero exit code.

                if (e.ExitCode != 0)
                {
                    ExitCode = e.ExitCode;
                }
            }
            catch (Exception e)
            {
                ExitException = e;

                Log.LogError(e);
            }

            // Perform last rights for the service before it passes away.

            Log.LogInfo(() => $"Exiting [{Name}] with [exitcode={ExitCode}].");
            Terminator.ReadyToExit();

            Status = KubeServiceStatus.Terminated;

            return ExitCode;
        }

        /// <summary>
        /// <para>
        /// Stops the service if it's not already stopped.  This is intended to be called by
        /// external things like unit test fixtures and is not intended to be called by the
        /// service itself.  Service implementations should use <see cref="ExitCode(int, bool)"/>.
        /// </para>
        /// </summary>
        /// <exception cref="TimeoutException">
        /// Thrown if the service did not exit gracefully in time before it would have 
        /// been killed (e.g. by Kubernetes or Docker).
        /// </exception>
        /// <remarks>
        /// <note>
        /// It is not possible to restart a service after it's been stopped.
        /// </note>
        /// <para>
        /// This is intended for internal use or managing unit test execution and is not intended 
        /// for use by the service to stop itself.
        /// </para>
        /// </remarks>
        public virtual void Stop()
        {
            lock (syncLock)
            {
                if (stopPending || !isRunning)
                {
                    return;
                }

                stopPending = true;
            }

            Terminator.Signal();
        }

        /// <summary>
        /// Used by services to stop themselves, specifying an optional process exit code.
        /// </summary>
        /// <param name="exitCode">The optional exit code (defaults to <b>0</b>).</param>
        /// <remarks>
        /// This works by setting <see cref="ExitCode"/> if <paramref name="exitCode"/> is non-zero,
        /// signalling process termination on another thread and then throwing a <see cref="ProgramExitException"/> 
        /// on the current thread.  This will generally cause the current thread or task to terminate
        /// immediately and any other properly implemented threads and tasks to terminate gracefully
        /// when they receive the termination signal.
        /// </remarks>
        public virtual void Exit(int exitCode = 0)
        {
            lock (syncLock)
            {
                if (exitCode != 0)
                {
                    ExitCode = exitCode;
                }

                new Thread(
                    new ThreadStart(
                        () =>
                        {
                            // $hack(jeff.lill):
                            //
                            // Give the Exit() method a bit of time to throw the 
                            // ProgramExitException to make termination handling
                            // a bit more deterministic.

                            Thread.Sleep(TimeSpan.FromSeconds(0.5));

                            try
                            {
                                Stop();
                            }
                            catch
                            {
                                // Ignoring any errors.
                            }

                        })).Start();

                throw new ProgramExitException(ExitCode);
            }
        }

        /// <summary>
        /// Called to actually implement the service.
        /// </summary>
        /// <returns>The the progam exit code.</returns>
        /// <remarks>
        /// <para>
        /// Services should perform any required initialization and then must call <see cref="SetRunning()"/>
        /// to indicate that the service should transition into the <see cref="KubeServiceStatus.Running"/>
        /// state.  This is very important because the service test fixture requires the service to be
        /// in the running state before it allows tests to proceed.  This is necessary to avoid unit test 
        /// race conditions.
        /// </para>
        /// <para>
        /// This method should return the program exit code or throw a <see cref="ProgramExitException"/>
        /// to exit with the program exit code.
        /// </para>
        /// </remarks>
        protected abstract Task<int> OnRunAsync();

        /// <summary>
        /// <para>
        /// Loads environment variables formatted as <c>NAME=VALUE</c> from a text file as service
        /// environment variables.  The file will be decrypted using <see cref="NeonVault"/> if necessary.
        /// </para>
        /// <note>
        /// Blank lines and lines beginning with '#' will be ignored.
        /// </note>
        /// </summary>
        /// <param name="path">The input file path.</param>
        /// <param name="passwordProvider">
        /// Optionally specifies the password provider function to be used to locate the
        /// password required to decrypt the source file when necessary.  The password will 
        /// use the <see cref="KubeHelper.LookupPassword(string)"/> method when 
        /// <paramref name="passwordProvider"/> is <c>null</c>.
        /// </param>
        /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
        /// <exception cref="FormatException">Thrown for file formatting problems.</exception>
        public void LoadEnvironmentVariables(string path, Func<string, string> passwordProvider = null)
        {
            passwordProvider = passwordProvider ?? KubeHelper.LookupPassword;

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            var vault = new NeonVault(passwordProvider);
            var bytes = vault.Decrypt(path);

            using (var ms = new MemoryStream(bytes))
            {
                using (var reader = new StreamReader(ms))
                {
                    var lineNumber = 1;

                    foreach (var rawLine in reader.Lines())
                    {
                        var line = rawLine.Trim();

                        if (line.Length == 0 || line.StartsWith("#"))
                        {
                            continue;
                        }

                        var fields = line.Split(new char[] { '=' }, 2);

                        if (fields.Length != 2)
                        {
                            throw new FormatException($"[{path}:{lineNumber}]: Invalid input: {line}");
                        }

                        var name  = fields[0].Trim();
                        var value = fields[1].Trim();

                        if (name.Length == 0)
                        {
                            throw new FormatException($"[{path}:{lineNumber}]: Setting name cannot be blank.");
                        }

                        SetEnvironmentVariable(name, value);
                    }
                }
            }
        }

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
        /// <param name="def">The value to be returned when the environment variable doesn't exist (defaults to <c>null</c>).</param>
        /// <returns>The variable value or <paramref name="def"/> if the variable doesn't exist.</returns>
        public string GetEnvironmentVariable(string name, string def = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            lock (syncLock)
            {
                if (InProduction)
                {
                    return Environment.GetEnvironmentVariable(name) ?? def;
                }

                if (environmentVariables.TryGetValue(name, out var value))
                {
                    return value;
                }
                else
                {
                    return def;
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
        /// <param name="passwordProvider">
        /// Optionally specifies the password provider function to be used to locate the
        /// password required to decrypt the source file when necessary.  The password will 
        /// use the <see cref="KubeHelper.LookupPassword(string)"/> method when 
        /// <paramref name="passwordProvider"/> is <c>null</c>.
        /// </param>
        /// <exception cref="FileNotFoundException">Thrown if there's no file at <paramref name="physicalPath"/>.</exception>
        public void SetConfigFilePath(string logicalPath, string physicalPath, Func<string, string> passwordProvider = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(logicalPath));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(physicalPath));

            if (!File.Exists(physicalPath))
            {
                throw new FileNotFoundException($"Physical configuration file [{physicalPath}] does not exist.");
            }

            passwordProvider = passwordProvider ?? KubeHelper.LookupPassword;

            var vault = new NeonVault(passwordProvider);
            var bytes = vault.Decrypt(physicalPath);

            SetConfigFile(logicalPath, bytes);
        }

        /// <summary>
        /// Maps a logical configuration file path to a temporary file holding the
        /// string contents passed encoded as UTF-8.  This is typically used for
        /// initializing confguration files for unit testing.
        /// </summary>
        /// <param name="logicalPath">The logical file path (typically expressed as a Linux path).</param>
        /// <param name="contents">The content string.</param>
        /// <param name="linuxLineEndings">
        /// Optionally convert any Windows style Lline endings (CRLF) into Linux 
        /// style endings (LF).  This defaults to <c>false</c>.
        /// </param>
        public void SetConfigFile(string logicalPath, string contents, bool linuxLineEndings = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(logicalPath));
            Covenant.Requires<ArgumentNullException>(contents != null);

            if (linuxLineEndings)
            {
                contents = contents.Replace("\r\n", "\n");
            }

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
        /// Returns the physical path for the confguration file whose logical path is specified.
        /// </summary>
        /// <param name="logicalPath">The logical file path (typically expressed as a Linux path).</param>
        /// <returns>The physical path for the configuration file or <c>null</c> if the logical file path is not present.</returns>
        /// <remarks>
        /// <note>
        /// This method does not verify that the physical file actually exists.
        /// </note>
        /// </remarks>
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
                    return null;
                }

                return fileInfo.PhysicalPath;
            }
        }
    }
}
