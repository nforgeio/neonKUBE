//-----------------------------------------------------------------------------
// FILE:	    LogServices.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Cluster;
using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Handles the provisioning of cluster logging related services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonCLUSTER logging is implemented by deploying <b>Elasticsearch</b>, <b>TD-Agent</b>, and 
    /// <b>Kibana</b>, <b>Metricbeat</b> as well as the neonCLUSTER <b>neon-log-collector</b> service
    /// and <b>neon-log-host</b> containers.
    /// </para>
    /// <para>
    /// <b>Elasticsearch</b> acts as the stateful database for cluster log events.  This is deployed
    /// to one or more cluster nodes using <b>docker run</b> so each instance can easily be
    /// managed and upgraded individually.  This is collectively known as the <b>neon-log-esdata</b>
    /// service (but it is not actually deployed as a Docker service).
    /// </para>
    /// <para>
    /// Each Elasticsearch container joins the Docker host network and listens on two ports: 
    /// <see cref="NeonHostPorts.LogEsDataTcp"/> handles internal intra-node Elasticsearch 
    /// traffic and <see cref="NeonHostPorts.LogEsDataHttp"/> exposes the public Elasticsearch
    /// HTTP API.  This class provides enough information to each of the instances so they can 
    /// discover each other and establish a cluster.  Attaching to the host network is required
    /// so that ZEN cluster discovery will work properly. 
    /// </para>
    /// <para>
    /// The <b>neon-log-esdata</b> containers are deployed behind the cluster's <b>private</b>
    /// proxy, with a route defined for each Elasticsearch container.  Cluster TD-Agents and Kibana 
    /// use the built-in <see cref="NeonHosts.LogEsData"/> DNS name to submit HTTP requests on
    /// port <see cref="NeonHostPorts.ProxyPrivateHttpLogEsData"/> to Elasticsearch via the proxy.
    /// </para>
    /// <para>
    /// <b>TD-Agent</b> is the community version of <b>Fluend</b> and is the foundation of the
    /// neonCLUSTER logging pipeline.  This is deployed as the <b>neon-log-host</b> local container
    /// to every cluster node to capture the host systemd journal and syslog events as well
    /// as any container events forwarded by the local Docker daemon via the <b>fluent</b>
    /// log driver.  The appropriate events will be forwarded to the cluster's <b>neon-log-collector</b>
    /// service for further processing.
    /// </para>
    /// <para>
    /// <b>neon-log-collector</b> is the cluster Docker service responsible for receiving events from
    /// hosts, filtering and normalizing them and then persisting them to Elasticsearch.  The <b>neon-log-collector</b>
    /// service is deployed behind the cluster's <b>private</b> proxy.  Cluster <b>neon-log-host</b> 
    /// containers will forward events to TCP port <see cref="NeonHostPorts.ProxyPrivateTcpLogCollector"/>
    /// to this service via  the proxy.
    /// </para>
    /// <para>
    /// <b>Kibana</b> is deployed as the <b>neon-log-kibana</b> docker service and acts
    /// as the logging dashboard.
    /// </para>
    /// </remarks>
    public class LogServices
    {
        private ClusterProxy cluster;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster proxy.</param>
        public LogServices(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Configures the cluster logging related services.
        /// </summary>
        /// <param name="firstManager">The first cluster proxy manager.</param>
        public void Configure(NodeProxy<NodeDefinition> firstManager)
        {
            if (!cluster.Definition.Log.Enabled)
            {
                return;
            }

            firstManager.InvokeIdempotentAction("setup-log-services",
                () =>
                {
                    var steps = new ConfigStepList();

                    AddElasticsearchSteps(steps);

                    if (cluster.Definition.Dashboard.Kibana)
                    {
                        AddKibanaSteps(steps);
                    }

                    AddCollectorSteps(steps);
                    AddHostSteps(steps);

                    cluster.Configure(steps);

                    // Wait for the Elasticsearch cluster to come online.  Note that we're going 
                    // to wait here even if Kibana isn't enabled to ensure that Elasticsearch is 
                    // in a good state.

                    var esNodeCount = cluster.Definition.Nodes.Count(n => n.Labels.LogEsData);

                    firstManager.Status = $"Waiting for [neon-log-esdata] cluster [0/{esNodeCount} nodes ready] (be patient)";

                    using (var jsonClient = new JsonClient())
                    {
                        var baseLogEsDataUri = $"http://{NeonHosts.LogEsData}:{NeonHostPorts.ProxyPrivateHttpLogEsData}";
                        var timeout          = TimeSpan.FromMinutes(10);
                        var timeoutTime      = DateTime.UtcNow + timeout;

                        // Wait for the Elasticsearch cluster.

                        jsonClient.UnsafeRetryPolicy = NoRetryPolicy.Instance;

                        while (true)
                        {
                            try
                            {
                                var response = jsonClient.GetUnsafeAsync($"{baseLogEsDataUri}/_cluster/health").Result;

                                if (response.IsSuccess)
                                {
                                    dynamic clusterStatus = response.AsDynamic();

                                    firstManager.Status = $"Waiting for [neon-log-esdata] cluster [{clusterStatus.number_of_nodes}/{esNodeCount} nodes ready] (be patient)";

                                    if (clusterStatus.status == "green" && clusterStatus.number_of_nodes == esNodeCount)
                                    {
                                        break;
                                    }
                                }
                            }
                            catch
                            {
                                if (DateTime.UtcNow >= timeoutTime)
                                {
                                    firstManager.Fault($"Unable to verify [neon-log-esdata] cluster after waiting [{timeout}].");
                                    return;
                                }
                            }

                            Thread.Sleep(TimeSpan.FromSeconds(10));
                        }

                        // Initialize the Kibana [logstash-*] index pattern if Kibana is enabled.
                        //
                        // NOTE: 
                        //
                        // We're NOT going to initialize the Elasticsearch index mappings here.
                        // That will be handled by the [neon-log-collector] service.

                        if (cluster.Definition.Dashboard.Kibana)
                        {
                            // Hit Kibana once so it will write its configuration document.

                            firstManager.Status = "Waiting for [Kibana] to initialize (be patient)";

                            timeoutTime = DateTime.UtcNow + timeout;

                            while (true)
                            {
                                try
                                {
                                    var response = jsonClient.GetAsync($"http://{firstManager.Metadata.PrivateAddress}:{NeonHostPorts.Kibana}/").Result;

                                    if (response.IsSuccess)
                                    {
                                        break;
                                    }
                                }
                                catch
                                {
                                    if (DateTime.UtcNow >= timeoutTime)
                                    {
                                        firstManager.Fault($"[Kibana] not initialized after waiting [{timeout}].");
                                        return;
                                    }
                                }

                                Thread.Sleep(TimeSpan.FromSeconds(10));
                            }

                            // Wait for Kibana to initialize its configuration document.

                            firstManager.Status = "Waiting for [Kibana] to initialize (be patient)";

                            timeoutTime = DateTime.UtcNow + timeout;

                            JObject     queryResults;
                            JObject     hitsObject;
                            JArray      hitsArray;

                            while (true)
                            {
                                try
                                {
                                    var response = jsonClient.GetUnsafeAsync($"{baseLogEsDataUri}/.kibana/config/_search").Result;

                                    if (response.IsSuccess)
                                    {
                                        queryResults = (JObject)response.AsDynamic();
                                        hitsObject   = (JObject)queryResults.GetValue("hits");
                                        hitsArray    = (JArray)hitsObject.GetValue("hits");

                                        if (hitsArray.Count > 0)
                                        {
                                            break;
                                        }
                                    }
                                }
                                catch
                                {
                                    if (DateTime.UtcNow >= timeoutTime)
                                    {
                                        firstManager.Fault($"[Kibana] not initialized after waiting [{timeout}].");
                                    }
                                }

                                Thread.Sleep(TimeSpan.FromSeconds(10));
                            }

                            // Initialize the Kibana [logstash-*] index pattern.

                            jsonClient.PostAsync($"{baseLogEsDataUri}/.kibana/index-pattern/logstash-*",
@"{
""title"": ""logstash-*"",
""timeFieldName"": ""@timestamp""
}
").Wait();

                            // Make the [logstash-*] index pattern the default Kibana index.

                            var kibanaConfigHit = (JObject)hitsArray[0];
                            var kibanaConfig = (JObject)kibanaConfigHit.GetValue("_source");

                            kibanaConfig["defaultIndex"] = "logstash-*";

                            jsonClient.PostAsync($"{baseLogEsDataUri}/.kibana/config/{kibanaConfigHit.GetValue("_id")}", kibanaConfig).Wait();
                        }
                    }

                    firstManager.Status = string.Empty;
                });
        }

        /// <summary>
        /// Adds the steps to configure the stateful Elasticsearch instances used to persist the log data.
        /// </summary>
        /// <param name="steps">The configuration step list.</param>
        private void AddElasticsearchSteps(ConfigStepList steps)
        {
            var esNodes = new List<NodeProxy<NodeDefinition>>();

            foreach (var nodeDefinition in cluster.Definition.Nodes.Where(n => n.Labels.LogEsData))
            {
                esNodes.Add(cluster.GetNode(nodeDefinition.Name));
            }

            // Determine number of manager nodes and the quorum size.
            // Note that we'll deploy an odd number of managers.

            var managerCount = Math.Min(esNodes.Count, 5);   // We shouldn't ever need more than 5 managers

            if (!NeonHelper.IsOdd(managerCount))
            {
                managerCount--;
            }

            var quorumCount = (managerCount / 2) + 1;

            // Sort the nodes by name and then separate the manager and
            // worker nodes (managers will be assigned to nodes that appear
            // first in the list).

            var managerEsNodes = new List<NodeProxy<NodeDefinition>>();
            var normalEsNodes  = new List<NodeProxy<NodeDefinition>>();

            esNodes = esNodes.OrderBy(n => n.Name).ToList();

            foreach (var esNode in esNodes)
            {
                if (managerEsNodes.Count < managerCount)
                {
                    managerEsNodes.Add(esNode);
                }
                else
                {
                    normalEsNodes.Add(esNode);
                }
            }

            // Figure out how much RAM to allocate to the Elasticsearch Docker containers
            // as well as Java heap within.  The guidance is to set the heap size to half
            // the container RAM up to a maximum of 31GB.

            var esContainerRam = cluster.Definition.Log.EsMemoryBytes;
            var esHeapBytes    = Math.Min(esContainerRam / 2, 31L * NeonHelper.Giga);

            // We're going to use explicit docker commands to deploy the Elasticsearch cluster
            // log storage containers.
            //
            // We're mounting three volumes to the container:
            //
            //      /etc/neoncluster/env-host         - Generic host specific environment variables
            //      /etc/neoncluster/env-log-esdata   - Elasticsearch node host specific environment variables
            //      neon-log-esdata-#                 - Persistent Elasticsearch data folder

            var esBootstrapNodes = new StringBuilder();

            foreach (var esMasterNode in managerEsNodes)
            {
                esBootstrapNodes.AppendWithSeparator($"{esMasterNode.PrivateAddress}:{NeonHostPorts.LogEsDataTcp}", ",");
            }

            // Create a data volume for each Elasticsearch node and then start the node container.

            for (int i = 0; i < esNodes.Count; i++)
            {
                var esNode        = esNodes[i];
                var containerName = $"neon-log-esdata-{i}";
                var isMaster      = managerEsNodes.Contains(esNode) ? "true" : "false";
                var volumeCommand = CommandStep.CreateSudo(esNode.Name, "docker-volume-create", containerName);

                steps.Add(volumeCommand);

                var runCommand = CommandStep.CreateIdempotentDocker(esNode.Name, "setup-neon-log-esdata",
                    "docker run",
                        "--name", containerName,
                        "--detach",
                        "--restart", "always",
                        "--volume", "/etc/neoncluster/env-host:/etc/neoncluster/env-host:ro",
                        "--volume", $"{containerName}:/mnt/esdata",
                        "--env", $"ELASTICSEARCH_CLUSTER={cluster.Definition.Datacenter}.{cluster.Definition.Name}.neon-log-esdata",
                        "--env", $"ELASTICSEARCH_NODE_MASTER={isMaster}",
                        "--env", $"ELASTICSEARCH_NODE_DATA=true",
                        "--env", $"ELASTICSEARCH_NODE_COUNT={esNodes.Count}",
                        "--env", $"ELASTICSEARCH_HTTP_PORT={NeonHostPorts.LogEsDataHttp}",
                        "--env", $"ELASTICSEARCH_TCP_PORT={NeonHostPorts.LogEsDataTcp}",
                        "--env", $"ELASTICSEARCH_QUORUM={quorumCount}",
                        "--env", $"ELASTICSEARCH_BOOTSTRAP_NODES={esBootstrapNodes}",
                        "--env", $"ES_JAVA_OPTS=-Xms{esHeapBytes / NeonHelper.Mega}M -Xmx{esHeapBytes / NeonHelper.Mega}M",
                        "--memory", $"{esContainerRam / NeonHelper.Mega}M",
                        "--memory-reservation", $"{esContainerRam / NeonHelper.Mega}M",
                        "--memory-swappiness", "0",
                        "--network", "host",
                        "--log-driver", "json-file",
                        cluster.Definition.Log.EsImage);

                steps.Add(runCommand);

                var scriptText =
$@"
{volumeCommand.ToBash()}

{runCommand.ToBash()}
";
                steps.Add(UploadStep.Text(esNode.Name, LinuxPath.Combine(NodeHostFolders.Scripts, "neon-log-esdata.sh"), scriptText));
            }

            // Configure a private cluster proxy route to the Elasticsearch nodes.

            var route = new ProxyHttpRoute()
            {
                Name     = "neon-log-esdata",
                Log      = false,   // This is important: we don't want to SPAM the log database with its own traffic.
                Resolver = null
            };

            route.Frontends.Add(
                new ProxyHttpFrontend()
                {
                     Host      = NeonHosts.LogEsData,
                     ProxyPort = NeonHostPorts.ProxyPrivateHttpLogEsData
                });

            foreach (var esNode in esNodes)
            {
                route.Backends.Add(
                    new ProxyHttpBackend()
                    {
                        Server = esNode.Metadata.PrivateAddress.ToString(),
                        Port   = NeonHostPorts.LogEsDataHttp
                    });
            }

            cluster.PrivateProxy.SetRoute(route);
        }

        /// <summary>
        /// Adds the steps required to configure the Kibana Elasticsearch/log user interface.
        /// </summary>
        /// <param name="steps">The configuration step list.</param>
        private void AddKibanaSteps(ConfigStepList steps)
        {
            // This is super simple: All we need to do is to launch the Kibana 
            // service on the cluster managers.

            var command =
                CommandStep.CreateIdempotentDocker(cluster.FirstManager.Name, "setup-neon-log-kibana",
                    "docker service create",
                        "--name", "neon-log-kibana",
                        "--mode", "global",
                        "--endpoint-mode", "vip",
                        "--restart-delay", cluster.Definition.Docker.RestartDelay,
                        "--network", NeonClusterConst.PrivateNetwork,
                        "--constraint", $"node.role==manager",
                        "--publish", $"{NeonHostPorts.Kibana}:{NetworkPorts.Kibana}",
                        "--mount", "type=bind,source=/etc/neoncluster/env-host,destination=/etc/neoncluster/env-host,readonly=true",
                        "--env", $"ELASTICSEARCH_URL=http://{NeonHosts.LogEsData}:{NeonHostPorts.ProxyPrivateHttpLogEsData}",
                        "--log-driver", "json-file",    // Ensure that we don't log to the pipeline to avoid cascading events.
                        cluster.Definition.Log.KibanaImage);

            steps.Add(command);
            steps.Add(cluster.GetFileUploadSteps(cluster.Managers, LinuxPath.Combine(NodeHostFolders.Scripts, "neon-log-kibana.sh"), command.ToBash()));
        }

        /// <summary>
        /// Adds the steps required to configure the cluster log collector which aggregates log events received
        /// from all cluster nodes via their [neon-log-host] containers.
        /// </summary>
        /// <param name="steps">The configuration step list.</param>
        private void AddCollectorSteps(ConfigStepList steps)
        {
            var command =
                CommandStep.CreateIdempotentDocker(cluster.FirstManager.Name, "setup-neon-log-collector",
                    "docker service create",
                        "--name", "neon-log-collector",
                        "--mode", "global",
                        "--restart-delay", cluster.Definition.Docker.RestartDelay,
                        "--endpoint-mode", "vip",
                        "--network", $"{NeonClusterConst.PrivateNetwork}",
                        "--constraint", $"node.role==manager",
                        "--mount", "type=bind,source=/etc/neoncluster/env-host,destination=/etc/neoncluster/env-host,readonly=true",
                        "--log-driver", "json-file",    // Ensure that we don't log to the pipeline to avoid cascading events.
                        cluster.Definition.Log.CollectorImage);

            steps.Add(command);
            steps.Add(cluster.GetFileUploadSteps(cluster.Managers, LinuxPath.Combine(NodeHostFolders.Scripts, "neon-log-collector.sh"), command.ToBash()));
        }

        /// <summary>
        /// Adds the steps required to configure the [neon-log-host] containers
        /// to every cluster node.
        /// </summary>
        /// <param name="steps">The configuration step list.</param>
        private void AddHostSteps(ConfigStepList steps)
        {
            // $todo(jeff.lill):
            //
            // It would be nice to start these in parallel.  Perhaps a new
            // CommandStep.CreateParallel() method or something?

            foreach (var node in cluster.Nodes)
            {
                var runCommand = CommandStep.CreateIdempotentDocker(node.Name, "setup-neon-log-host",
                    "docker run",
                    "--name", "neon-log-host",
                    "--detach",
                    "--restart", "always",
                    "--volume", "/etc/neoncluster/env-host:/etc/neoncluster/env-host:ro",
                    "--volume", "/var/log:/hostfs/var/log",
                    "--network", "host",
                    "--log-driver", "json-file",        // Ensure that we don't log to the pipeline to avoid cascading events.
                    cluster.Definition.Log.HostImage);

                steps.Add(runCommand);
                steps.Add(UploadStep.Text(node.Name, LinuxPath.Combine(NodeHostFolders.Scripts, "neon-log-host.sh"), runCommand.ToBash()));
            }

            // Configure a private cluster proxy TCP route so the [neon-log-host] containers
            // will be able to reach the collectors.

            var route = new ProxyTcpRoute()
            {
                Name = "neon-log-collector",
                Log  = false    // This is important: we don't want to SPAM the log database with its own traffic.
            };

            route.Frontends.Add(
                new ProxyTcpFrontend()
                {
                    ProxyPort = NeonHostPorts.ProxyPrivateTcpLogCollector
                });

            route.Backends.Add(
                new ProxyTcpBackend()
                {
                    Server = "neon-log-collector",
                    Port   = NetworkPorts.TDAgentForward
                });

            cluster.PrivateProxy.SetRoute(route);
        }
    }
}
