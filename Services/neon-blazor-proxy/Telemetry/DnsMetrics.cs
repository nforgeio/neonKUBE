//-----------------------------------------------------------------------------
// FILE:	    DnsMetrics.cs
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
    public class DnsMetrics : IMetricsConsumer<NameResolutionMetrics>
    {
        /// <summary>
        /// Additional DNS lookups requested by the <see cref="DnsClient"/> package.
        /// </summary>
        public static int DnsLookupsRequested = 0;

        private static readonly Counter _dnsLookupsRequested = Metrics.CreateCounter(
            "neonblazorproxy_dns_lookups_requested",
            "Number of DNS lookups requested"
            );

        /// <inheritdoc/>
        public void OnMetrics(NameResolutionMetrics previous, NameResolutionMetrics current)
        {
            _dnsLookupsRequested.IncTo(current.DnsLookupsRequested + DnsLookupsRequested);
        }
    }
}
