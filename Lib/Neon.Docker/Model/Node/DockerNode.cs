//-----------------------------------------------------------------------------
// FILE:	    DockerNode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Docker
{
    /// <summary>
    /// Describes a cluster node.
    /// </summary>
    public class DockerNode
    {
        /// <summary>
        /// Constructs an instance from the dynamic node information returned by
        /// the Docker engine.
        /// </summary>
        /// <param name="source">The dynamic source value.</param>
        internal DockerNode(dynamic source)
        {
            this.Inner         = source;
            this.ID            = source.ID;
            this.CreatedAt     = source.CreatedAt;
            this.UpdatedAt     = source.UpdatedAt;

            this.Role          = source.Spec.Role;
            this.Availability  = source.Spec.Availability;

            this.Hostname      = source.Description.Hostname;
            this.Architecture  = source.Description.Platform.Architecture;
            this.OS            = source.Description.Platform.OS;
            this.NanoCPUs      = source.Description.Resources.NanoCPUs;
            this.MemoryBytes   = source.Description.Resources.MemoryBytes;
            this.EngineVersion = source.Description.Engine.EngineVersion;

            this.State         = source.Status.State;
            this.Addr          = source.Status.Addr;

            this.Labels = new Dictionary<string, string>();

            if (source.Spec.Labels != null)
            {
                var labelsObject = (JObject)source.Spec.Labels;

                foreach (var property in labelsObject.Properties())
                {
                    this.Labels[property.Name] = (string)property.Value;
                }
            }

            if (source.ManagerStatus != null)
            {
                this.ManagerStatus = new DockerNodeManagerStatus(source.ManagerStatus);
            }
        }

        /// <summary>
        /// Returns the raw <v>dynamic</v> object actually returned by Docker.
        /// You may use this to access newer Docker properties that have not
        /// yet been wrapped by this class.
        /// </summary>
        public dynamic Inner { get; private set; }

        /// <summary>
        /// Returns the node ID.
        /// </summary>
        public string ID { get; private set; }

        /// <summary>
        /// Returns the time the node was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; private set; }

        /// <summary>
        /// Returns the time the node was updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; private set; }

        /// <summary>
        /// Returns the node role (currently one of <b>"control-plane"</b> or <b>"worker"</b>).
        /// </summary>
        public string Role { get; private set; }

        /// <summary>
        /// Returns the node availability.
        /// </summary>
        public string Availability { get; private set; }

        /// <summary>
        /// Returns the node labels.
        /// </summary>
        public Dictionary<string, string> Labels { get; private set; }

        /// <summary>
        /// Returns the node hostname.
        /// </summary>
        public string Hostname { get; private set; }

        /// <summary>
        /// Returns the node CPU architecture.
        /// </summary>
        public string Architecture { get; private set; }

        /// <summary>
        /// Returns the node operating system.
        /// </summary>
        public string OS { get; private set; }

        /// <summary>
        /// Returns the available CPU capacity, 
        /// </summary>
        public long NanoCPUs { get; private set; }

        /// <summary>
        /// Returns the bytes of available memory.
        /// </summary>
        public long MemoryBytes { get; private set; }

        /// <summary>
        /// Returns the Docker engine version.
        /// </summary>
        public string EngineVersion { get; private set; }

        /// <summary>
        /// Returns the node state.
        /// </summary>
        public string State { get; private set; }

        /// <summary>
        /// Returns the node IP address.
        /// </summary>
        public string Addr { get; private set; }

        /// <summary>
        /// Returns management status for cluster control-plane nodes.
        /// </summary>
        public DockerNodeManagerStatus ManagerStatus { get; private set; }
    }
}
