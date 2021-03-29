//-----------------------------------------------------------------------------
// FILE:	    ClusterSetupState.cs
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
    public class ClusterSetupState
    {
        // This can be changed to [Formatting.Indented] whil debugging.
        private const Formatting jsonFormatting = Formatting.None; 

        private object                              syncLock = new object();
        private ISetupController                    controller;
        private ClusterProxy                        cluster;
        private bool                                isFaulted;
        private Dictionary<string, NodeSetupState>  nameToNodeState;
        private string                              lastStateJson;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        internal ClusterSetupState(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            this.controller      = controller;
            this.cluster         = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            this.nameToNodeState = new Dictionary<string, NodeSetupState>(StringComparer.OrdinalIgnoreCase);

            foreach (var nodeDefinition in cluster.Definition.Nodes)
            {
                nameToNodeState.Add(nodeDefinition.Name, new NodeSetupState(nodeDefinition));
            }

            this.lastStateJson = null;
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
        public IEnumerable<NodeSetupState> Nodes => nameToNodeState.Values;

        /// <summary>
        /// Updates the cluster setup state from the related <see cref="ClusterProxy"/>.  This
        /// also detects whether the state has changed since the previous <see cref="Update"/>
        /// call and returns a clone of the new state on the change.
        /// </summary>
        /// <returns>
        /// A cloned copy of the <see cref="ClusterSetupState"/> when the state has changed 
        /// since the previous call.
        /// </returns>
        internal ClusterSetupState Update()
        {
            lock (syncLock)
            {
                foreach (var node in cluster.Nodes)
                {
                    var nodeState = nameToNodeState[node.Name];

                    nodeState.IsFaulted = node.IsFaulted;
                    node.IsReady        = node.IsReady;
                }

                // Detect whether the state has changed since the last call by serializing as
                // JSON and comparing against the previous state.

                if (lastStateJson == null)
                {
                    // This is the first call to [Update()] so we'll treat this as a change.

                    lastStateJson = NeonHelper.JsonSerialize(this, jsonFormatting);

                    return this.Clone();
                }
                else
                {
                    var newStateJson = NeonHelper.JsonSerialize(this, jsonFormatting);

                    if (newStateJson == lastStateJson)
                    {
                        lastStateJson = newStateJson;

                        return this.Clone();
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a copy of the instance.
        /// </summary>
        /// <returns>The copy.</returns>
        private ClusterSetupState Clone()
        {
            var clone = new ClusterSetupState(controller)
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
