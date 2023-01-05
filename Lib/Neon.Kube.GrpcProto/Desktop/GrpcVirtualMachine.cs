//-----------------------------------------------------------------------------
// FILE:	    GrpcVirtualMachine.cs
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
    /// Describes the state of a Hyper-V virtual machine.
    /// </summary>
    [DataContract]
    public class GrpcVirtualMachine
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcVirtualMachine()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">Specifies the machine name.</param>
        /// <param name="state">Specifies the machine state.  This corresponds to [VirtualMachineState] defined in [Neon.HyperV].</param>
        /// <param name="switchName">Optionally identifies the attached switch.</param>
        /// <param name="interfaceName">Optionall identifies the attached network adaptor.</param>
        public GrpcVirtualMachine(string name, string state, string? switchName, string? interfaceName)
        {
            this.Name          = name;
            this.State         = state;
            this.SwitchName    = switchName;
            this.InterfaceName = interfaceName;
        }

        /// <summary>
        /// The machine name.
        /// </summary>
        [DataMember(Order = 1)]
        public string? Name { get; set; }

        /// <summary>
        /// The current machine state.  This corresponds to [VirtualMachineState] defined in [Neon.HyperV].
        /// </summary>
        [DataMember(Order = 2)]
        public string? State { get; set; }

        /// <summary>
        /// Identifies the virtual switch to which this virtual machine is attached (or null).
        /// </summary>
        [DataMember(Order = 3)]
        public string? SwitchName { get; set; }

        /// <summary>
        /// Identifies the network interface or switch to which the address is assigned (or null).
        /// </summary>
        [DataMember(Order = 4)]
        public string? InterfaceName { get; set; }
    }
}
