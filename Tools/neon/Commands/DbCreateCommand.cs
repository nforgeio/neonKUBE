//-----------------------------------------------------------------------------
// FILE:	    DbCreateCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

using Neon.Cluster;
using Neon.Common;

namespace NeonTool
{
    /// <summary>
    /// Implements the <b>db create</b> command.
    /// </summary>
    public partial class DbCreateCommand : CommandBase
    {
        private const string usage = @"
Creates a persisted service.

USAGE:

    neon db create TYPE [OPTIONS] NAME

ARGS:

    NAME            - Service name
    TYPE            - Identifies the desired persisted service type

COMMON OPTIONS:

    --image=REPO:TAG    - Overrides the default Docker image repo

SERVICE TYPES:

    Couchbase
    ---------
    default image:  NeonCluster/Couchbase:latest


";
        private CommandLine     commandLine;
        private ClusterLogin    clusterLogin;
        private ClusterProxy    cluster;
        private string          serviceType;
        private string          baseName;
        private string          serviceName;
        private string          managerName;

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "db", "create" }; }
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
            else if (commandLine.Arguments.Length > 2)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            this.clusterLogin = Program.ConnectCluster();
            this.cluster      = new ClusterProxy(clusterLogin, Program.CreateNodeProxy<NodeDefinition>);
            this.commandLine  = commandLine;
            this.serviceType  = commandLine.Arguments[0];
            this.baseName     = commandLine.Arguments[1];
            this.serviceName  = baseName + "-service";
            this.managerName  = baseName + "-manager";

            if (cluster.FirstManager.SudoCommand($"docker service inspect {serviceName}").ExitCode == 0 ||
                cluster.FirstManager.SudoCommand($"docker service inspect {managerName}").ExitCode == 0)
            {
                Console.WriteLine($"*** ERROR: One or more service components for database service [{baseName}] are already deployed.");
                Program.Exit(1);
            }

            switch (serviceType.ToLowerInvariant())
            {
                case "couchbase":

                    CreateCouchbase();
                    break;

                default:

                    Console.WriteLine($"*** ERROR: [{serviceType}] is not a currently supported persisted service.");
                    Program.Exit(1);
                    break;
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: true, ensureConnection: true);
        }
    }
}
