//-----------------------------------------------------------------------------
// FILE:        Location.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Specifies whether the service is part of Istio mesh or outside the mesh. Location determines the behavior of several
    /// features, such as service-to-service mTLS authentication, policy enforcement, etc. When communicating with services outside 
    /// the mesh, Istio’s mTLS authentication is disabled, and policy enforcement is performed on the client-side as opposed to
    /// server-side.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
    public enum Location
    {
        /// <summary>
        /// Signifies that the service is external to the mesh. Typically used to indicate external services consumed through APIs.
        /// </summary>
        [EnumMember(Value = "MESH_EXTERNAL")]
        MeshExternal = 0,

        /// <summary>
        /// Signifies that the service is part of the mesh. Typically used to indicate services added explicitly as part of 
        /// expanding the service mesh to include unmanaged infrastructure (e.g., VMs added to a Kubernetes based service mesh).
        /// </summary>
        [EnumMember(Value = "MESH_INTERNAL")]
        MeshInternal
    }
}
