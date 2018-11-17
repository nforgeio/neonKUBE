//-----------------------------------------------------------------------------
// FILE:	    Update-18.10.0-alpha.4_18.10.1-alpha.4.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.IO;
using System.Text;

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Updates a hive from version <b>18.10.0-alpha.4</b> to <b>18.10.1-alpha.4</b>.
    /// </summary>
    [HiveUpdate]
    public class Update_1810_0_alpha_4_1810_1_alpha_4 : HiveUpdate
    {
        /// <inheritdoc/>
        public override SemanticVersion FromVersion { get; protected set; } = SemanticVersion.Parse("18.10.0-alpha.4");

        /// <inheritdoc/>
        public override SemanticVersion ToVersion { get; protected set; } = SemanticVersion.Parse("18.10.1-alpha.4");

        /// <inheritdoc/>
        public override bool RestartRequired => false;

        /// <inheritdoc/>
        public override void AddUpdateSteps(SetupController<NodeDefinition> controller)
        {
            base.Initialize(controller);

            controller.AddGlobalStep(GetStepLabel("make neon-registry LB rule private"), () => PrivateRegistryRule());
            controller.AddStep(GetStepLabel("remove docker python module"), (node, stepDelay) => RemoveDockerPython(node));
            controller.AddStep(GetStepLabel("edit /etc/hosts"), (node, stepDelay) => EditEtcHosts(node));
        }

        /// <summary>
        /// Removes the Docker python module from all nodes because it conflicts with
        /// Docker related Ansible playbooks.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void RemoveDockerPython(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction(GetIdempotentTag("remove-docker-py"),
                () =>
                {
                    node.SudoCommand("su sysadmin -c 'pip uninstall -y docker'", RunOptions.LogOnErrorOnly);
                });
        }

        /// <summary>
        /// Marks the <b>neon-registry</b> traffic manager rule as a system rule if the rule exists.
        /// </summary>
        private void PrivateRegistryRule()
        {
            var rule = Hive.PrivateTraffic.GetRule("neon-registry");

            if (rule != null && !rule.System)
            {
                rule.System = true;
                Hive.PrivateTraffic.SetRule(rule);
            }
        }

        /// <summary>
        /// <para>
        /// Edits the [/etc/hosts] file on all hive nodes so that the line:
        /// </para>
        /// <code>
        /// 127.0.1.1   {hostname}
        /// </code>
        /// <para>
        /// is changed to:
        /// </para>
        /// <code>
        /// {node.PrivateAddress} {hostname}
        /// </code>
        /// <para>
        /// Hashicorp Vault cannot restart with the old setting, complaining about a
        /// <b>""missing API address</b>.
        /// </para>
        /// </summary>
        /// <param name="node">The target node.</param>
        private void EditEtcHosts(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction(GetIdempotentTag("edit-etc-hosts"),
                () =>
                {
                    var etcHosts   = node.DownloadText("/etc/hosts");
                    var sbEtcHosts = new StringBuilder();

                    using (var reader = new StringReader(etcHosts))
                    {
                        foreach (var line in reader.Lines())
                        {
                            if (line.StartsWith("127.0.1.1"))
                            {
                                var nodeAddress = node.PrivateAddress.ToString();
                                var separator   = new string(' ', Math.Max(16 - nodeAddress.Length, 1));

                                sbEtcHosts.AppendLine($"{nodeAddress}{separator}{node.Name}");
                            }
                            else
                            {
                                sbEtcHosts.AppendLine(line);
                            }
                        }
                    }

                    node.UploadText("/etc/hosts", sbEtcHosts.ToString(), permissions: "644");
                    node.SudoCommand("systemctl restart vault");
                });
        }
    }
}
