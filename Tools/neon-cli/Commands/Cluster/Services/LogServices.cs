//-----------------------------------------------------------------------------
// FILE:	    LogServices.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

using Consul;
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
        public void Configure(SshProxy<NodeDefinition> firstManager)
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
                    cluster.Configure(steps);

                    firstManager.Status = string.Empty;
                });
        }

        /// <summary>
        /// Configures the Kibana dashboard.
        /// </summary>
        /// <param name="firstManager">The first cluster proxy manager.</param>
        public void ConfigureKibana(SshProxy<NodeDefinition> firstManager)
        {
            if (!cluster.Definition.Log.Enabled)
            {
                return;
            }

            firstManager.InvokeIdempotentAction("setup-log-kibana",
                () =>
                {
                    // Wait for the Elasticsearch cluster to come online.

                    var esNodeCount = cluster.Definition.Nodes.Count(n => n.Labels.LogEsData);

                    firstManager.Status = $"wait for [neon-log-esdata] cluster [0/{esNodeCount} nodes ready]";

                    using (var jsonClient = new JsonClient())
                    {
                        var baseLogEsDataUri = $"http://{NeonHosts.LogEsData}:{NeonHostPorts.ProxyPrivateHttpLogEsData}";
                        var baseKibanaUri    = $"http://{firstManager.PrivateAddress}:{NeonHostPorts.Kibana}";
                        var timeout          = TimeSpan.FromMinutes(10);
                        var timeoutTime      = DateTime.UtcNow + timeout;
                        var retry            = new LinearRetryPolicy(TransientDetector.Http, maxAttempts: 30, retryInterval: TimeSpan.FromSeconds(1));

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

                                    firstManager.Status = $"wait for [neon-log-esdata] cluster: [status={clusterStatus.status}] [{clusterStatus.number_of_nodes}/{esNodeCount} nodes joined])";

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
                                    firstManager.Fault($"[neon-log-esdata] cluster not ready after waiting [{timeout}].");
                                    return;
                                }
                            }

                            Thread.Sleep(TimeSpan.FromSeconds(1));
                        }

                        // The Kibana API calls below require the [kbn-xsrf] header.

                        jsonClient.DefaultRequestHeaders.Add("kbn-xsrf", "true");

                        // Load the [logstash-*] index pattern directly into Elasticsearch.

                        firstManager.Status = "load the [logstash-*] index pattern";

                        retry.InvokeAsync(
                            async () =>
                            {
                                var indexJson = ResourceFiles.Root.GetFolder("Kibana").GetFile("6-logstash-index-pattern.json").Contents;

                                indexJson = indexJson.Replace("${TIMESTAMP}", DateTime.UtcNow.ToString(NeonHelper.DateFormatTZ));

                                await jsonClient.PutAsync($"{baseLogEsDataUri}/.kibana/doc/index-pattern:logstash-*", indexJson);

                            }).Wait();

                        // Ensure that Kibana is ready before we submit any API requests.

                        firstManager.Status = "wait for kibana";

                        retry.InvokeAsync(
                            async () =>
                            {
                                var response = await jsonClient.GetAsync<dynamic>($"{baseKibanaUri}/api/status");

                                if (response.status.overall.state != "green")
                                {
                                    throw new NeonClusterException($"Kibana [state={response.status.overall.state}]");
                                }

                            }).Wait();

                        // Add the index pattern to Kibana.

                        firstManager.Status = "add index pattern to kibana";

                        retry.InvokeAsync(
                            async () =>
                            {
                                dynamic indexPattern = new ExpandoObject();
                                dynamic attributes   = new ExpandoObject();

                                attributes.title         = "logstash-*";
                                attributes.timeFieldName = "@timestamp";

                                indexPattern.attributes = attributes;

                                await jsonClient.PostAsync($"{baseKibanaUri}/api/saved_objects/index-pattern/logstash-*?overwrite=true", indexPattern);

                            }).Wait();

                        // Now we need to save a Kibana config document so that [logstash-*] will be
                        // the default index and the timestamp will be displayed as UTC and have a
                        // more useful terse format.

                        firstManager.Status = "configure kibana defaults";

                        retry.InvokeAsync(
                            async () =>
                            {
                                dynamic setting = new ExpandoObject();

                                setting.value = "logstash-*";
                                await jsonClient.PostAsync($"{baseKibanaUri}/api/kibana/settings/defaultIndex", setting);

                                setting.value = "HH:mm:ss.SSS MM-DD-YYYY";
                                await jsonClient.PostAsync($"{baseKibanaUri}/api/kibana/settings/dateFormat", setting);

                                setting.value = "UTC";
                                await jsonClient.PostAsync($"{baseKibanaUri}/api/kibana/settings/dateFormat:tz", setting);

                            }).Wait();
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
            var esNodes = new List<SshProxy<NodeDefinition>>();

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

            var managerEsNodes = new List<SshProxy<NodeDefinition>>();
            var normalEsNodes  = new List<SshProxy<NodeDefinition>>();

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

                var command = CommandStep.CreateIdempotentDocker(esNode.Name, "setup-neon-log-esdata",
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
                    Program.ResolveDockerImage(cluster.Definition.Log.EsImage));

                steps.Add(command);

                var scriptText =
$@"
{volumeCommand.ToBash()}

{command.ToBash()}
";
                steps.Add(UploadStep.Text(esNode.Name, LinuxPath.Combine(NeonHostFolders.Scripts, "neon-log-esdata.sh"), scriptText));
            }

            // Configure a private cluster proxy route to the Elasticsearch nodes.

            var rule = new LoadBalancerHttpRule()
            {
                Name     = "neon-log-esdata",
                System   = true,
                Log      = false,   // This is important: we don't want to SPAM the log database with its own traffic.
                Resolver = null
            };

            rule.Frontends.Add(
                new LoadBalancerHttpFrontend()
                {
                     ProxyPort = NeonHostPorts.ProxyPrivateHttpLogEsData
                });

            foreach (var esNode in esNodes)
            {
                rule.Backends.Add(
                    new LoadBalancerHttpBackend()
                    {
                        Server = esNode.Metadata.PrivateAddress.ToString(),
                        Port   = NeonHostPorts.LogEsDataHttp
                    });
            }

            cluster.PrivateLoadBalancer.SetRule(rule);
        }

        /// <summary>
        /// Adds the steps required to configure the Kibana Elasticsearch/log user interface.
        /// </summary>
        /// <param name="steps">The configuration step list.</param>
        private void AddKibanaSteps(ConfigStepList steps)
        {
            // This is super simple: All we need to do is to launch the Kibana 
            // service on the cluster managers.

            var command =  CommandStep.CreateIdempotentDocker(cluster.FirstManager.Name, "setup-neon-log-kibana",
                "docker service create",
                "--name", "neon-log-kibana",
                "--detach=false",
                "--mode", "global",
                "--endpoint-mode", "vip",
                "--restart-delay", cluster.Definition.Docker.RestartDelay,
                "--network", NeonClusterConst.PrivateNetwork,
                "--constraint", $"node.role==manager",
                "--publish", $"{NeonHostPorts.Kibana}:{NetworkPorts.Kibana}",
                "--mount", "type=bind,source=/etc/neoncluster/env-host,destination=/etc/neoncluster/env-host,readonly=true",
                "--env", $"ELASTICSEARCH_URL=http://{NeonHosts.LogEsData}:{NeonHostPorts.ProxyPrivateHttpLogEsData}",
                "--log-driver", "json-file",    // Ensure that we don't log to the pipeline to avoid cascading events.
                Program.ResolveDockerImage(cluster.Definition.Log.KibanaImage));

            steps.Add(command);
            steps.Add(cluster.GetFileUploadSteps(cluster.Managers, LinuxPath.Combine(NeonHostFolders.Scripts, "neon-log-kibana.sh"), command.ToBash()));
        }

        /// <summary>
        /// Adds the steps required to configure the cluster log collector which aggregates log events received
        /// from all cluster nodes via their [neon-log-host] containers.
        /// </summary>
        /// <param name="steps">The configuration step list.</param>
        private void AddCollectorSteps(ConfigStepList steps)
        {
            var command = CommandStep.CreateIdempotentDocker(cluster.FirstManager.Name, "setup-neon-log-collector",
                "docker service create",
                "--name", "neon-log-collector",
                "--detach=false",
                "--mode", "global",
                "--restart-delay", cluster.Definition.Docker.RestartDelay,
                "--endpoint-mode", "vip",
                "--network", $"{NeonClusterConst.PrivateNetwork}",
                "--constraint", $"node.role==manager",
                "--mount", "type=bind,source=/etc/neoncluster/env-host,destination=/etc/neoncluster/env-host,readonly=true",
                "--env", $"SHARD_COUNT={cluster.Definition.Log.EsShards}",
                "--env", $"REPLICA_COUNT={cluster.Definition.Log.EsReplicas}",
                "--log-driver", "json-file",    // Ensure that we don't log to the pipeline to avoid cascading events.
                Program.ResolveDockerImage(cluster.Definition.Log.CollectorImage));

            steps.Add(command);
            steps.Add(cluster.GetFileUploadSteps(cluster.Managers, LinuxPath.Combine(NeonHostFolders.Scripts, "neon-log-collector.sh"), command.ToBash()));

            // Configure a private cluster proxy TCP route so the [neon-log-host] containers
            // will be able to reach the collectors.

            var rule = new LoadBalancerTcpRule()
            {
                Name   = "neon-log-collector",
                System = true,
                Log    = false    // This is important: we don't want to SPAM the log database with its own traffic.
            };

            rule.Frontends.Add(
                new LoadBalancerTcpFrontend()
                {
                    ProxyPort = NeonHostPorts.ProxyPrivateTcpLogCollector
                });

            rule.Backends.Add(
                new LoadBalancerTcpBackend()
                {
                    Server = "neon-log-collector",
                    Port   = NetworkPorts.TDAgentForward
                });

            cluster.PrivateLoadBalancer.SetRule(rule);
        }

        /// <summary>
        /// Deploys the log related containers on a node.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        public void DeployContainers(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.InvokeIdempotentAction("setup-neon-log-host",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "start: neon-log-host";

                    var response = node.DockerCommand(
                        "docker run",
                        "--name", "neon-log-host",
                        "--detach",
                        "--restart", "always",
                        "--volume", "/etc/neoncluster/env-host:/etc/neoncluster/env-host:ro",
                        "--volume", "/var/log:/hostfs/var/log",
                        "--network", "host",
                        "--log-driver", "json-file",        // Ensure that we don't log to the pipeline to avoid cascading events.
                        Program.ResolveDockerImage(cluster.Definition.Log.HostImage));

                    node.UploadText(LinuxPath.Combine(NeonHostFolders.Scripts, "neon-log-host.sh"), response.BashCommand);
                });

            node.InvokeIdempotentAction("setup-metricbeat",
                () =>
                {
                    node.Status = "start: neon-log-metricbeat";

                    var response = node.DockerCommand(
                        "docker run",
                        "--name", "neon-log-metricbeat",
                        "--detach",
                        "--net", "host",
                        "--restart", "always",
                        "--volume", "/etc/neoncluster/env-host:/etc/neoncluster/env-host:ro",
                        "--volume", "/proc:/hostfs/proc:ro",
                        "--volume", "/:/hostfs:ro",
                        "--log-driver", "json-file",
                        Program.ResolveDockerImage(cluster.Definition.Log.MetricbeatImage));

                    node.UploadText(LinuxPath.Combine(NeonHostFolders.Scripts, "neon-log-metricbeat.sh"), response.BashCommand);
                });
        }
    }
}
