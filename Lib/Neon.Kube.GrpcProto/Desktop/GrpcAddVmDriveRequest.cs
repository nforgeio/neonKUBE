//-----------------------------------------------------------------------------
// FILE:	    GrpcAddVmDriveRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    /// Requests information about a virtual machine's drives.  This request returns a <see cref="GrpcErrorReply"/>.
    /// </summary>
    [DataContract]
    public class GrpcAddVmDriveRequest
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="machineName">Specifies the machine name.</param>
        /// <param name="path">
        /// Specifies the path where the drive is located.  The drive format
        /// is indicated by the file type, either <b>.vhd</b> or <b>.vhdx</b>.
        /// </param>
        /// <param name="size">The drive size in bytes.</param>
        /// <param name="isDynamic">Controls whether the drive is fixed or dynamically sizable (defaults to <c>true</c> or <b>dynamic</b>).</param>
        public GrpcAddVmDriveRequest(string machineName, string path, decimal size, bool isDynamic = true)
        {
            this.MachineName = machineName;
            this.Path        = path;
            this.Size        = (long)size;
            this.IsDynamic   = isDynamic;
        }

        /// <summary>
        /// Identifies the desired virtual machine.
        /// </summary>
        [DataMember(Order = 1)]
        public string MachineName { get; set; }

        /// <summary>
        /// Specifies the path where the drive is located.  The drive format
        /// is indicated by the file type, either <b>.vhd</b> or <b>.vhdx</b>.
        /// </summary>
        [DataMember(Order = 2)]
        public string Path { get; set; }

        /// <summary>
        /// The drive size in bytes.
        /// </summary>
        [DataMember(Order = 3)]
        public long Size { get; set; }

        /// <summary>
        /// Indicates whether a dynamic drive will be created as opposed to a
        /// pre-allocated fixed drive.
        /// </summary>
        [DataMember(Order = 4)]
        public bool IsDynamic { get; set; }
    }
}
