//-----------------------------------------------------------------------------
// FILE:        GrpcFindNatByNameRequest.cs
// CONTRIBUTOR: Jeff Lill
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
    /// Returns information about a virtual Hyper-V NAT by name.  This returns a <see cref="GrpcFindNatReply"/>.
    /// </summary>
    [DataContract]
    public class GrpcFindNatByNameRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcFindNatByNameRequest()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">Specifies the desired NAT name.</param>
        public GrpcFindNatByNameRequest(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Identifies the NAT by name.
        /// </summary>
        [DataMember(Order = 1)]
        public string? Name { get; set; }
    }
}
