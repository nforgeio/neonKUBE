//-----------------------------------------------------------------------------
// FILE:	    ActionStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Runs an <see cref="Action{SshProxy}"/> as a cluster setup step.
    /// </summary>
    public class ActionStep : ConfigStep
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates a configuration step that executes an potentially idempotent <see cref="Action"/>
        /// on a specific cluster node.
        /// </summary>
        /// <param name="nodeName">The node name.</param>
        /// <param name="operationName">The idempotent operation name or <c>null</c> if the operation is not idempotent.</param>
        /// <param name="action">The action to be invoked.</param>
        /// <returns>The <see cref="ActionStep"/>.</returns>
        public static ActionStep Create(string nodeName, string operationName, Action<LinuxSshProxy<NodeDefinition>> action)
        {
            return new ActionStep(nodeName, operationName, action);
        }

        //---------------------------------------------------------------------
        // Instance members

        private string                              nodeName;
        private string                              operationName;
        private Action<LinuxSshProxy<NodeDefinition>>    action;

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="nodeName">The node name.</param>
        /// <param name="operationName">The idempotent operation name or <c>null</c> if the operation is not idempotent.</param>
        /// <param name="action">The action to be invoked.</param>
        private ActionStep(string nodeName, string operationName, Action<LinuxSshProxy<NodeDefinition>> action)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName), nameof(nodeName));
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            this.nodeName      = nodeName;
            this.operationName = operationName;
            this.action        = action;
        }

        /// <inheritdoc/>
        public override void Run(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            var node = cluster.GetNode(nodeName);

            if (operationName != null)
            {
                node.InvokeIdempotentAction(operationName, () => action(node));
            }
            else
            {
                action(node);
            }
        }
    }
}
