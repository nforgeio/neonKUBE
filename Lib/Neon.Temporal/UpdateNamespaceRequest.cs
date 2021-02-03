//-----------------------------------------------------------------------------
// FILE:	    UpdateNamespaceRequest.cs
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
using System.ComponentModel;

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Newtonsoft.Json;

namespace Neon.Temporal
{
    /// <summary>
    /// Holds the changes to be made to a Temporal namespace.
    /// </summary>
    public class UpdateNamespaceRequest
    {
        /// <summary>
        /// The namespace name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The updated basic namespace properties.
        /// </summary>
        [JsonProperty(PropertyName = "update_info")]
        public UpdateNamespaceInfo UpdateInfo { get; set; } = new UpdateNamespaceInfo();

        /// <summary>
        /// The updated namespace configuration.
        /// </summary>
        public NamespaceConfig Config { get; set; } = new NamespaceConfig();

        /// <summary>
        /// The updated namespace replication configuration.
        /// </summary>
        [JsonProperty(PropertyName = "replication_config")]
        public NamespaceReplicationConfig ReplicationConfig { get; set; }

        /// <summary>
        /// The updated namespace security token.
        /// </summary>
        [JsonProperty(PropertyName = "security_token")]
        public string SecurityToken { get; set; }

        /// <summary>
        /// The updated namespace bad binary.
        /// </summary>
        [JsonProperty(PropertyName = "delete_bad_binary")]
        public string DeleteBadBinary { get; set; }
    }
}
