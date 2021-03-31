//-----------------------------------------------------------------------------
// FILE:	    SetupNodeStatus.cs
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
    /// Describes the current state of a node during cluster setup.
    /// </summary>
    public class SetupNodeStatus
    {
        private string      status;
        private bool        isReady;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The node name.</param>
        /// <param name="metadata">The node metadata.</param>
        internal SetupNodeStatus(string name, object metadata)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(metadata != null, nameof(metadata));

            this.Name      = name;
            this.Metadata  = metadata;
            this.isReady   = false;
            this.IsFaulted = false;
            this.status    = string.Empty;
        }

        /// <summary>
        /// Returns the node name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Indicates that the node has successfully  completed the current setup step.
        /// Note that this will always return <c>false</c> when the node is faulted.
        /// </summary>
        public bool IsReady
        {
            get => isReady && !IsFaulted;
            set => isReady = value;
        }

        /// <summary>
        /// Returns an indication that a setup step failed on the node.
        /// </summary>
        public bool IsFaulted { get; internal set; }

        /// <summary>
        /// Returns the node status.
        /// </summary>
        public string Status
        {
            get { return status; }
            set { status = value ?? string.Empty; }
        }

        /// <summary>
        /// Returns the node metadata as an object.  The actual type can be determined
        /// by examining <see cref="ISetupController.NodeMetadataType"/>.
        /// </summary>
        [JsonIgnore]
        public object Metadata { get; internal set; }

        /// <summary>
        /// Copies the state properties of this instance to another.
        /// </summary>
        /// <param name="target">The target instance.</param>
        internal void CopyTo(SetupNodeStatus target)
        {
            target.isReady   = this.isReady;
            target.IsFaulted = this.IsFaulted;
            target.status    = this.Status;
        }
    }
}
