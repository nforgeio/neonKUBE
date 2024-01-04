//-----------------------------------------------------------------------------
// FILE:        GrpcListNatsReply.cs
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
    /// Lists the information about the Hyper-V virtual NATs.
    /// </summary>
    [DataContract]
    public class GrpcListNatsReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcListNatsReply()
        {
        }

        /// <summary>
        /// Error constructor.
        /// </summary>
        /// <param name="e">The exception.</param>
        public GrpcListNatsReply(Exception e)
        {
            this.Error = new GrpcError(e);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="nats">The virtual NATs.</param>
        public GrpcListNatsReply(List<GrpcVirtualNat> nats)
        {
            this.Nats = nats;
        }

        /// <summary>
        /// Set to a non-null error when the request failed.
        /// </summary>
        [DataMember(Order = 1)]
        public GrpcError? Error { get; set; }

        /// <summary>
        /// Information about the virtual NATs.
        /// </summary>
        [DataMember(Order = 2)]
        public List<GrpcVirtualNat>? Nats { get; set; } = new List<GrpcVirtualNat>();
    }
}
