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
            if (!string.IsNullOrEmpty(commandLine.GetOption("--ports")))
            {
                Console.WriteLine("*** ERROR: Couchbase databases do not support [--ports] mapping.");
                Program.Exit(1);
            }

            var image   = commandLine.GetOption("--image", "neoncluster/couchbase:latest");
            var runOpts = commandLine.GetOption("--runopts", "--ulimit nofile=40960:40960 --ulimit core=100000000:100000000 --ulimit memlock=100000000:100000000");

            // Generate the database cluster information we're going to persist to Consul.

            var dbInfo        = new DbClusterInfo() { ServiceType = "couchbase" };
            var clientConfig  = new DbCouchbaseConfig();

            SetConfigPort(8091, port => clientConfig.MgmtPort = port);
            SetConfigPort(8092, port => clientConfig.ApiPort = port);
            SetConfigPort(8093, port => clientConfig.QueryPort = port);
            SetConfigPort(8094, port => clientConfig.SearchPort = port);
            SetConfigPort(11210, port => clientConfig.DirectPort = port);
            SetConfigPort(18091, port => clientConfig.HttpsMgmtPort = port);
            SetConfigPort(18092, port => clientConfig.HttpsApiPort = port);

            foreach (var node in targetNodes)
            {
                clientConfig.Servers.Add(new Uri($"http://{node.PrivateAddress}:{clientConfig.MgmtPort}"));
            }

            dbInfo.ClientConfig = NeonHelper.JsonSerialize(clientConfig, Formatting.Indented);

            dbInfo.Status = DbStatus.Setup;

            foreach (var node in targetNodes)
            {
                dbInfo.Nodes.Add(
                    new DbNode()
                    {
                        Name    = node.Name,
                        Address = node.PrivateAddress.ToString(),
                        Status  = DbStatus.Setup
                    });
            }

            // Build the port publish options.

            var portOpts = new List<string>();

            foreach (var publishedPort in publishedPorts)
            {
                portOpts.Append("-p");
                portOpts.Append(publishedPort);
            }

            // Perform the setup operations. 

            var controller = new SetupController($"db create couchbase [{baseName}]", cluster.Nodes)
            {
                ShowStatus = !Program.Quiet
            };

            controller.AddStep("cluster instances",
                targetNode =>
                {
                    targetNode.Status = $"creating volume: {containerName}";

                    var response = targetNode.SudoCommand("docker-volume-create", 
                        containerName, 
                        "--label", "neon-db-service=true");

                    if (response.ExitCode != 0)
                    {
                        targetNode.Fault($"volume create failed: {response.AllText}");
                    }

                    targetNode.Status = $"run container: {containerName}";

                    response = targetNode.SudoCommand($"docker run --detach {runOpts}",
                        "--name", containerName,
                        "--volume", $"{containerName}:/opt/couchbase/var",
                        "--net", "host",
                        //portOpts,
                        "--restart", "always",
                        "--label", "neon-db-service=true",
                        "--log-driver", "json-file",
                        image);

                    if (response.ExitCode != 0)
                    {
                        targetNode.Fault($"container run failed: {response.AllText}");
                    }
                },
                targetNode => targetNodes.Count(n => n.Name == targetNode.Name) > 0);

            controller.AddStep("cluster manager",
                manager =>
                {
                    manager.Status = $"create service: {managerName}";

                    var response = manager.SudoCommand("docker service create",
                        "--name", managerName,
                        "--env", $"DATABASE={baseName}",
                        "--env", "LOG_LEVEL=INFO",
                        "--constraint", "node.role==manager",
                        "--replicas", 1,
                        "--label", "neon-db-service=true",
                        "--mount", "type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true",
                        "--log-driver", "json-file",
                        "neoncluster/neon-couchbase-manager");

                    if (response.ExitCode != 0)
                    {
                        manager.Fault($"service start failed: {response.AllText}");
                    }

                    manager.Status = $"update service state: {dbInfoKey}";

                    if (!cluster.Consul.KV.PutObject(dbInfoKey, dbInfo).Result)
                    {
                        manager.Fault($"Consul write failed: {response.AllText}");
                    }
                },
                node => node == cluster.FirstManager);

            // $todo(jeff.lill): Wait for the operation to complete.

            if (!controller.Run())
            {
                Console.WriteLine("*** ERROR: One or more deploy operations failed.");
                Program.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine("Your Couchbase cluster is ready to configured using the Admin portal:");
            Console.WriteLine();
            Console.WriteLine($"    http://{targetNodes.OrderBy(n => n.Name).First().PrivateAddress}:8091");
            Console.WriteLine();
        }
    }
}
