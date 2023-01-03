//-----------------------------------------------------------------------------
// FILE:	    GrpcCompactDriveRequest.cs
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
    /// Compacts a virtual disk.  This request returns a <see cref="GrpcBaseReply"/>.
    /// </summary>
    [DataContract]
    public class GrpcCompactDriveRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcCompactDriveRequest()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="drivePath">Specifies the path to the virtual drive.</param>
        public GrpcCompactDriveRequest(string drivePath)
        {
            this.DrivePath = drivePath;
        }

        /// <summary>
        /// Specifies the path to the virtual drive.
        /// </summary>
        [DataMember(Order = 1)]
        public string? DrivePath { get; set; }
    }
}
