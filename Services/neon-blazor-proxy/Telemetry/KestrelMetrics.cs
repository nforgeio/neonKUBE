//-----------------------------------------------------------------------------
// FILE:	    KestrelMetrics.cs
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

    /// <inheritdoc/>
    public sealed class KestrelMetrics : IMetricsConsumer<Yarp.Telemetry.Consumption.KestrelMetrics>
    {
        private static readonly Counter _totalConnections = Metrics.CreateCounter(
            "neonblazorproxy_kestrel_total_connections",
            "Number of incomming connections opened"
            );

        private static readonly Counter _totalTlsHandshakes = Metrics.CreateCounter(
            "neonblazorproxy_kestrel_total_tls_Handshakes",
            "Numer of TLS handshakes started"
            );

        private static readonly Gauge _currentTlsHandshakes = Metrics.CreateGauge(
            "neonblazorproxy_kestrel_current_tls_handshakes",
            "Number of active TLS handshakes that have started but not yet completed or failed"
            );

        private static readonly Counter _failedTlsHandshakes = Metrics.CreateCounter(
            "neonblazorproxy_kestrel_failed_tls_handshakes",
            "Number of TLS handshakes that failed"
            );

        private static readonly Gauge _currentConnections = Metrics.CreateGauge(
            "neonblazorproxy_kestrel_current_connections",
            "Number of currently open incomming connections"
            );

        private static readonly Gauge _connectionQueueLength = Metrics.CreateGauge(
            "neonblazorproxy_kestrel_connection_queue_length",
            "Number of connections on the queue."
            );

        private static readonly Gauge _requestQueueLength = Metrics.CreateGauge(
            "neonblazorproxy_kestrel_request_queue_length",
            "Number of requests on the queue"
            );

        /// <inheritdoc/>
        public void OnMetrics(
            Yarp.Telemetry.Consumption.KestrelMetrics previous, 
            Yarp.Telemetry.Consumption.KestrelMetrics current)
        {
            _totalConnections.IncTo(current.TotalConnections);
            _totalTlsHandshakes.IncTo(current.TotalTlsHandshakes);
            _currentTlsHandshakes.Set(current.CurrentTlsHandshakes);
            _failedTlsHandshakes.IncTo(current.FailedTlsHandshakes);
            _currentConnections.Set(current.CurrentConnections);
            _connectionQueueLength.Set(current.ConnectionQueueLength);
            _requestQueueLength.Set(current.RequestQueueLength);
        }
    }
}
