//-----------------------------------------------------------------------------
// FILE:	    GrpcGetVmNetworkAdaptersReply.cs
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
    /// Returns the network adaptors attached to a virtual machine.
    /// </summary>
    [DataContract]
    public class GrpcGetVmNetworkAdaptersReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcGetVmNetworkAdaptersReply()
        {
        }

        /// <summary>
        /// Error constructor.
        /// </summary>
        /// <param name="e">The exception.</param>
        public GrpcGetVmNetworkAdaptersReply(Exception e)
        {
            this.Error = new GrpcError(e);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="adapters">The attached network adapters.</param>
        public GrpcGetVmNetworkAdaptersReply(List<GrpcVirtualNetworkAdapter> adapters)
        {
            this.Adapters = adapters;
        }

        /// <summary>
        /// Set to a non-null error when the request failed.
        /// </summary>
        [DataMember(Order = 1)]
        public GrpcError? Error { get; set; }

        /// <summary>
        /// Returns information about the attached network adapters.
        /// </summary>
        [DataMember(Order = 2)]
        public List<GrpcVirtualNetworkAdapter>? Adapters { get; set; }
    }
}
