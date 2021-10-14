//-----------------------------------------------------------------------------
// FILE:	    DockerComposeFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Retry;

namespace Neon.Xunit
{
    /// <summary>
    /// <para>
    /// Used to run a <b>docker-compose</b> application on the current machine as a test 
    /// fixture while tests are being performed and then deletes the applicatiuon when 
    /// the fixture is disposed.
    /// </para>
    /// <note>
    /// The <see cref="DockerComposeFixture"/> and <see cref="DockerFixture"/> fixtures are
    /// not compatible with each other.  You may only use one of these at a time.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The base Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Docker container, modifying the local DNS <b>hosts</b> file, configuring
    /// environment variables or initializing a test database.
    /// </para>
    /// <para>
    /// You should explicitly disable parallel execution in all test assemblies that
    /// rely on test fixtures by adding a C# file called <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
    /// </code>
    /// <para>
    /// and then define your test classes like:
    /// </para>
    /// <code language="csharp">
    /// public class MyTests : IClassFixture&lt;DockerComposeFixture&gt;, IDisposable
    /// {
    ///     [Collection(TestCollection.NonParallel)]
    ///     [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    ///     [Fact]
    ///     public void Test()
    ///     {
    ///     }
    /// }
    /// </code>
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class DockerComposeFixture : TestFixture
    {
        //---------------------------------------------------------------------
        // Static mmembers

        /// <summary>
        /// Stops any existing docker-compose application running with the same name passed.
        /// </summary>
        /// <param name="name">The application name.</param>
        /// <param name="customContainerNames">
        /// Optionally specifies custom container names deployed by the Docker Compose file that
        /// will not be prefixed by the application name.  The fixture needs to know these so
        /// it can remove the containers when required.
        /// </param>
        private static void StopApplication(string name, string[] customContainerNames = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            // We're going to list any existing compose containers and look for any
            // with names like:
            //
            //      APPLICATION-NAME_*
            //
            // or containers that match any of the custom container names passed and
            // forcably remove them.

            var result = NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new object[] { "ps", "--all", "--format", "{{.Names}}" });

            Covenant.Assert(result.ExitCode == 0, result.ErrorText);

            var appNamePrefix  = $"{name}_";
            var containerNames = new List<string>();

            using (var reader = new StringReader(result.AllText))
            {
                foreach (var line in reader.Lines())
                {
                    if (line.StartsWith(appNamePrefix))
                    {
                        containerNames.Add(line);
                    }
                    else if (customContainerNames != null && customContainerNames.Contains(line))
                    {
                        containerNames.Add(line);
                    }
                }
            }

            if (containerNames.Count > 0)
            {
                // Looks like the application is running, so we'll need to stop it.
                // We have a couple problems to deal with:docker 
                //
                //      1. Normally you'd need the original compose file to bring the
                //         application down cleanly via docker-compose.  The problem is
                //         that we may not have the original compose file anymore because
                //         the application could have been started long ago.
                //
                //         We could save the compose file as a temp file somewhere but
                //         we're not going to do that because of #2 below.
                //
                //      2. It seems like many container images don't handle SIGTERM signals
                //         and rely on the hosting environment to wait for seconds before
                //         killing the container processes.  Docker defaults to waiting for
                //         10 seconds.  This delay is annoying, and to make it worse, compose
                //         seems to wait separately for each container it's closing, so 
                //         stopping a three container application may take 30 seconds.
                //
                // To deal with both #1 and #2 above we're going to simply [rm --force] the application's
                // containers explicitly as well as removing any lingering application networks.

                // Remove the application containers:

                result = NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new object[] { "rm", "--force", containerNames });

                Covenant.Assert(result.ExitCode == 0, result.ErrorText);
            }

            // Remove any application (possibly orphaned) networks:

            var networkNames = new List<string>();

