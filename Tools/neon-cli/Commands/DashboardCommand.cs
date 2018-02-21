//-----------------------------------------------------------------------------
// FILE:	    DashboardCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>dashboard</b> command.
    /// </summary>
    public class DashboardCommand : CommandBase
    {
        private const string usage = @"
Manages cluster dashboards.  Dashboards are simply URLs registered with
a name.

USAGE:

    neon dashboard NAME             - Display dashboard in a browser
    neon dashboard ls|list          - Lists the dashboards
    neon dashboard rm|remove NAME   - Removes a dashboard
    neon dashboard set NAME URL     - Registers a dashboard
    neon dashboard url NAME         - Returns a dashboard's URL

REMARKS:

Many dashboards will require proxy routes.  These will need to be 
registered elsewhere using [proxy] commands.  Note that the following
names are reserved for use as dashboard commands:

    list, ls, rm, remove, set, url
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "dashboard" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length != 1)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            var clusterLogin = Program.ConnectCluster();
            var cluster      = new ClusterProxy(clusterLogin, Program.CreateNodeProxy<NodeDefinition>);
            var dashboard    = commandLine.Arguments[0];
            var nodeName     = commandLine.GetOption("--node");

            SshProxy<NodeDefinition> node;

            if (nodeName == null)
            {
                node = cluster.FirstManager;
            }
            else
            {
                node = cluster.GetNode(nodeName);
            }

            switch (dashboard)
            {
                case "consul":

                    NeonHelper.OpenBrowser($"http://{node.PrivateAddress}:{NetworkPorts.Consul}/ui");
                    break;

                case "kibana":

                    NeonHelper.OpenBrowser($"http://{node.PrivateAddress}:{NeonHostPorts.Kibana}");
                    break;

                default:

                    Console.WriteLine($"Unknown dashboard: [{dashboard}]");
                    Program.Exit(1);
                    break;
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: false);
        }
    }
}
