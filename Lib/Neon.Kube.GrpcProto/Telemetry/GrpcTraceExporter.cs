//-----------------------------------------------------------------------------
// FILE:	    GrpcTraceExporter.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using DNS.Client;

using Neon.Common;
using Neon.Kube;
using Neon.Kube.GrpcProto.Desktop;
using Neon.Tasks;
using Neon.Time;

using OpenTelemetry;

namespace Neon.Kube
{
    /// <summary>
    /// Implements a trace exporter that forwards trace batches to the <b>neon-desktop-service</b>
    /// which then handles the transmission to the headend.
    /// </summary>
    public class GrpcTraceExporter : BaseExporter<Activity>
    {
        private IGrpcDesktopService? desktopService;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="desktopService">Specifies the gRPC client for the <b>neon-desktop-service</b>.</param>
        public GrpcTraceExporter(IGrpcDesktopService desktopService)
        {
            Covenant.Requires<ArgumentNullException>(desktopService != null, nameof(desktopService));

            this.desktopService = desktopService;
        }

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<Activity> batch)
        {
            desktopService?.RelayTraceBatchAsync(new GrpcRelayTraceBatchRequest() { BatchJson = NeonHelper.JsonSerialize(batch) });

            return ExportResult.Success;
        }
    }
}
