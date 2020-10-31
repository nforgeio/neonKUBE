//-----------------------------------------------------------------------------
// FILE:	    MetricsOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Windows;

namespace Neon.Service
{
    /// <summary>
    /// Specifies options for a <see cref="NeonService"/>.  This is initialized to reasonable defaults.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These options allow developers to customize some service behaviors.  This is
    /// is exposed as the <see cref="NeonService.MetricsOptions"/> property and is initialized
    /// to reasonable default values.  Developers may modify these options as desired 
    /// before calling <see cref="NeonService.RunAsync(bool)"/> to start their service.
    /// </para>
    /// <para>
    /// Prometheus metrics capturing is disabled by default.  You can change this by 
    /// setting <see cref="Mode"/> to <see cref="MetricsMode.Scrape"/>, <see cref="MetricsMode.ScrapeIgnoreErrors"/>,
    /// or <see cref="MetricsMode.Push"/>.  The two scrape modes expect that Prometheus will
    /// be perodically reading metrics from the service via the HTTP endpoint specified
    /// by <see cref="Port"/> and <see cref="Path"/>.
    /// </para>
    /// <note>
    /// The <see cref="MetricsMode.ScrapeIgnoreErrors"/> mode is somewhat specialized and is
    /// intended for testing environments and is not recommended for production.
    /// </note>
    /// <note>
    /// <para>
    /// Built-in Prometheus scraping support is limited to HTTP and not HTTPS and no authentication
    /// is enforced.  Pushgateway support can use HTTPS as well as HTTP, but we don't support
    /// authentication.  
    /// </para>
    /// <para>
    /// For more complex scenarios, just leave <see cref="Mode"/><c>==</c><see cref="MetricsMode.Disabled"/>
    /// and configure <b>prometheus-net</b> yourself before calling <see cref="NeonService.RunAsync(bool)"/>.  We're
    /// trying to address 80% scenarios to reduce a bit of service related boilerplate code but <b>prometheus-net</b>
    /// is quite easy to configure.
    /// </para>
    /// </note>
    /// <note>
    /// For ASPNET applications, we recommend that you leave metrics collection disabled here and 
    /// configure middleware to handle the metrics; this will automatically much more detailed web
    /// related metrics.  You can use the standard <b>prometheus-net</b> middleware builder extension
    /// or a slightly modifed builder extension from <b>Neon.Web</b>.
    /// </note>
    /// </remarks>
    public class MetricsOptions
    {
        /// <summary>
        /// Enables Prometheus and controls how metrics are published.
        /// </summary>
        public MetricsMode Mode { get; set; } = MetricsMode.Disabled;

        /// <summary>
        /// Specifies the TCP port for the local HTTP listener that exposes metrics
        /// for scraping by Prometheus.
        /// </summary>
        public int Port { get; set; } = NetworkPorts.NeonPrometheus;

        /// <summary>
        /// Specifies the URL path for the local HTTP listener that exposes metrics
        /// for scraping by Prometheus.
        /// </summary>
        public string Path { get; set; } = "metrics/";

        /// <summary>
        /// Specifies the target Prometheus Pushgateway for <see cref="MetricsMode.Push"/> mode.
        /// </summary>
        public string PushUrl { get; set; } = null;
    }
}
