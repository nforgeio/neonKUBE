//-----------------------------------------------------------------------------
// FILE:	    GrpcVirtualIPAddress.cs
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
    /// Describes a virtual Hyper-V IP address.
    /// </summary>
    [DataContract]
    public class GrpcVirtualIPAddress
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcVirtualIPAddress()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="address">The associated IP address.</param>
        /// <param name="subnet">The network subnet.</param>
        /// <param name="interfaceName">
        /// Identifies the network interface or switch to which this address
        /// is connected.
        /// </param>
        public GrpcVirtualIPAddress(string address, NetworkCidr subnet, string interfaceName)
        {
            this.Address       = address;
            this.Subnet        = subnet.ToString();
            this.InterfaceName = interfaceName;
        }

        /// <summary>
        /// The associated IP address.
        /// </summary>
        [DataMember(Order = 1)]
        public string? Address { get; set; }

        /// <summary>
        /// The network subnet.
        /// </summary>
        [DataMember(Order = 2)]
        public string? Subnet { get; set; }

        /// <summary>
        /// Identifies the network interface or switch to which this address
        /// is connected.
        /// </summary>
        [DataMember(Order = 3)]
        public string? InterfaceName { get; set; }
    }
}
