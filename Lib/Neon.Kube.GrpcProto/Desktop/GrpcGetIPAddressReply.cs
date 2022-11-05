//-----------------------------------------------------------------------------
// FILE:	    GrpcGetIPAddressReply.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
    /// Returns information about a virtual IP address.
    /// </summary>
    [DataContract]
    public class GrpcGetIPAddressReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcGetIPAddressReply()
        {
        }

        /// <summary>
        /// Error constructor.
        /// </summary>
        /// <param name="e">The exception.</param>
        public GrpcGetIPAddressReply(Exception e)
        {
            this.Error = new GrpcError(e);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="address">The virtual IP address.</param>
        public GrpcGetIPAddressReply(GrpcVirtualIPAddress? address)
        {
            this.Address = address;
        }

        /// <summary>
        /// Set to a non-null error when the request failed.
        /// </summary>
        [DataMember(Order = 1)]
        public GrpcError? Error { get; set; }

        /// <summary>
        /// Describes the switch or <c>null</c> when the switch doesn't exist.
        /// </summary>
        [DataMember(Order = 2)]
        public GrpcVirtualIPAddress? Address { get; set; }
    }
}
