//-----------------------------------------------------------------------------
// FILE:	    GrpcNewInternalSwitchRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

using ProtoBuf.Grpc;

namespace Neon.Kube.GrpcProto.Desktop
{
    /// <summary>
    /// Adds an internal Hyper-V switch configured for the specified subnet and gateway as well
    /// as an optional NAT enabling external connectivity.  This requ3est returns a <see cref="GrpcBaseReply"/>.
    /// </summary>
    [DataContract]
    public class GrpcNewInternalSwitchRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcNewInternalSwitchRequest()
        {
        }

        /// <summary>
        /// Adds an internal Hyper-V switch configured for the specified subnet and gateway as well
        /// as an optional NAT enabling external connectivity.
        /// </summary>
        /// <param name="switchName">The new switch name.</param>
        /// <param name="subnet">Specifies the internal subnet.</param>
        /// <param name="addNat">Optionally configure a NAT to support external routing.</param>
        public GrpcNewInternalSwitchRequest(string switchName, NetworkCidr subnet, bool addNat = false)
        {
            this.SwitchName = switchName;
            this.Subnet     = subnet.ToString();
            this.AddNat     = addNat;
        }

        /// <summary>
        /// The new switch name.
        /// </summary>
        [DataMember(Order = 1)]
        public string? SwitchName { get; set; }

        /// <summary>
        /// Specifies the internal subnet (as a <see cref="NetworkCidr"/> string.
        /// </summary>
        [DataMember(Order = 2)]
        public string? Subnet { get; set; }

        /// <summary>
        /// Configure a NAT to support external routing.
        /// </summary>
        [DataMember(Order = 3)]
        public bool AddNat { get; set; }
    }
}
