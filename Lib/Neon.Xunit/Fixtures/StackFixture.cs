//-----------------------------------------------------------------------------
// FILE:	    StackFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Retry;
using Org.BouncyCastle.Utilities;

namespace Neon.Xunit
{
    /// <summary>
    /// Used to run a Docker stack on the current machine as a test 
    /// fixture while tests are being performed and then deletes the
    /// stack when the fixture is disposed.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Couchbase Docker stack, modifying the local DNS <b>hosts</b>
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
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class StackFixture : TestFixture
    {
        //---------------------------------------------------------------------
        // Static members

        private static string defaultHostInterface = "0.0.0.0";

        /// <summary>
        /// Specifies the IP address of host interface where stack ports
        /// will be published.  This defaults to <b>0.0.0.0</b> which binds
        /// ports to all network interfaces.
        /// </summary>
        /// <remarks>
        /// <para>
        /// You may need to customize this to avoid port conflicts with other
        /// running applications.  When all tests are running on a single host,
        /// you should consider setting this to one of the 16 million loopback
        /// addresses in the <b>127.0.0.0/8</b> subnet (e.g. 127.0.0.1, 127.0.0.2,
        /// etc).  You'll need to set this before starting any fixture stacks.
        /// </para>
        /// <note>
        /// Fixtures implemented by neonFORGE that are derived from <see cref="StackFixture"/> 
        /// all implement this behavior.  If you implement your own derived fixtures,
        /// you should consider implementing this as well for consistency.
        /// </note>
        /// </remarks>
        public static string DefaultHostInterface
        {
            get => defaultHostInterface;

            set
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(value), nameof(value));
                Covenant.Requires<ArgumentException>(IPAddress.TryParse(value, out var address) && address.AddressFamily == AddressFamily.InterNetwork, nameof(value), $"[{value}] is not a valid IPv4 address.");
                
                defaultHostInterface = value;
            }
        }

        /// <summary>
        /// Used by derived fixtures to retrieve the host network interface address for the docker
        /// <b>-p</b> port publish option or the address to use for establishing a Cadence connections.
        /// interfaces.
        /// </summary>
        /// <param name="hostInterface">The desired host interface IPv4 address or <c>null</c>.</param>
        /// <param name="forConnection">
        /// Indicates that the address a client should use to establish a connection should be 
        /// returned vs. the address the stack will listen on.
        /// </param>
        /// <returns>The target network interface address.</returns>
        /// <remarks>
        /// This method returns <see cref="DefaultHostInterface"/> when <paramref name="hostInterface"/>
        /// is <c>null</c> or empty otherwise it will ensure that the parameter is valid
        /// and before returning it.
        /// </remarks>
        protected static string GetHostInterface(string hostInterface, bool forConnection = false)
        {
            if (string.IsNullOrEmpty(hostInterface))
            {
                hostInterface = DefaultHostInterface;
            }

            Covenant.Requires<ArgumentException>(IPAddress.TryParse(hostInterface, out var address) && address.AddressFamily == AddressFamily.InterNetwork, nameof(hostInterface), $"[{hostInterface}] is not a valid IPv4 address.");

            if (forConnection && hostInterface == "0.0.0.0")
            {
                return "127.0.0.1";
            }

            return hostInterface;
        }

        //---------------------------------------------------------------------
        // Instance members

        // Arguments required to restart the stack.

        private TimeSpan    removeDelay = TimeSpan.FromSeconds(5); // $hack(jefflill): FRAGILE
        private string      name;
        private string      stackDefinition;
        private bool        keepOpen;

        /// <summary>
        /// Constructor.
        /// </summary>
        public StackFixture()
        {
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~StackFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns the running stack's name or <c>null</c> if the stack
        /// has not been started.
        /// </summary>
        public string StackName { get; private set; }

        /// <summary>
        /// <para>
        /// Starts the stack.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="StartAsComposed(string, string, bool)"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <param name="name">Specifies the stack name.</param>
        /// <param name="stackDefinition">Specifies the contents of the <b>docker-compose.yml</b> file defining the stack.</param>
        /// <param name="keepOpen">
        /// Optionally indicates that the stack should continue to run after the fixture is disposed.  
        /// This defaults to <c>false</c>.
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
        /// You must specify a valid stack <paramref name="name"/>so that the fixure
        /// can remove any existing stack with the same name before starting the new stack.
        /// This is very useful during test debugging when the test might be interrupted during 
        /// debugging before ensuring that the stack is stopped.
        /// </note>
        /// </remarks>
        public TestFixtureStatus Start(string name, string stackDefinition, bool keepOpen = false)
        {
            return base.Start(
                () =>
                {
                    StartAsComposed(name, stackDefinition, keepOpen);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="name">Specifies the stack name.</param>
        /// <param name="stackDefinition">Specifies the contents of the <b>docker-compose.yml</b> file defining the stack.</param>
        /// <param name="keepOpen">
        /// Optionally indicates that the stack should continue to run after the fixture is disposed.  
        /// This defaults to <c>false</c>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Start(Action)"/>
        /// </exception>
        /// <remarks>
        /// <note>
        /// You must specify a valid stack <paramref name="name"/>so that the fixure
        /// can remove any existing stack with the same name before starting the new stack.
        /// This is very useful during test debugging when the test might be interrupted during 
        /// debugging before ensuring that the stack is stopped.
        /// </note>
        /// </remarks>
        public void StartAsComposed(string name, string stackDefinition, bool keepOpen = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(stackDefinition), nameof(stackDefinition));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            base.CheckWithinAction();

            if (IsRunning)
            {
                return;
            }

            this.name            = name;
            this.stackDefinition = stackDefinition;
            this.keepOpen        = keepOpen;

            StartStack();
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
                    Reset();
                }

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Starts the stack using the instance fields.
        /// </summary>
        private void StartStack()
        {
            // Handle the special case where an earlier run of this stack was
            // not stopped because the developer was debugging and interrupted the
            // the unit tests before the fixture was disposed or a stack with
            // the same name is already running for some other reason.
            //
            // We're just going to issue a command to remove the stack and ignore
            // any error code (presumably indicating that the stack doesn't exist).

            var result = NeonHelper.ExecuteCapture("docker.exe", new string[] { "stack", "ls" });

            if (result.AllText.Contains(name))
            {
                NeonHelper.ExecuteCapture($"docker.exe", new string[] { "stack", "rm", name });

                // $hack(jefflill):
                //
                // Unforunately, the [docker stack rm ...] command is asynchronous, which
                // means that that containers and related assets like networks will not
                // necessarily be removed when the command returns.  This is uper annoying
                // and has been an open Docker CLI bug since 2017:
                //
                //      https://github.com/moby/moby/issues/32620
                //
                // We're going to workaround this by waiting until all stack containers
                // and networks are actually removed.  We're going to assume that these
                // will have names prefixed by stack "<name>_" which may be a bit fragile.

                // Poll until there are no containers named like "<name>_*"

                var pollTimeout = TimeSpan.FromSeconds(60);

                NeonHelper.WaitFor(
                    () =>
                    {
                        var result = NeonHelper.ExecuteCapture("docker.exe", new string[] { "ps" });

                        return !result.OutputText.Contains($"{name}_");
                    },
                    timeout:  pollTimeout,
                    pollTime: TimeSpan.FromMilliseconds(250));

                // Poll until there are no networks named like "<name>_*"

                NeonHelper.WaitFor(
                    () =>
                    {
                        var result = NeonHelper.ExecuteCapture("docker.exe", new string[] { "network", "ls" });

                        return !result.OutputText.Contains($"{name}_");
                    },
                    timeout:  pollTimeout,
                    pollTime: TimeSpan.FromMilliseconds(250));

                Thread.Sleep(removeDelay);
            }

            // Start the stack.  Note that we're going to write the stack definition
            // to a temporary file.  We could have streamed the definition as STDIN
            // but Neon.Common doesn't have nice methods for this yet.

            using (var tempFile = new TempFile(".yml"))
            {
                File.WriteAllText(tempFile.Path, stackDefinition);

                result = NeonHelper.ExecuteCapture("docker.exe", new string[] { "stack", "deploy", "-c", tempFile.Path, name });

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot launch stack [{name}] - [exitcode={result.ExitCode}]: {result.ErrorText}");
                }
                else
                {
                    StackName = name;
                }
            }
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            // Remove the stack if it's running and [keepOpen] is disabled.

            if (!keepOpen)
            {
                NeonHelper.ExecuteCapture($"docker.exe", new string[] { "stack", "rm", name });
            }

            base.Reset();
        }

        /// <summary>
        /// Restarts the stack.  This is a handy way to deploy a fresh stack with the
        /// same properties while running unit tests.
        /// </summary>
        public void Restart()
        {
            Reset();
            StartStack();
        }
    }
}
