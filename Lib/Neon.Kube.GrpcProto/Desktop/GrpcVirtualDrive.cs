//-----------------------------------------------------------------------------
// FILE:	    GrpcVirtualDrive.cs
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
    /// Specifies a virtual drive.
    /// </summary>
    [DataContract]
    public class GrpcVirtualDrive
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcVirtualDrive()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="path">Specifies the path where the drive is located.</param>
        /// <param name="size">The drive size in bytes.</param>
        /// <param name="isDynamic">
        /// Indicates whether a dynamic drive will be created as opposed to a
        /// pre-allocated fixed drive.
        /// </param>
        public GrpcVirtualDrive(string path, decimal size, bool isDynamic)
        {
            this.Path      = path;
            this.Size      = (long)size;
            this.IsDynamic = isDynamic;
        }

        /// <summary>
        /// Specifies the path where the drive will be located.  The drive format
        /// is indicated by the file type, either <b>.vhd</b> or <b>.vhdx</b>.
        /// </summary>
        [DataMember(Order = 1)]
        public string? Path { get; set; }

        /// <summary>
        /// The drive size in bytes.
        /// </summary>
        [DataMember(Order = 2)]
        public long Size { get; set; }

        /// <summary>
        /// Indicates whether a dynamic drive will be created as opposed to a
        /// pre-allocated fixed drive.  This defaults to <b>true</b>.
        /// </summary>
        [DataMember(Order = 3)]
        public bool IsDynamic { get; set; } = true;
    }
}
