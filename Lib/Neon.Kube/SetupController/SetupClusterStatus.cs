//-----------------------------------------------------------------------------
// FILE:	    SetupClusterStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

using Newtonsoft.Json;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Describes the current state of cluster setup.
    /// </summary>
    public partial class SetupClusterStatus
    {
        private object                              syncLock = new object();
        private ISetupController                    controller;
        private ClusterProxy                        cluster;
        private bool                                isFaulted;
        private Dictionary<string, SetupNodeStatus> nameToNodeState;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        internal SetupClusterStatus(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            this.controller      = controller;
            this.cluster         = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            this.GlobalStatus    = controller.GlobalStatus;
            this.Steps           = controller.GetStepStatus().ToList();
            this.CurrentStep     = Steps.SingleOrDefault(step => step.Number == controller.CurrentStepNumber);
            this.nameToNodeState = new Dictionary<string, SetupNodeStatus>(StringComparer.OrdinalIgnoreCase);

            foreach (var node in cluster.Nodes)
            {
                nameToNodeState.Add(node.Name, new SetupNodeStatus(node.Name, node.Status, node.NodeDefinition));
            }
        }

        /// <summary>
        /// Indicates whether cluster setup has failed.
        /// </summary>
        public bool IsFaulted
        {
            get
            {
                lock (syncLock)
                {
                    return isFaulted;
                }
            }

            set
            {
                lock (syncLock)
                {
                    isFaulted = value;
                }
            }
        }

        /// <summary>
        /// Returns the current node setup state.
        /// </summary>
        public IEnumerable<SetupNodeStatus> Nodes => nameToNodeState.Values;

        /// <summary>
        /// Returns information about the setup steps in order of execution. 
        /// </summary>
        public List<SetupStepStatus> Steps { get; private set; } = new List<SetupStepStatus>();

        /// <summary>
        /// Returns the currently executing step status (or <c>null</c>).
        /// </summary>
        public SetupStepStatus CurrentStep { get; private set; }

        /// <summary>
        /// Returns any status for the overall setup operation.
        /// </summary>
        public string GlobalStatus { get; private set; }

        /// <summary>
        /// Returns a copy of the instance.
        /// </summary>
        /// <returns>The copy.</returns>
        private SetupClusterStatus Clone()
        {
            var clone = new SetupClusterStatus(controller)
            {
                IsFaulted = this.IsFaulted
            };

            foreach (var node in this.nameToNodeState.Values)
            {
                node.CopyTo(nameToNodeState[node.Name]);
            }

            return clone;
        }
    }
}
