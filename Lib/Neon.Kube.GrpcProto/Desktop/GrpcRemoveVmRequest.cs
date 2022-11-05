//-----------------------------------------------------------------------------
// FILE:	    GrpcRemoveVmRequest.cs
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
    /// Removes a virtual machine.  This request returns a <see cref="GrpcBaseReply"/>.
    /// </summary>
    [DataContract]
    public class GrpcRemoveVmRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcRemoveVmRequest()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="machineName">Specifies the machine name.</param>
        /// <param name="keepDrives">Optionally retains the VM disk files.</param>
        public GrpcRemoveVmRequest(string machineName, bool keepDrives = false)
        {
            this.MachineName = machineName;
            this.KeepDrives  = keepDrives;
        }

        /// <summary>
        /// The machine name.
        /// </summary>
        [DataMember(Order = 1)]
        public string? MachineName { get; set; }

        /// <summary>
        /// Indicates whether the virtual machine drives should be retained after 
        /// removing the machine.
        /// </summary>
        [DataMember(Order = 2)]
        public bool KeepDrives { get; set; }
    }
}
