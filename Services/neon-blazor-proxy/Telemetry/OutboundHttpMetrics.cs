//-----------------------------------------------------------------------------
// FILE:	    OutboundHttpMetrics.cs
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

using System;

using Prometheus;

using Yarp.Telemetry.Consumption;

namespace NeonBlazorProxy
{
    /// <inheritdoc/>
    public sealed class OutboundHttpMetrics : IMetricsConsumer<HttpMetrics>
    {
        private static readonly double CUBE_ROOT_10 = Math.Pow(10, (1.0 / 3));

        private static readonly Counter _outboundRequestsStarted = Metrics.CreateCounter(
            "neonblazorproxy_outbound_http_requests_started",
            "Number of outbound requests inititated by the proxy"
            );

        private static readonly Counter _outboundRequestsFailed = Metrics.CreateCounter(
            "neonblazorproxy_outbound_http_requests_failed",
            "Number of outbound requests failed"
            );

        private static readonly Gauge _outboundCurrentRequests = Metrics.CreateGauge(
            "neonblazorproxy_outbound_http_current_requests",
            "Number of active outbound requests that have started but not yet completed or failed"
            );

        private static readonly Gauge _outboundCurrentHttp11Connections = Metrics.CreateGauge(
            "neonblazorproxy_outbound_http11_connections",
            "Number of currently open HTTP 1.1 connections"
            );

        private static readonly Gauge _outboundCurrentHttp20Connections= Metrics.CreateGauge(
            "neonblazorproxy_outbound_http20_connections",
            "Number of active proxy requests that have started but not yet completed or failed"
            );

        private static readonly Histogram _outboundHttp11RequestQueueDuration= Metrics.CreateHistogram(
            "neonblazorproxy_outbound_http11_request_queue_duration",
            "Average time spent on queue for HTTP 1.1 requests that hit the MaxConnectionsPerServer limit in the last metrics interval",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(10, CUBE_ROOT_10, 10)
            });

        private static readonly Histogram _outboundHttp20RequestQueueDuration = Metrics.CreateHistogram(
            "neonblazorproxy_outbound_http20_request_queue_duration",
            "Average time spent on queue for HTTP 2.0 requests that hit the MAX_CONCURRENT_STREAMS limit on the connection in the last metrics interval",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(10, CUBE_ROOT_10, 10)
            });

        public void OnMetrics(HttpMetrics previous, HttpMetrics current)
        {
            _outboundRequestsStarted.IncTo(current.RequestsStarted);
            _outboundRequestsFailed.IncTo(current.RequestsFailed);
            _outboundCurrentRequests.Set(current.CurrentRequests);
            _outboundCurrentHttp11Connections.Set(current.CurrentHttp11Connections);
            _outboundCurrentHttp20Connections.Set(current.CurrentHttp20Connections);
            _outboundHttp11RequestQueueDuration.Observe(current.Http11RequestsQueueDuration.TotalMilliseconds);
            _outboundHttp20RequestQueueDuration.Observe(current.Http20RequestsQueueDuration.TotalMilliseconds);
        }
    }
}
