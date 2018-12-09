//-----------------------------------------------------------------------------
// FILE:	    Update_1809-3-alpha_18.10.0-alpha.4.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using ICSharpCode.SharpZipLib.Zip;

using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.HiveMQ;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Updates a hive from version <b>18.9.3-alpha</b> to <b>18.10.0-alpha.4</b>.
    /// </summary>
    [HiveUpdate]
    public class Update_1809_3_alpha_1809_4_alpha : HiveUpdate
    {
        /// <inheritdoc/>
        public override SemanticVersion FromVersion { get; protected set; } = SemanticVersion.Parse("18.9.3-alpha");

        /// <inheritdoc/>
        public override SemanticVersion ToVersion { get; protected set; } = SemanticVersion.Parse("18.10.0-alpha.4");

        /// <inheritdoc/>
        public override bool RestartRequired => true;

        /// <inheritdoc/>
        public override void AddUpdateSteps(SetupController<NodeDefinition> controller)
        {
            base.Initialize(controller);

            controller.AddStep(GetStepLabel("elasticsearch"), (node, stepDelay) => UpdateElasticsearch(node));
            controller.AddStep(GetStepLabel("ceph-fuse"), (node, stepDelay) => UpdateCephFuse(node));

            // $todo(jeff.lill):
            //
            // Update these component scripts to remove this secret: neon-hivemq-neon
            //
            //      neon-hive-manager.sh
            //      neon-proxy-manager.sh
            //      neon-proxy-public.sh
            //      neon-proxy-private.sh
            //
            // Remove these Docker secrets after updating services:
            //
            //      neon-hivemq-neon
            //      neon-hivemq-sysadmin
            //      neon-hivemq-app
            //
            // We also need to reconfigure the AMPQ private [neon-hivemq-ampq] traffic manager rule 
            // as TCP because older builds incorrectly configured this as an HTTP proxy.

            controller.AddGlobalStep(GetStepLabel("hivemq-settings"), () => UpdateHiveMQSettings());
            controller.AddGlobalStep(GetStepLabel("hivemq cluster name"), () => UpdateHiveMQClusterName());
            controller.AddGlobalStep(GetStepLabel("rename log-retention-days"), () => UpdateLogRetentionDays());
            controller.AddGlobalStep(GetStepLabel("proxy cache services"), () => UpdateProxyCacheServices());

            controller.AddStep(GetStepLabel("edit proxy bridge scripts"), (node, stepDelay) => UpdateProxyBridgeScripts(node));
        }

        /// <summary>
        /// Update the Elasticsearch container launch scripts to enable automatic
        /// memory settings based on any cgroup limits.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void UpdateElasticsearch(SshProxy<NodeDefinition> node)
        {
            // This method is called for all cluster nodes, even those
            // that aren't currently hosting Elasticsearch, so we can
            // update any scripts that may have been orphaned (for
            // consistency).
            //
            // The update consists of replacing the script line that
            // sets the [ES_JAVA_OPTS] environment variable with:
            //
            //      --env ES_JAVA_OPTS=-XX:+UnlockExperimentalVMOptions -XX:+UseCGroupMemoryLimitForHeap \
            //
            // To ensure that this feature is enabled in favor of the 
            // old hacked memory level settings.

            var scriptPath = LinuxPath.Combine(HiveHostFolders.Scripts, "neon-log-esdata.sh");

            node.InvokeIdempotentAction(GetIdempotentTag("neon-log-esdata"),
                () =>
                {
                    if (node.FileExists(scriptPath))
                    {
                        node.Status = $"edit: {scriptPath}";

                        var orgScript = node.DownloadText(scriptPath);
                        var newScript = new StringBuilder();

                        foreach (var line in new StringReader(orgScript).Lines())
                        {
                            if (line.Contains("ES_JAVA_OPTS="))
                            {
                                newScript.AppendLine("    --env \"ES_JAVA_OPTS=-XX:+UnlockExperimentalVMOptions -XX:+UseCGroupMemoryLimitForHeap\" \\");
                            }
                            else
                            {
                                newScript.AppendLine(line);
                            }
                        }

                        node.UploadText(scriptPath, newScript.ToString(), permissions: "");

                        node.Status = string.Empty;
                    }
                });
        }

        /// <summary>
        /// Updates the <b>/etc/systemd/system/ceph-fuse-hivefs.service</b> to adjust restart
        /// behavior: https://github.com/jefflill/NeonForge/issues/364
        /// </summary>
        /// <param name="node">The target node.</param>
        private void UpdateCephFuse(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction(GetIdempotentTag("ceph-fuse"),
                () =>
                {
                    node.UploadText("/etc/systemd/system/ceph-fuse-hivefs.service",
@"[Unit]
Description=Ceph FUSE client (for /mnt/hivefs)
After=network-online.target local-fs.target time-sync.target
Wants=network-online.target local-fs.target time-sync.target
Conflicts=umount.target
PartOf=ceph-fuse.target

[Service]
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph
ExecStart=/usr/bin/ceph-fuse -f -o nonempty --cluster ${CLUSTER} /mnt/hivefs
TasksMax=infinity

# These settings configure the service to restart always after
# waiting 5 seconds between attempts for up to a 365 days (effectively 
# forever).  [StartLimitIntervalSec] is set to the number of seconds 
# in a year and [StartLimitBurst] is set to the number of 5 second 
# intervals in [StartLimitIntervalSec].

Restart=always
RestartSec=5
StartLimitIntervalSec=31536000 
StartLimitBurst=6307200

[Install]
WantedBy=ceph-fuse.target
WantedBy=docker.service
",
                    permissions: "644");

                    // Tell systemd to regenerate its configuration.

                    node.SudoCommand("systemctl daemon-reload");
                });
        }

        /// <summary>
        /// <para>
        /// This release relocated the HiveMQ account settings from Docker secrets
        /// to hive global Consul keys so that these can be available to containers
        /// too (e.g. running on pets) and also to make deploying services that use
        /// queuing more transparent.
        /// </para>
        /// <para>
        /// This update copies these settings from the Docker secrets to Consul and
        /// then removes these secret references from the built-in hive service
        /// scripts and also updates the services by pulling the latest images and
        /// removing the secrets there too.
        /// </para>
        /// <note>
        /// We're going to leave the old Docker secrets in place on the off chance
        /// that a user service is still referencing them.
        /// </note>
        /// </summary>
        private void UpdateHiveMQSettings()
        {
            var firstManager = Hive.FirstManager;

            // Fetch the current HiveMQ settings from the Docker secrets.
            // This is slow, so we'll capture these in parallel.

            firstManager.Status = "reading hivemq secrets";

            var appSettings = (HiveMQSettings)null;
            var neonSettings = (HiveMQSettings)null;
            var sysadminSettings = (HiveMQSettings)null;

            NeonHelper.WaitForParallel(
                new Action[]
                {
                    new Action(() => appSettings      = Hive.Docker.Secret.Get<HiveMQSettings>("neon-hivemq-settings-app")),
                    new Action(() => neonSettings     = Hive.Docker.Secret.Get<HiveMQSettings>("neon-hivemq-settings-neon")),
                    new Action(() => sysadminSettings = Hive.Docker.Secret.Get<HiveMQSettings>("neon-hivemq-settings-sysadmin")),
                },
                TimeSpan.FromSeconds(120));

            firstManager.Status = string.Empty;

            // Edit the service start scripts by removing any lines that 
            // attach a HiveMQ account secret.

            var services = new string[]
                {
                    "neon-hive-manager",
                    "neon-proxy-manager",
                    "neon-proxy-public",
                    "neon-proxy-private"
                };

            foreach (var manager in Hive.Managers)
            {
                foreach (var service in services)
                {
                    var scriptPath = LinuxPath.Combine(HiveHostFolders.Scripts, $"{service}.sh");

                    manager.Status = $"edit: {scriptPath}";

                    // Edit the service start scripts by removing any lines that 
                    // attach a HiveMQ account secret.

                    var curScript = manager.DownloadText(scriptPath);
                    var newScript = new StringBuilder();

                    using (var reader = new StringReader(curScript))
                    {
                        foreach (var line in reader.Lines())
                        {
                            if (!line.Trim().StartsWith("--secret neon-hivemq-settings-"))
                            {
                                newScript.AppendLine(line);
                            }
                        }
                    }

                    manager.UploadText(scriptPath, newScript.ToString(), permissions: "660");

                    manager.Status = string.Empty;
                }
            }

            // Update the impacted services by removing the secret and pulling the latest image.

            firstManager.Status = "neon-hive-manager";
            firstManager.DockerCommand(RunOptions.FaultOnError, $"docker service update --secret-rm=neon-hivemq-settings-neon --image {Program.ResolveDockerImage(Hive.Definition.Image.HiveManager)} neon-hive-manager");
            firstManager.Status = "neon-proxy-manager";
            firstManager.DockerCommand(RunOptions.FaultOnError, $"docker service update --secret-rm=neon-hivemq-settings-neon --image {Program.ResolveDockerImage(Hive.Definition.Image.ProxyManager)} neon-proxy-manager");
            firstManager.Status = "neon-proxy-public";
            firstManager.DockerCommand(RunOptions.FaultOnError, $"docker service update --secret-rm=neon-hivemq-settings-neon --image {Program.ResolveDockerImage(Hive.Definition.Image.Proxy)} neon-proxy-public");
            firstManager.Status = "neon-proxy-private";
            firstManager.DockerCommand(RunOptions.FaultOnError, $"docker service update --secret-rm=neon-hivemq-settings-neon --image {Program.ResolveDockerImage(Hive.Definition.Image.Proxy)} neon-proxy-private");
            firstManager.Status = string.Empty;

            // Redeploy the [neon-hivemq-ampq] private traffic manager to be a TCP rather than an HTTP proxy.

            var ampqRule = new TrafficTcpRule()
            {
                Name = "neon-hivemq-ampq",
                System = true,
                Resolver = null
            };

            ampqRule.Frontends.Add(
                new TrafficTcpFrontend()
                {
                    ProxyPort = HiveHostPorts.ProxyPrivateHiveMQAMQP
                });

            ampqRule.Backends.Add(
                new TrafficTcpBackend()
                {
                    Group = HiveHostGroups.HiveMQ,
                    GroupLimit = 5,
                    Port = HiveHostPorts.HiveMQAMQP
                });

            Hive.PrivateTraffic.SetRule(ampqRule);
        }

        /// <summary>
        /// Older builds didn't configure the HiveMQ cluster name so it defaulted
        /// to the name of the first RabbitMQ container name that formed the cluster.
        /// This was not super useful so we're going to explicity use the hive name.
        /// </summary>
        private void UpdateHiveMQClusterName()
        {
            var hiveMQNode = Hive.Nodes
                .Where(n => n.Metadata.Labels.HiveMQ)
                .OrderBy(n => n.Name)
                .First();

            hiveMQNode.InvokeIdempotentAction(GetIdempotentTag("hivemq-cluster-name"),
                () =>
                {
                    hiveMQNode.SudoCommand($"docker exec neon-hivemq rabbitmqctl set_cluster_name {Hive.Definition.Name}", RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Older builds misspelled the <b>neon/global/log-rentention-days</b> variable.
        /// This method copies this to <b>log-retention-days</b> but leaves the old 
        /// variable around for backwards compatability.
        /// </summary>
        private void UpdateLogRetentionDays()
        {
            var consul = Hive.Consul.Client;

            // Create the correct variable if it doesn't already exist.

            var oldPath = Hive.Globals.GetKey("log-rentention-days");
            var newPath = Hive.Globals.GetKey(HiveGlobals.UserLogRetentionDays);

            if (!consul.KV.Exists(newPath).Result)
            {
                // Retrieve the original value (or default to 14 days).

                var value = consul.KV.GetString(oldPath).Result;

                if (value == null)
                {
                    value = "14";
                }

                // Save the new variable.

                consul.KV.PutString(newPath, value).Wait();
            }
        }

        /// <summary>
        /// Deploys the <b>neon-proxy-public-cache</b> and <b>neon-proxy-private-cache</b>
        /// services if they're not already running.
        /// </summary>
        private void UpdateProxyCacheServices()
        {
            var firstManager = Hive.FirstManager;

            var response = firstManager.SudoCommand("docker service inspect neon-proxy-public-cache", RunOptions.None);

            if (response.ExitCode != 0)
            {
                // Deploy: neon-proxy-public-cache

                var publicCacheConstraintArgs = new List<string>();
                var publicCacheReplicaArgs    = new List<string>();

                if (Hive.Definition.Proxy.PublicCacheReplicas >= Hive.Definition.Workers.Count())
                {
                    publicCacheConstraintArgs.Add("--constraint");
                    publicCacheConstraintArgs.Add("node.role==worker");
                }

                publicCacheReplicaArgs.Add("--replicas");
                publicCacheReplicaArgs.Add($"{Hive.Definition.Proxy.PublicCacheReplicas}");

                ServiceHelper.StartService(Hive, "neon-proxy-public-cache", Hive.Definition.Image.ProxyCache,
                    new CommandBundle(
                        "docker service create",
                        "--name", "neon-proxy-public-cache",
                        "--detach=false",
                        "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                        "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                        "--mount", "type=tmpfs,dst=/var/lib/varnish/_.vsm_mgt,tmpfs-size=90M,tmpfs-mode=755",
                        "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/public/proxy-conf",
                        "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/public/proxy-hash",
                        "--env", "WARN_SECONDS=300",
                        "--env", $"MEMORY-LIMIT={Hive.Definition.Proxy.PublicCacheSize}",
                        "--env", "LOG_LEVEL=INFO",
                        "--env", "DEBUG=false",
                        "--secret", "neon-proxy-public-credentials",
                        publicCacheConstraintArgs,
                        publicCacheReplicaArgs,
                        "--restart-delay", Hive.Definition.Docker.RestartDelay,
                        "--network", HiveConst.PublicNetwork,
                        ServiceHelper.ImagePlaceholderArg));
            }

            response = firstManager.SudoCommand("docker service inspect neon-proxy-private-cache", RunOptions.None);

            if (response.ExitCode != 0)
            {
                var privateCacheConstraintArgs = new List<string>();
                var privateCacheReplicaArgs    = new List<string>();

                if (Hive.Definition.Proxy.PrivateCacheReplicas >= Hive.Definition.Workers.Count())
                {
                    privateCacheConstraintArgs.Add("--constraint");
                    privateCacheConstraintArgs.Add("node.role==worker");
                }

                privateCacheReplicaArgs.Add("--replicas");
                privateCacheReplicaArgs.Add($"{Hive.Definition.Proxy.PrivateCacheReplicas}");

                ServiceHelper.StartService(Hive, "neon-proxy-private-cache", Hive.Definition.Image.ProxyCache,
                    new CommandBundle(
                        "docker service create",
                        "--name", "neon-proxy-private-cache",
                        "--detach=false",
                        "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                        "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                        "--mount", "type=tmpfs,dst=/var/lib/varnish/_.vsm_mgt,tmpfs-size=90M,tmpfs-mode=755",
                        "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/private/proxy-conf",
                        "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/private/proxy-hash",
                        "--env", "WARN_SECONDS=300",
                        "--env", $"MEMORY-LIMIT={Hive.Definition.Proxy.PrivateCacheSize}",
                        "--env", "LOG_LEVEL=INFO",
                        "--env", "DEBUG=false",
                        "--secret", "neon-proxy-private-credentials",
                        privateCacheConstraintArgs,
                        privateCacheReplicaArgs,
                        "--restart-delay", Hive.Definition.Docker.RestartDelay,
                        "--network", HiveConst.PrivateNetwork,
                        ServiceHelper.ImagePlaceholderArg));
            }
        }

        /// <summary>
        /// Edits the [neon-proxy-public-bridge.sh] and [neon-proxy-private-bridge.sh]
        /// scripts to remove the [VAULT_CREDENTIALS] environment variable so the new
        /// .NET based proxy bridge image will work properly.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void UpdateProxyBridgeScripts(SshProxy<NodeDefinition> node)
        {
            var scriptNames =
                new string[]
                {
                    "neon-proxy-public-bridge.sh",
                    "neon-proxy-private-bridge.sh"
                };

            foreach (var scriptName in scriptNames)
            {
                var scriptPath = LinuxPath.Combine(HiveHostFolders.Scripts, scriptName);
                var scriptText = node.DownloadText(scriptName);
                var sbEdited   = new StringBuilder();

                using (var reader = new StringReader(scriptText))
                {
                    foreach (var line in reader.Lines())
                    {
                        if (!line.Contains("--env VAULT_CREDENTIALS="))
                        {
                            sbEdited.AppendLineLinux(line);
                        }
                    }
                }

                node.UploadText(scriptPath, sbEdited.ToString(), permissions: "700");
            }
        }
    }
}
