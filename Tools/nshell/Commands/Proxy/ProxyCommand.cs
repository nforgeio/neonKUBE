//-----------------------------------------------------------------------------
// FILE:	    ProxyCommand.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Kube;
using Neon.Net;
using Neon.Xunit;

namespace NShell
{
    /// <summary>
    /// Implements the <b>proxy</b> command.
    /// </summary>
    public class ProxyCommand : CommandBase
    {
        private const string usage = @"
Starts a neonKUBE proxy.

USAGE:

    nshell proxy
    nshell proxy SERVICE LOCAL-ENDPOINT REMOTE-ENDPOINT

ARGUMENTS:

    LOCAL-ENDPOINT  - local proxy endpoint (IP:PORT)
    REMOTE-ENDPOINT - remote endpoint (IP:PORT)

    SERVICE         - identifies the service being proxied:

                         kube-dashboard

REMARKS:

This command sets up a local HTTP proxy that forwards requests into the
current cluster from a local machine endpoint to a specified cluster
node port.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "proxy" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            var cluster           = Program.GetCluster();
            var serverAddress     = cluster.FirstMaster.PrivateAddress;
            var target            = commandLine.Arguments.ElementAtOrDefault(0);
            var localEndpointArg  = commandLine.Arguments.ElementAtOrDefault(1);
            var remoteEndpointArg = commandLine.Arguments.ElementAtOrDefault(2);

            if (target == null)
            {
                Console.Error.WriteLine("*** ERROR: TARGET argument is required.");
                Program.Exit(1);
            }

            switch (target)
            {
                case "kube-dashboard":
                case "unit-test":

                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: Unknown [TARGET={target}].");
                    Program.Exit(1);
                    break;
            }

            if (localEndpointArg == null)
            {
                Console.Error.WriteLine("*** ERROR: LOCAL-ENDPOINT argument is required.");
                Program.Exit(1);
            }

            if (!TryParseEndpoint(localEndpointArg, out var localEndpoint))
            {
                Console.Error.WriteLine($"[LOCAL-ENDPOINT={localEndpointArg}] is invalid.");
                Program.Exit(1);
            }

            if (remoteEndpointArg == null)
            {
                Console.Error.WriteLine("*** ERROR: REMOTE-ENDPOINT argument is required.");
                Program.Exit(1);
            }

            if (!TryParseEndpoint(remoteEndpointArg, out var remoteEndpoint))
            {
                Console.Error.WriteLine($"*** ERROR: [REMOTE-ENDPOINT={remoteEndpointArg}] is invalid.");
                Program.Exit(1);
            }

            // $todo(jeff.lill:
            //
            // We should query the API server here to identify the current master
            // nodes and also to help ensure the user has cluster admin rights.
            // We also need to pass endpoints for all masters to the proxy
            // so it can forward traffic to a healthy master and finally, we
            // we need to use HTTPS to secure traffic forwarded to the cluster:
            //
            //      https://github.com/nforgeio/neonKUBE/issues/424
            //
            // See: nshell proxy improvements

            // This starts and runs the proxy.  We don't need to dispose it because
            // the thing is supposed to run until the process is terminated.

            Console.WriteLine($" HTTP Proxy: {localEndpoint} --> {remoteEndpoint}");

            new ReverseProxy(localEndpoint, remoteEndpoint);

            // Signal [ProgramRunner] (if there is one) that we're ready for any pending tests
            // and then wait for the [ProgramRunner] to signal that we need to exit the
            // program.

            if (ProgramRunner.Current != null)
            {
                ProgramRunner.Current.ProgramReady();
                ProgramRunner.Current.WaitForExit();
            }
            else
            {
                // Otherwise, sleep until the process is killed.

                while (true)
                {
                    Thread.Sleep(TimeSpan.FromMinutes(300));
                }
            }
        }

        /// <summary>
        /// Attempts to parse an IPv4 endpoint.
        /// </summary>
        /// <param name="input">The input text.</param>
        /// <param name="output">Returns as the parsed <see cref="IPEndPoint"/>.</param>
        /// <returns><c>true</c> on success.</returns>
        private bool TryParseEndpoint(string input, out IPEndPoint output)
        {
            output = null;

            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            var fields = input.Split(':');

            if (fields.Length != 2)
            {
                return false;
            }

            if (!IPAddress.TryParse(fields[0], out var address) || address.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            if (!int.TryParse(fields[1], out var port) || !NetHelper.IsValidPort(port))
            {
                return false;
            }

            output = new IPEndPoint(address, port);

            return true;
        }
    }
}
