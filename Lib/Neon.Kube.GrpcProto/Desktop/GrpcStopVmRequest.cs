//-----------------------------------------------------------------------------
// FILE:        GrpcStopVmRequest.cs
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
    /// Stops a virtual machine.  This request returns a <see cref="GrpcBaseReply"/>.
    /// </summary>
    [DataContract]
    public class GrpcStopVmRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcStopVmRequest()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="machineName">Specifies the machine name.</param>
        /// <param name="turnOff">
        /// <para>
        /// Optionally just turns the VM off without performing a graceful shutdown first.
        /// </para>
        /// <note>
        /// <b>WARNING!</b> This could result in corruption or the the loss of unsaved data.
        /// </note>
        /// </param>
        public GrpcStopVmRequest(string machineName, bool turnOff = false)
        {
            this.MachineName = machineName;
            this.TurnOff     = turnOff;
        }

        /// <summary>
        /// Identifies the desired virtual machine.
        /// </summary>
        [DataMember(Order = 1)]
        public string? MachineName { get; set; }

        /// <summary>
        /// Indicates that the virtual machine should be turned of as opposed to be
        /// shutdown gracefully.
        /// </summary>
        [DataMember(Order = 2)]
        public bool TurnOff { get; set; }
    }
}
