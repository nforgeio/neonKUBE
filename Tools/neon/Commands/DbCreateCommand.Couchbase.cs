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

using Couchbase;
using Couchbase.Configuration.Client;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;

namespace NeonTool
{
    public partial class DbCreateCommand : CommandBase
    {
        private const string DefaultCouchbasePorts = "8091-8094:8091-8094,11210:11210";

        /// <summary>
        /// Deploy Couchbase.
        /// </summary>
        private void CreateCouchbase()
        {
            var image   = commandLine.GetOption("--image", "NeonCluster/Couchbase:latest");
            var runOpts = commandLine.GetOption("--runopts", "--ulimit nofile=40960:40960 --ulimit core=100000000:100000000 --ulimit memlock=100000000:100000000");

            // Generate the database cluster information we're going to persist to Consul.

            var dbInfo         = new DbClusterInfo();
            var clientTemplate = new ClientConfiguration();
            var clientConfig   = new DbCouchbaseConfig();

            SetConfigPort(clientTemplate.ApiPort, port => clientConfig.ApiPort = port);
            SetConfigPort(clientTemplate.DirectPort, port => clientConfig.DirectPort = port);
            SetConfigPort(clientTemplate.HttpsApiPort, port => clientConfig.HttpsApiPort = port);
            SetConfigPort(clientTemplate.HttpsMgmtPort, port => clientConfig.HttpsMgmtPort = port);
            SetConfigPort(clientTemplate.MgmtPort, port => clientConfig.MgmtPort = port);
            SetConfigPort(clientTemplate.SslPort, port => clientConfig.SslPort = port);

            foreach (var node in targetNodes)
            {
                clientConfig.Servers.Add(new Uri($"http://{node.PrivateAddress}:{clientTemplate.MgmtPort}"));
            }

            var clientConfigJson = NeonHelper.JsonSerialize(clientConfig, Formatting.Indented);

            // Perform the setup operations. 

            var controller = new SetupController($"db create couchbase [{baseName}]", cluster.Nodes);

            controller.AddStep("cluster instances",
                targetNode =>
                {
                    targetNode.SudoCommand("docker-volume-create", containerName);
                    targetNode.SudoCommand($"docker run --detach {runOpts}",
                        "--name", containerName,
                        "--restart", "always",
                        image);
                },
                targetNode => targetNodes.Count(n => n.Name == targetNode.Name) > 0);

            controller.AddStep("cluster manager",
                manager =>
                {
                    manager.SudoCommand("docker service create",
                        "--env", $"DATABASE={baseName}",
                        "--env", "LOG_LEVEL=INFO",
                        "--constraint", "node.role==manager",
                        "--replicas", 1,
                        image);

                    cluster.Consul.KV.PutObject(dbInfoKey, clientConfig).Wait();
                },
                node => node == cluster.FirstManager);

            // $todo(jeff.lill): Wait for the operation to complete.

            if (!controller.Run())
            {
                Console.WriteLine("*** ERROR: One or more deploy operations failed.");
                Program.Exit(1);
            }
        }
    }
}
