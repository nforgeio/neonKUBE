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

using Consul;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Net;

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

    neon db create TYPE [OPTIONS] NAME NODES...

ARGS:

    NAME            - Service name
    TYPE            - Identifies the desired persisted service type
    NODES           - Names one or more host nodes where the database
                      cluster nodes are to be created.

COMMON OPTIONS:

    --image=REPO:TAG    - Overrides the default Docker image repo
    --ports=PORTLIST    - Overrides default published ports
                          (e.g. 9091-9094:8091-8094,12210:11210)
    --runopts=STRING    - Overrides default Docker run options
                          (e.g. to customize ULIMIT settings)

SERVICE TYPES:

    Couchbase
    ---------
    default image:  NeonCluster/Couchbase:latest


";
        private CommandLine                         commandLine;
        private ClusterLogin                        clusterLogin;
        private ClusterProxy                        cluster;
        private string                              serviceType;
        private string                              baseName;
        private string                              containerName;
        private string                              managerName;
        private string                              dbInfoKey;
        private List<NodeProxy<NodeDefinition>>     targetNodes;
        private List<string>                        publishedPorts;
        private Dictionary<int,int>                 internalToExternalPorts;
        private ConsulClient                        consul;

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
            else if (commandLine.Arguments.Length < 2)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            this.clusterLogin  = Program.ConnectCluster();
            this.cluster       = new ClusterProxy(clusterLogin, Program.CreateNodeProxy<NodeDefinition>);
            this.commandLine   = commandLine;
            this.serviceType   = commandLine.Arguments[0].ToLowerInvariant();
            this.baseName      = commandLine.Arguments[1].ToLowerInvariant();
            this.containerName = $"neon-db-{baseName}";
            this.managerName   = $"neon-db-{baseName}-manager";
            this.dbInfoKey     = $"neon/databases/{baseName}";

            // Verify the published ports.  We're also going to create a dictionary that
            // maps internal container ports to the external ports.  The actual deployment
            // methods can use this to initialize the client settings.

            var defaultPorts = string.Empty;

            switch (serviceType)
            {
                case "couchbase":

                    defaultPorts = DefaultCouchbasePorts;
                    break;

                default:

                    Console.WriteLine($"*** ERROR: [{serviceType}] is not a currently supported persisted service.");
                    Program.Exit(1);
                    break;
            }

            var portsOption = commandLine.GetOption("--ports", defaultPorts);

            publishedPorts          = new List<string>();
            internalToExternalPorts = new Dictionary<int, int>();

            if (!string.IsNullOrEmpty(portsOption))
            {
                foreach (var portSpec in portsOption.Split(','))
                {
                    var portItem   = portSpec.Trim();
                    var invalidMsg = $"*** ERROR: Invalid port specification [{portItem}].";
                    var parts      = portItem.Split(':');

                    if (parts.Length != 2)
                    {
                        Console.WriteLine(invalidMsg);
                        Program.Exit(1);
                    }

                    var externalPart  = parts[0];
                    var internalPart  = parts[1];
                    var externalRange = externalPart.Split('-');
                    var internalRange = internalPart.Split('-');

                    if (externalRange.Length != internalRange.Length || externalRange.Length > 2)
                    {
                        Console.WriteLine(invalidMsg);
                        Program.Exit(1);
                    }

                    if (externalRange.Length == 1)
                    {
                        if (!int.TryParse(externalPart, out var externalPort) || !NetHelper.IsValidPort(externalPort))
                        {
                            Console.WriteLine(invalidMsg);
                            Program.Exit(1);
                        }

                        if (!int.TryParse(internalPart, out var internalPort) || !NetHelper.IsValidPort(internalPort))
                        {
                            Console.WriteLine(invalidMsg);
                            Program.Exit(1);
                        }

                        publishedPorts.Add($"{externalPort}:{internalPort}");
                        internalToExternalPorts[internalPort] = externalPort;
                    }
                    else
                    {
                        if (!int.TryParse(externalRange[0], out var externalPortStart) || !NetHelper.IsValidPort(externalPortStart))
                        {
                            Console.WriteLine(invalidMsg);
                            Program.Exit(1);
                        }

                        if (!int.TryParse(externalRange[1], out var externalPortEnd) || !NetHelper.IsValidPort(externalPortEnd))
                        {
                            Console.WriteLine(invalidMsg);
                            Program.Exit(1);
                        }

                        if (!int.TryParse(internalRange[0], out var internalPortStart) || !NetHelper.IsValidPort(internalPortStart))
                        {
                            Console.WriteLine(invalidMsg);
                            Program.Exit(1);
                        }

                        if (!int.TryParse(internalRange[1], out var internalPortEnd) || !NetHelper.IsValidPort(internalPortEnd))
                        {
                            Console.WriteLine(invalidMsg);
                            Program.Exit(1);
                        }

                        if (externalPortStart > externalPortEnd || internalPortStart > internalPortEnd)
                        {
                            Console.WriteLine(invalidMsg);
                            Program.Exit(1);
                        }

                        publishedPorts.Add($"{externalPortStart}-{externalPortEnd}:{internalPortStart}:{internalPortEnd}");

                        for (int port = internalPortStart; port <= internalPortEnd; port++)
                        {
                            internalToExternalPorts[port] = (port - internalPortStart) + externalPortStart;
                        }
                    }
                }
            }

            // Verify the target nodes.

            targetNodes = new List<NodeProxy<NodeDefinition>>();

            foreach (var nodeName in commandLine.Shift(2).Arguments)
            {
                try
                {
                    targetNodes.Add(cluster.GetNode(nodeName));
                }
                catch
                {
                    Console.WriteLine($"*** ERROR: Node [{nodeName}] is not present in the cluster.");
                    Program.Exit(1);
                }
            }

            if (targetNodes.Count == 0)
            {
                Console.WriteLine("*** ERROR: At least one target node is required.");
                Program.Exit(1);
            }

            // Verify that the database service doesn't already exist.

            try
            {
                var dbCluster = cluster.Consul.KV.GetObject<DbClusterInfo>(dbInfoKey).Result;

                Console.WriteLine($"*** ERROR: A database service named [{baseName}] is already deployed.");
                Program.Exit(1);
            }
            catch (AggregateException e)
            {
                if (e.InnerException is KeyNotFoundException)
                {
                    // Expecting this
                }
                else
                {
                    throw;
                }
            }

            if (cluster.FirstManager.SudoCommand($"docker service inspect {containerName}").ExitCode == 0 ||
                cluster.FirstManager.SudoCommand($"docker service inspect {managerName}").ExitCode == 0)
            {
                Console.WriteLine($"*** ERROR: One or more service components for database service [{baseName}] are already deployed.");
                Program.Exit(1);
            }

            switch (serviceType)
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

        /// <summary>
        /// Invokes a port setting action passing the internal or overriding port.
        /// </summary>
        /// <param name="internalPort">The internal container port.</param>
        /// <param name="setter">Sets the port passed.</param>
        private void SetConfigPort(int internalPort, Action<int> setter)
        {
            var port = internalPort;

            if (internalToExternalPorts.TryGetValue(internalPort, out var externalPort))
            {
                port = externalPort;
            }

            setter(port);
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: true, ensureConnection: true);
        }
    }
}
