//-----------------------------------------------------------------------------
// FILE:	    WebsocketMetrics.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Prometheus;

using Yarp.Telemetry.Consumption;

namespace NeonBlazorProxy
{
    /// <summary>
    /// Collects Websocket metrics.
    /// </summary>
    public class WebsocketMetrics
    {
        /// <summary>
        /// The total number of websocket connections established.
        /// </summary>
        public static readonly Counter ConnectionsEstablished = Metrics.CreateCounter(
            "neonblazorproxy_websockets_connections_established_total",
            "Number of websocket requests inititated."
            );

        /// <summary>
        /// The current number of open websocket connections.
        /// </summary>
        public static readonly Gauge CurrentConnections = Metrics.CreateGauge(
            "neonblazorproxy_websockets_current_connections",
            "Number of active websocket connections that have are connected."
            );
    }
}
