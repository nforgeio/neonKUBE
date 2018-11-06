//-----------------------------------------------------------------------------
// FILE:	    Update-18.10.0-alpha.4_18.10.1-alpha.4.cs
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
    /// Updates a hive from version <b>18.10.0-alpha.4</b> to <b>18.10.1-alpha.4</b>.
    /// </summary>
    [HiveUpdate]
    public class Update_1810_0_alpha_4_1810_1_alpha_4 : HiveUpdate
    {
        /// <inheritdoc/>
        public override SemanticVersion FromVersion { get; protected set; } = SemanticVersion.Parse("18.10.0-alpha.4");

        /// <inheritdoc/>
        public override SemanticVersion ToVersion { get; protected set; } = SemanticVersion.Parse("18.11.0-alpha.5");

        /// <inheritdoc/>
        public override bool RestartRequired => false;

        /// <inheritdoc/>
        public override void AddUpdateSteps(SetupController<NodeDefinition> controller)
        {
            base.Initialize(controller);

            controller.AddStep(GetStepLabel("remove docker python module"), (node, stepDelay) => RemoveDockerPython(node));
            controller.AddGlobalStep(GetStepLabel("make neon-registry load balancer rule private"), () => PrivateRegistryRule());
        }

        /// <summary>
        /// Removes the Docker python module from all nodes because it conflicts with
        /// Docker related Ansible scripts.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void RemoveDockerPython(SshProxy<NodeDefinition> node)
        {
            node.SudoCommand("pip uninstall docker", RunOptions.LogOnErrorOnly);
        }

        /// <summary>
        /// Marks the <b>neon-registry</b> load balancer rule as a system rule if the rule exists.
        /// </summary>
        private void PrivateRegistryRule()
        {
            var rule = Hive.PrivateLoadBalancer.GetRule("neon-registry");

            if (rule != null && !rule.System)
            {
                rule.System = true;
                Hive.PrivateLoadBalancer.SetRule(rule);
            }
        }
    }
}
