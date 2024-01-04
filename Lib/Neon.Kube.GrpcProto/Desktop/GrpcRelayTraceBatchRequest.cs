//-----------------------------------------------------------------------------
// FILE:        GrpcRelayTraceBatchRequest.cs
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
using System.Diagnostics.Contracts;
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
    /// <para>
    /// Used to submit a batch of telemetry traces from <b>neon-desktop</b> and
    /// <b>neon-cli</b> to the <b>neon-desktop-service</b> which will then forward
    /// them to the headend.
    /// </para>
    /// <note>
    /// The batch is actually serialized as a JSON string so that we won't have to
    /// define protobufs for this, keeping things simple.
    /// </note>
    /// </summary>
    [DataContract]
    public class GrpcRelayTraceBatchRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcRelayTraceBatchRequest()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="batchJson">The batched trace records serialized as JSON.</param>
        public GrpcRelayTraceBatchRequest(string batchJson)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(batchJson), nameof(batchJson));

            this.BatchJson = batchJson;
        }

        /// <summary>
        /// The log record batch serialized as JSON.
        /// </summary>
        [DataMember(Order = 1)]
        public string? BatchJson { get; set; }
    }
}
