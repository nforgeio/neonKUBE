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

using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Handles the provisioning of hive logging related services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonHIVE logging is implemented by deploying <b>Elasticsearch</b>, <b>TD-Agent</b>, and 
    /// <b>Kibana</b>, <b>Metricbeat</b> as well as the neonHIVE <b>neon-log-collector</b> service
    /// and <b>neon-log-host</b> containers.
    /// </para>
    /// <para>
    /// <b>Elasticsearch</b> acts as the stateful database for hive log events.  This is deployed
    /// to one or more hive nodes using <b>docker run</b> so each instance can easily be
    /// managed and upgraded individually.  This is collectively known as the <b>neon-log-esdata</b>
    /// service (but it is not actually deployed as a Docker service).
    /// </para>
    /// <para>
    /// Each Elasticsearch container joins the Docker host network and listens on two ports: 
    /// <see cref="HiveHostPorts.LogEsDataTcp"/> handles internal intra-node Elasticsearch 
    /// traffic and <see cref="HiveHostPorts.LogEsDataHttp"/> exposes the public Elasticsearch
    /// HTTP API.  This class provides enough information to each of the instances so they can 
    /// discover each other and establish a hive.  Attaching to the host network is required
    /// so that ZEN cluster discovery will work properly. 
    /// </para>
    /// <para>
    /// The <b>neon-log-esdata</b> containers are deployed behind the hive's <b>private</b>
    /// proxy, with a route defined for each Elasticsearch container.  Hive TD-Agents and Kibana 
    /// use the built-in <see cref="HiveHostnames.LogEsData"/> DNS name to submit HTTP requests on
    /// port <see cref="HiveHostPorts.ProxyPrivateHttpLogEsData"/> to Elasticsearch via the proxy.
    /// </para>
    /// <para>
    /// <b>TD-Agent</b> is the community version of <b>Fluend</b> and is the foundation of the
    /// neonHIVE logging pipeline.  This is deployed as the <b>neon-log-host</b> local container
    /// to every hive node to capture the host systemd journal and syslog events as well
    /// as any container events forwarded by the local Docker daemon via the <b>fluent</b>
    /// log driver.  The appropriate events will be forwarded to the hive's <b>neon-log-collector</b>
    /// service for further processing.
    /// </para>
    /// <para>
    /// <b>neon-log-collector</b> is the hive Docker service responsible for receiving events from
    /// hosts, filtering and normalizing them and then persisting them to Elasticsearch.  The <b>neon-log-collector</b>
    /// service is deployed behind the hive's <b>private</b> proxy.  Hive <b>neon-log-host</b> 
    /// containers will forward events to TCP port <see cref="HiveHostPorts.ProxyPrivateTcpLogCollector"/>
    /// to this service via  the proxy.
    /// </para>
    /// <para>
    /// <b>Kibana</b> is deployed as the <b>neon-log-kibana</b> docker service and acts
    /// as the hive logging dashboard.
    /// </para>
    /// </remarks>
    public class LogServices
    {
        private HiveProxy hive;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="hive">The hive proxy.</param>
        public LogServices(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            this.hive = hive;
        }

        /// <summary>
        /// Configures the hive logging related services.
        /// </summary>
        /// <param name="firstManager">The first hive proxy manager.</param>
        public void Configure(SshProxy<NodeDefinition> firstManager)
        {
            if (!hive.Definition.Log.Enabled)
            {
                return;
            }

            firstManager.InvokeIdempotentAction("setup/log-services",
                () =>
                {
                    var steps = new ConfigStepList();

                    AddElasticsearchSteps(steps);

                    if (hive.Definition.Dashboard.Kibana)
                    {
                        AddKibanaSteps(steps);
                    }

                    AddCollectorSteps(steps);
                    hive.Configure(steps);

                    firstManager.Status = string.Empty;
                });
        }

        /// <summary>
        /// Configures the Kibana dashboard.
        /// </summary>
        /// <param name="firstManager">The first hive proxy manager.</param>
        public void ConfigureKibana(SshProxy<NodeDefinition> firstManager)
        {
            if (!hive.Definition.Log.Enabled)
            {
                return;
            }

            firstManager.InvokeIdempotentAction("setup/log-kibana",
                () =>
                {
                    using (var jsonClient = new JsonClient())
                    {
                        var baseLogEsDataUri = hive.Definition.LogEsDataUri;
                        var baseKibanaUri    = $"http://{firstManager.PrivateAddress}:{HiveHostPorts.Kibana}";
                        var timeout          = TimeSpan.FromMinutes(5);
                        var retry             = new LinearRetryPolicy(TransientDetector.Http, maxAttempts: 30, retryInterval: TimeSpan.FromSeconds(2));

                        // The Kibana API calls below require the [kbn-xsrf] header.

                        jsonClient.DefaultRequestHeaders.Add("kbn-xsrf", "true");

                        // Ensure that Kibana is ready before we submit any API requests.

                        firstManager.Status = "wait for kibana";

                        retry.InvokeAsync(
                            async () =>
                            {
                                var response = await jsonClient.GetAsync<dynamic>($"{baseKibanaUri}/api/status");

                                if (response.status.overall.state != "green")
                                {
                                    throw new TransientException($"Kibana [state={response.status.overall.state}]");
                                }

                            }).Wait();

                        // Add the index pattern to Kibana.

                        firstManager.Status = "configure kibana index pattern";

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

                        // Set the Kibana traffic manager rule.

                        firstManager.Status = "kibana traffic manager rule";

                        var rule = new TrafficHttpRule()
                        {
                            Name     = "neon-log-kibana",
                            System   = true,
                            Log      = true,
                            Resolver = null
                        };

                        rule.Frontends.Add(
                            new TrafficHttpFrontend()
                            {
                                ProxyPort = HiveHostPorts.ProxyPrivateKibanaDashboard
                            });

                        rule.Backends.Add(
                            new TrafficHttpBackend()
                            {
                                Server = "neon-log-kibana",
                                Port   = NetworkPorts.Kibana
                            });

                        hive.PrivateTraffic.SetRule(rule);

                        firstManager.Status = string.Empty;
                    }
                });
        }

        /// <summary>
        /// Adds the steps to configure the stateful Elasticsearch instances used to persist the log data.
        /// </summary>
        /// <param name="steps">The configuration step list.</param>
        private void AddElasticsearchSteps(ConfigStepList steps)
        {
            var esNodes = new List<SshProxy<NodeDefinition>>();

            foreach (var nodeDefinition in hive.Definition.Nodes.Where(n => n.Labels.LogEsData))
            {
                esNodes.Add(hive.GetNode(nodeDefinition.Name));
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

            var esContainerRam = hive.Definition.Log.EsMemoryBytes;
            var esHeapBytes    = Math.Min(esContainerRam / 2, 31L * NeonHelper.Giga);

            // We're going to use explicit docker commands to deploy the Elasticsearch cluster
            // log storage containers.
            //
            // We're mounting three volumes to the container:
            //
            //      /etc/neon/host-env         - Generic host specific environment variables
            //      /etc/neon/env-log-esdata   - Elasticsearch node host specific environment variables
            //      neon-log-esdata-#          - Persistent Elasticsearch data folder

            var esBootstrapNodes = new StringBuilder();

            foreach (var esMasterNode in managerEsNodes)
            {
                esBootstrapNodes.AppendWithSeparator($"{esMasterNode.PrivateAddress}:{HiveHostPorts.LogEsDataTcp}", ",");
            }

            // Create a data volume for each Elasticsearch node and then start the node container.

            for (int i = 0; i < esNodes.Count; i++)
            {
                var esNode        = esNodes[i];
                var containerName = $"neon-log-esdata";
                var isMaster      = managerEsNodes.Contains(esNode) ? "true" : "false";
                var volumeCommand = CommandStep.CreateSudo(esNode.Name, "docker-volume-create", containerName);

                steps.Add(volumeCommand);

                ServiceHelper.AddContainerStartSteps(hive, steps, esNode, containerName, hive.Definition.Image.Elasticsearch,
                    new CommandBundle(
                        "docker run",
                        "--name", containerName,
                        "--detach",
                        "--restart", "always",
                        "--volume", "/etc/neon/host-env:/etc/neon/host-env:ro",
                        "--volume", $"{containerName}:/mnt/esdata",
                        "--env", $"ELASTICSEARCH_CLUSTER={hive.Definition.Datacenter}.{hive.Definition.Name}.neon-log-esdata",
                        "--env", $"ELASTICSEARCH_NODE_MASTER={isMaster}",
                        "--env", $"ELASTICSEARCH_NODE_DATA=true",
                        "--env", $"ELASTICSEARCH_NODE_COUNT={esNodes.Count}",
                        "--env", $"ELASTICSEARCH_HTTP_PORT={HiveHostPorts.LogEsDataHttp}",
                        "--env", $"ELASTICSEARCH_TCP_PORT={HiveHostPorts.LogEsDataTcp}",
                        "--env", $"ELASTICSEARCH_QUORUM={quorumCount}",
                        "--env", $"ELASTICSEARCH_BOOTSTRAP_NODES={esBootstrapNodes}",
                        "--env", $"ES_JAVA_OPTS=-XX:+UnlockExperimentalVMOptions -XX:+UseCGroupMemoryLimitForHeap",
                        "--memory", $"{esContainerRam / NeonHelper.Mega}M",
                        "--memory-reservation", $"{esContainerRam / NeonHelper.Mega}M",
                        "--memory-swappiness", "0",
                        "--network", "host",
                        "--log-driver", "json-file",        // Ensure that we don't log to the pipeline to avoid cascading events.
                        ServiceHelper.ImagePlaceholderArg));
            }

            // Configure a private hive proxy route to the Elasticsearch nodes.

            steps.Add(ActionStep.Create(hive.FirstManager.Name, "setup/elasticsearch-lbrule",
                node =>
                {
                    var rule = new TrafficHttpRule()
                    {
                        Name     = "neon-log-esdata",
                        System   = true,
                        Log      = false,   // This is important: we don't want to SPAM the log database with its own traffic.
                        Resolver = null
                    };

                    rule.Frontends.Add(
                        new TrafficHttpFrontend()
                        {
                            ProxyPort = HiveHostPorts.ProxyPrivateHttpLogEsData
                        });

                    foreach (var esNode in esNodes)
                    {
                        rule.Backends.Add(
                            new TrafficHttpBackend()
                            {
                                Server = esNode.Metadata.PrivateAddress.ToString(),
                                Port = HiveHostPorts.LogEsDataHttp
                            });
                    }

                    hive.PrivateTraffic.SetRule(rule);
                }));

            // Wait for the elasticsearch cluster to become ready and then save the
            // [logstash-*] template.  We need to do this before [neon-log-collector]
            // is started so we'll be sure that no indexes will be created before
            // we have a chance to persist the pattern.
            //
            // This works because [neon-log-collector] is the main service responsible
            // for persisting events to this index.

            steps.Add(ActionStep.Create(hive.FirstManager.Name, operationName: null,
                node =>
                {
                    node.Status = "wait for elasticsearch cluster";

                    using (var jsonClient = new JsonClient())
                    {
                        var baseLogEsDataUri = hive.Definition.LogEsDataUri;
                        var timeout          = TimeSpan.FromMinutes(5);
                        var timeoutTime      = DateTime.UtcNow + timeout;
                        var esNodeCount      = hive.Definition.Nodes.Count(n => n.Labels.LogEsData);

                        // Wait for the Elasticsearch cluster.

                        jsonClient.UnsafeRetryPolicy = NoRetryPolicy.Instance;

                        while (true)
                        {
                            try
                            {
                                var response = jsonClient.GetUnsafeAsync($"{baseLogEsDataUri}/_cluster/health").Result;

                                if (response.IsSuccess)
                                {
                                    var clusterStatus = response.AsDynamic();
                                    var status        = (string)(clusterStatus.status);

                                    status      = status.ToUpperInvariant();
                                    node.Status = $"wait for [neon-log-esdata] cluster: [status={status}] [{clusterStatus.number_of_nodes}/{esNodeCount} nodes ready])";

                                    // $todo(jeff.lill):
                                    //
                                    // We're accepting YELLOW status here due to this issue:
                                    //
                                    //      https://github.com/jefflill/NeonForge/issues/257

                                    if ((status == "GREEN" || status == "YELLOW") && clusterStatus.number_of_nodes == esNodeCount)
                                    {
                                        node.Status = "elasticsearch cluster is ready";
                                        break;
                                    }
                                }
                            }
                            catch
                            {
                                if (DateTime.UtcNow >= timeoutTime)
                                {
                                    node.Fault($"[neon-log-esdata] cluster not ready after waiting [{timeout}].");
                                    return;
                                }
                            }

                            Thread.Sleep(TimeSpan.FromSeconds(1));
                        }

                        // Save the [logstash-*]  template pattern.

                        var templatePattern = ResourceFiles.Root.GetFolder("Elasticsearch").GetFile("logstash-template.json").Contents;

                        jsonClient.PutAsync($"{baseLogEsDataUri}/_template/logstash-*", templatePattern).Wait();
                    }
                }));
        }

        /// <summary>
        /// Adds the steps required to configure the Kibana Elasticsearch/logging user interface.
        /// </summary>
        /// <param name="steps">The configuration step list.</param>
        private void AddKibanaSteps(ConfigStepList steps)
        {
            // This is super simple: All we need to do is to launch the Kibana 
            // service on the hive managers.

            ServiceHelper.AddServiceStartSteps(hive, steps, "neon-log-kibana", hive.Definition.Image.Kibana,
                new CommandBundle(
                    "docker service create",
                    "--name", "neon-log-kibana",
                    "--detach=false",
                    "--mode", "global",
                    "--endpoint-mode", "vip",
                    "--restart-delay", hive.Definition.Docker.RestartDelay,
                    "--network", HiveConst.PrivateNetwork,
                    "--constraint", $"node.role==manager",
                    "--publish", $"{HiveHostPorts.Kibana}:{NetworkPorts.Kibana}",
                    "--mount", "type=bind,source=/etc/neon/host-env,destination=/etc/neon/host-env,readonly=true",
                    "--env", $"ELASTICSEARCH_URL={hive.Definition.LogEsDataUri}",
                    "--log-driver", "json-file",    // Ensure that we don't log to the pipeline to avoid cascading events.
                    ServiceHelper.ImagePlaceholderArg));
        }

        /// <summary>
        /// Adds the steps required to configure the hive log collector which aggregates log events received
        /// from all hive nodes via their [neon-log-host] containers.
        /// </summary>
        /// <param name="steps">The configuration step list.</param>
        private void AddCollectorSteps(ConfigStepList steps)
        {
            // Add the steps to create the service.

            ServiceHelper.AddServiceStartSteps(hive, steps, "neon-log-collector", hive.Definition.Image.LogCollector,
                new CommandBundle(
                    "docker service create",
                    "--name", "neon-log-collector",
                    "--detach=false",
                    "--mode", "global",
                    "--restart-delay", hive.Definition.Docker.RestartDelay,
                    "--endpoint-mode", "vip",
                    "--network", $"{HiveConst.PrivateNetwork}",
                    "--constraint", $"node.role==manager",
                    "--mount", "type=bind,source=/etc/neon/host-env,destination=/etc/neon/host-env,readonly=true",
                    "--log-driver", "json-file",    // Ensure that we don't log to the pipeline to avoid cascading events.
                    ServiceHelper.ImagePlaceholderArg));

            // Deploy the [neon-log-collector] traffic manager rule.

            steps.Add(ActionStep.Create(hive.FirstManager.Name, "setup/neon-log-collection-lbrule",
                node =>
                {
                    node.Status = "set neon-log-collector traffic manager rule";

                    // Configure a private hive proxy TCP route so the [neon-log-host] containers
                    // will be able to reach the collectors.

                    var rule = new TrafficTcpRule()
                    {
                        Name   = "neon-log-collector",
                        System = true,
                        Log    = false    // This is important: we don't want to SPAM the log database with its own traffic.
                    };

                    rule.Frontends.Add(
                        new TrafficTcpFrontend()
                        {
                            ProxyPort = HiveHostPorts.ProxyPrivateTcpLogCollector
                        });

                    rule.Backends.Add(
                        new TrafficTcpBackend()
                        {
                            Server = "neon-log-collector",
                            Port   = NetworkPorts.TDAgentForward
                        });

                    hive.PrivateTraffic.SetRule(rule);
                }));
        }

        /// <summary>
        /// Deploys the log related containers to a node.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        public void DeployContainers(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            Thread.Sleep(stepDelay);

            ServiceHelper.StartContainer(node, "neon-log-host", hive.Definition.Image.LogHost, RunOptions.FaultOnError,
                new CommandBundle(
                    "docker run",
                    "--name", "neon-log-host",
                    "--detach",
                    "--restart", "always",
                    "--volume", "/etc/neon/host-env:/etc/neon/host-env:ro",
                    "--volume", "/var/log:/hostfs/var/log",
                    "--network", "host",
                    "--log-driver", "json-file",        // Ensure that we don't log to the pipeline to avoid cascading events.
                    ServiceHelper.ImagePlaceholderArg));

            ServiceHelper.StartContainer(node, "neon-log-metricbeat", hive.Definition.Image.Metricbeat, RunOptions.FaultOnError,
                new CommandBundle(
                    "docker run",
                    "--name", "neon-log-metricbeat",
                    "--detach",
                    "--net", "host",
                    "--restart", "always",
                    "--mount", "type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock",
                    "--volume", "/etc/neon/host-env:/etc/neon/host-env:ro",
                    "--volume", "/proc:/hostfs/proc:ro",
                    "--volume", "/:/hostfs:ro",
                    "--env", $"ELASTICSEARCH_URL={hive.Definition.LogEsDataUri}",
                    "--log-driver", "json-file",        // Ensure that we don't log to the pipeline to avoid cascading events.
                    ServiceHelper.ImagePlaceholderArg));
        }
    }
}
