//-----------------------------------------------------------------------------
// FILE:	    GrpcVirtualMachineNetworkAdapter.cs
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
    /// Describes a network adaptor attached to a virtual machine.
    /// </summary>
    [DataContract]
    public class GrpcVirtualMachineNetworkAdapter
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcVirtualMachineNetworkAdapter()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The adapter name.</param>
        /// <param name="isManagementOs"><c>true</c> if this adapter is attached to the management operating system.</param>
        /// <param name="vmName">The name of the attached virtual machine.</param>
        /// <param name="switchName">TThe adapter's MAC address.he attached switch name.</param>
        /// <param name="macAddress">The adapter's MAC address.</param>
        /// <param name="status">The adapter status.</param>
        /// <param name="addresses">The IP addresses assigned to the adapter.</param>
        public GrpcVirtualMachineNetworkAdapter(
            string          name,
            bool            isManagementOs,
            string          vmName,
            string          switchName,
            string          macAddress,
            string          status,
            List<string>    addresses)
        {
            this.Name           = name;
            this.IsManagementOs = isManagementOs;
            this.VMName         = vmName;
            this.SwitchName     = switchName;
            this.MacAddress     = macAddress;
            this.Status         = status;
            this.Addresses      = addresses;
        }

        /// <summary>
        /// The adapter name.
        /// </summary>
        [DataMember(Order = 1)]
        public string? Name { get; set; }

        /// <summary>
        /// <c>true</c> if this adapter is attached to the management operating system.
        /// </summary>
        [DataMember(Order = 2)]
        public bool IsManagementOs { get; set; }

        /// <summary>
        /// The name of the attached virtual machine.
        /// </summary>
        [DataMember(Order = 3)]
        public string? VMName { get; set; }

        /// <summary>
        /// The attached switch name.
        /// </summary>
        [DataMember(Order = 14)]
        public string? SwitchName { get; set; }

        /// <summary>
        /// The adapter's MAC address.
        /// </summary>
        [DataMember(Order = 5)]
        public string? MacAddress { get; set; }

        /// <summary>
        /// The adapter status.
        /// </summary>
        [DataMember(Order = 6)]
        public string? Status { get; set; }

        /// <summary>
        /// The IP addresses assigned to the adapter.
        /// </summary>
        [DataMember(Order = 7)]
        public List<string>? Addresses { get; set; }
    }
}
