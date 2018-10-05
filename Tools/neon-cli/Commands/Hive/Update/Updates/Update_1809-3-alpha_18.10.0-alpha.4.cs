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

using ICSharpCode.SharpZipLib.Zip;

using Neon.Common;
using Neon.IO;
using Neon.Hive;
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

                        var orgScript   = node.DownloadText(scriptPath);
                        var sbNewScript = new StringBuilder();

                        foreach (var line in new StringReader(orgScript).Lines())
                        {
                            if (line.Contains("ES_JAVA_OPTS="))
                            {
                                sbNewScript.AppendLine("    --env \"ES_JAVA_OPTS=-XX:+UnlockExperimentalVMOptions -XX:+UseCGroupMemoryLimitForHeap\" \\");
                            }
                            else
                            {
                                sbNewScript.AppendLine(line);
                            }
                        }

                        node.UploadText(scriptPath, sbNewScript.ToString(), permissions: "");

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
    }
}
