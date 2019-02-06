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
    nshell proxy SERVICE LOCAL-PORT NODE-PORT

ARGUMENTS:

    LOCAL-PORT      - local proxy port on 127.0.0.1
    NODE-PORT       - remote cluster node port

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

            var cluster       = Program.GetCluster();
            var serverAddress = cluster.FirstMaster.PrivateAddress;
            var target        = commandLine.Arguments.ElementAtOrDefault(0);
            var localPortArg  = commandLine.Arguments.ElementAtOrDefault(1);
            var nodePortArg   = commandLine.Arguments.ElementAtOrDefault(2);

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

            if (localPortArg == null)
            {
                Console.Error.WriteLine("*** ERROR: LOCAL_PORT argument is required.");
                Program.Exit(1);
            }

            if (!int.TryParse(localPortArg, out var localPort) || !NetHelper.IsValidPort(localPort))
            {
                Console.Error.WriteLine($"[LOCAL_PORT={localPortArg}] is invalid.");
                Program.Exit(1);
            }

            if (nodePortArg == null)
            {
                Console.Error.WriteLine("*** ERROR: NODE_PORT argument is required.");
                Program.Exit(1);
            }

            if (!int.TryParse(nodePortArg, out var nodePort) || !NetHelper.IsValidPort(nodePort))
            {
                Console.Error.WriteLine($"*** ERROR: [NODE_PORT={nodePortArg}] is invalid.");
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

            var localEndpoint = new IPEndPoint(IPAddress.Loopback, localPort);
            var nodeEndpoint  = new IPEndPoint(serverAddress, nodePort);

            // This starts and runs the proxy.  We don't need to dispose it because
            // the thing is supposed to run until the process is terminated.

            Console.WriteLine($" HTTP Proxy: {localEndpoint} --> {nodeEndpoint}");

            new ReverseProxy(localEndpoint, nodeEndpoint);

            // Signal [ProgramRunner] (if there is one) that we're ready for any pending tests.

            ProgramRunner.Current?.ProgramReady();

            while (true)
            {
                Thread.Sleep(300);
            }
        }
    }
}
