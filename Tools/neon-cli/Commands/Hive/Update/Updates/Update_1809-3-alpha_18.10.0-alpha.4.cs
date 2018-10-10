//-----------------------------------------------------------------------------
// FILE:	    Update-1809-3-alpha_18.10.0-alpha.4.cs
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
            // We also need to reconfigure the AMPQ private [neon-hivemq-ampq] load balancer rule 
            // as TCP because older builds incorrectly configured this as an HTTP proxy.

            controller.AddGlobalStep(GetStepLabel("hivemq-settings"), () => UpdateHiveMQSettings());
            controller.AddGlobalStep(GetStepLabel("hivemq cluster name"), () => UpdateHiveMQClusterName());
            controller.AddGlobalStep(GetStepLabel("rename log-retention-days"), () => UpdateLogRetentionDays());
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

            var appSettings      = (HiveMQSettings)null;
            var neonSettings     = (HiveMQSettings)null;
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

            // Redeploy the [neon-hivemq-ampq] private load balancer to be a TCP rather than an HTTP proxy.

            var ampqRule = new LoadBalancerTcpRule()
            {
                Name     = "neon-hivemq-ampq",
                System   = true,
                Resolver = null
            };

            ampqRule.Frontends.Add(
                new LoadBalancerTcpFrontend()
                {
                    ProxyPort = HiveHostPorts.ProxyPrivateHiveMQAMPQ
                });

            ampqRule.Backends.Add(
                new LoadBalancerTcpBackend()
                {
                    Group      = HiveHostGroups.HiveMQ,
                    GroupLimit = 5,
                    Port       = HiveHostPorts.HiveMQAMPQ
                });

            Hive.PrivateLoadBalancer.SetRule(ampqRule);
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
    }
}
