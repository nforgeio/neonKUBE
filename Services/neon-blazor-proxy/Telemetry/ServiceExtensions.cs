//-----------------------------------------------------------------------------
// FILE:	    ServiceExtensions.cs
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

using Microsoft.Extensions.DependencyInjection;

using Prometheus;

using Yarp.Telemetry.Consumption;

namespace NeonBlazorProxy
{
    public static class ServiceExtensions
    {
        /// <summary>
        /// Add Forwarder metrics.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddPrometheusForwarderMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IMetricsConsumer<Yarp.Telemetry.Consumption.ForwarderMetrics>, ForwarderMetrics>();
            return services;
        }

        /// <summary>
        /// Add DNS metrics.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddPrometheusDnsMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IMetricsConsumer<NameResolutionMetrics>, DnsMetrics>();
            return services;
        }

        /// <summary>
        /// Add Kestrel metrics.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddPrometheusKestrelMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IMetricsConsumer<Yarp.Telemetry.Consumption.KestrelMetrics>, KestrelMetrics>();
            return services;
        }

        /// <summary>
        /// Add Outbound HTTP metrics.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddPrometheusOutboundHttpMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IMetricsConsumer<HttpMetrics>, OutboundHttpMetrics>();
            return services;
        }

        /// <summary>
        /// Add Socket metrics.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddPrometheusSocketsMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IMetricsConsumer<SocketsMetrics>, SocketMetrics>();
            return services;
        }

        /// <summary>
        /// Add all prometheus metrics.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddAllPrometheusMetrics(this IServiceCollection services)
        {
            services.AddPrometheusForwarderMetrics();
            services.AddPrometheusDnsMetrics();
            services.AddPrometheusKestrelMetrics();
            services.AddPrometheusOutboundHttpMetrics();
            services.AddPrometheusSocketsMetrics();
            return services;
        }
    }
}