            result = NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new object[] { "network", "ls", "--format", "{{.Name}}" });

            Covenant.Assert(result.ExitCode == 0, result.ErrorText);

            using (var reader = new StringReader(result.AllText))
            {
                foreach (var line in reader.Lines())
                {
                    if (line.StartsWith(appNamePrefix))
                    {
                        networkNames.Add(line);
                    }
                }
            }

            NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new object[] { "network", "rm", networkNames });
        }

        //---------------------------------------------------------------------
        // Instance members

        private string      composeFile;
        private bool        keepOpen;
        private string[]    customContainerNames;

        /// <summary>
        /// Constructor.
        /// </summary>
        public DockerComposeFixture()
        {
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~DockerComposeFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns the running application name or <c>null</c> if the compose file
        /// has not been started.
        /// </summary>
        public string ApplicationName { get; private set; }

        /// <summary>
        /// <para>
        /// Starts the fixture by running a Docker compose application.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="StartAsComposed(string, string, bool, string[])"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <param name="name">Specifies the application name.</param>
        /// <param name="composeFile">Specifies the contents of the <b>docker-compose.yml</b> file defining the application.</param>
        /// <param name="keepOpen">
        /// Optionally indicates that the application should continue to run after the fixture is disposed.  
        /// This defaults to <c>false</c>.
        /// </param>
        /// <param name="customContainerNames">
        /// Optionally specifies custom container names deployed by the Docker Compose file that
        /// will not be prefixed by the application name.  The fixture needs to know these so
        /// it can remove the containers when required.
        /// </param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Start(Action)"/>
        /// </exception>
        /// <remarks>
        /// <note>
        /// You must specify a valid application <paramref name="name"/> so that the fixure
        /// can remove any existing application with the same name before starting the new instance
        /// of the application.  This is very useful during test debugging when the test might be 
        /// interrupted during debugging or when <paramref name="keepOpen"/><c>=true</c>.
        /// </note>
        /// </remarks>
        public TestFixtureStatus Start(string name, string composeFile, bool keepOpen = false, string[] customContainerNames = null)
        {
            return base.Start(
                () =>
                {
                    StartAsComposed(name, composeFile, keepOpen, customContainerNames);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="name">Specifies the application name.</param>
        /// <param name="composeFile">Specifies the contents of the <b>docker-compose.yml</b> file defining the application.</param>
        /// <param name="keepOpen">
        /// Optionally indicates that the application should continue to run after the fixture is disposed.  
        /// This defaults to <c>false</c>.
        /// </param>
        /// <param name="customContainerNames">
        /// Optionally specifies custom container names deployed by the Docker Compose file that
        /// will not be prefixed by the application name.  The fixture needs to know these so
        /// it can remove the containers when required.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Start(Action)"/>
        /// </exception>
        /// <remarks>
        /// <note>
        /// You must specify a valid application <paramref name="name"/> so that the fixure
        /// can remove any existing application with the same name before starting the new instance
        /// of the application.  This is very useful during test debugging when the test might be 
        /// interrupted during debugging or when <paramref name="keepOpen"/><c>=true</c>.
        /// </note>
        /// </remarks>
        public void StartAsComposed(string name, string composeFile, bool keepOpen = false, string[] customContainerNames = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(composeFile), nameof(composeFile));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            base.CheckWithinAction();

            if (IsRunning)
            {
                return;
            }

            this.ApplicationName      = name;
            this.composeFile          = composeFile;
            this.keepOpen             = keepOpen;
            this.customContainerNames = customContainerNames;

            // Docker compose is not compatible with Docker Swarm, so we're going to 
            // execute a command to leave the swarm.  We're not going to check the 
            // exit code to ignore the error when Docker isn't in swarm mode.

            NeonHelper.Execute(NeonHelper.DockerCli, new string[] { "swarm", "leave", "--force" });

            StartApplication();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!base.IsDisposed)
                {
                    Reset();
                }

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Starts the application using the instance fields.
        /// </summary>
        private void StartApplication()
        {
            // Handle the special case where an earlier run of this application was
            // not stopped because the developer was debugging and interrupted the
            // the unit tests before the fixture was disposed or an application with
            // the same name is already running for some other reason.

            StopApplication(ApplicationName, customContainerNames);

            // Start the application.  Note that we're going to write the compose file
            // to a temporary file to accomplish this.

            using (var tempFile = new TempFile(".yml"))
            {
                File.WriteAllText(tempFile.Path, composeFile);

                var result = NeonHelper.ExecuteCapture(NeonHelper.DockerComposeCli, new string[] { "-f", tempFile.Path, "--project-name", ApplicationName, "up", "--detach" });

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot launch application [{ApplicationName}] - [exitcode={result.ExitCode}]: {result.ErrorText}");
                }
            }
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            // Remove the application if it's running and [keepOpen] is disabled.

            if (!keepOpen)
            {
                StopApplication(ApplicationName, customContainerNames);
            }

            base.Reset();
        }

        /// <summary>
        /// Restarts the application.  This is a handy way to deploy a fresh instance with the
        /// same properties while running unit tests.
        /// </summary>
        public virtual void Restart()
        {
            Reset();
            StartApplication();
        }
    }
}
