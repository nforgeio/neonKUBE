//-----------------------------------------------------------------------------
// FILE:	    MetricsOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Text.RegularExpressions;
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
    /// authentication at this time.  
    /// </para>
    /// <para>
    /// For more complex scenarios, just leave <see cref="Mode"/><c>==</c><see cref="MetricsMode.Disabled"/>
    /// and configure <b>prometheus-net</b> yourself before calling <see cref="NeonService.RunAsync(bool)"/>.  We're
    /// trying to address 80% scenarios to reduce a bit of service related boilerplate code but <b>prometheus-net</b>
    /// is quite easy to configure.
    /// </para>
    /// </note>
    /// <note>
    /// <para>
    /// For ASPNET applications, you have some choices:
    /// </para>
    /// <list type="number">
    /// <item>
    /// Leave metrics disabled here and configure middleware to handle the metrics; this will
    /// automatically much more detailed web related metrics.  You can use the standard 
    /// <b>prometheus-net</b> middleware builder extension.
    /// </item>
    /// <item>
    /// Enable metrics here and optionally set <see cref="GetCollector"/> to a function that
    /// returns the 
    /// </item>
    /// <item>
    /// </item>
    /// </list>
    /// 
    /// 
    /// 
    /// 
    /// 
    /// , we recommend that you leave metrics collection disabled here and 
    /// configure middleware to handle the metrics; this will automatically much more detailed web
    /// related metrics.  You can use the standard <b>prometheus-net</b> middleware builder extension.
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
        /// for scraping by Prometheus.  This defaults to <see cref="NetworkPorts.PrometheusMetrics"/>.
        /// </summary>
        public int Port { get; set; } = NetworkPorts.PrometheusMetrics;

        /// <summary>
        /// Specifies the URL path for the local HTTP listener that exposes metrics
        /// for scraping by Prometheus.  This defaults to <b>"metrics/"</b>.
        /// </summary>
        public string Path { get; set; } = "metrics/";

        /// <summary>
        /// Specifies the target Prometheus Pushgateway for <see cref="MetricsMode.Push"/> mode.
        /// This defaults to <c>null</c>.
        /// </summary>
        public string PushUrl { get; set; } = null;

        /// <summary>
        /// Specifies how often metrics will be pushed to the target Prometheus Pushgateway for 
        /// <see cref="MetricsMode.Push"/> mode.  This defaults to <b>5 seconds</b>.
        /// </summary>
        public TimeSpan PushInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Optionally specifies additional labels to be identify the source for <see cref="MetricsMode.Push"/> mode.
        /// </summary>
        public IList<Tuple<string, string>> PushLabels { get; set; } = new List<Tuple<string, string>>();

        /// <summary>
        /// <para>
        /// Optionally configures a callback that can return a custom metrics collector for the service.
        /// </para>
        /// <para>
        /// We recommend that you configure this to return the standard .NET Runtime or ASP.NET runtime metrics
        /// collector so your services will report those as well.  The code to configure the .NET Runtime metrics
        /// looks like this:
        /// </para>
        /// <code language="C#">
        /// Service.MetricsOptions.GetCollector =
        ///     () =>
        ///     {
        ///         return DotNetRuntimeStatsBuilder
        ///             .Default()
        ///             .StartCollecting();
        ///     };
    /// </code>
    /// </summary>
    public Func<IDisposable> GetCollector { get; set; }

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown for any errors.</exception>
        public void Validate()
        {
            switch (Mode)
            {
                case MetricsMode.Disabled:

                    break;

                case MetricsMode.Scrape:
                case MetricsMode.ScrapeIgnoreErrors:

                    if (!NetHelper.IsValidPort(Port))
                    {
                        throw new ArgumentNullException($"Metrics [Port={Port}] is not valid.");
                    }

                    if (string.IsNullOrEmpty(Path))
                    {
                        throw new ArgumentNullException("Metrics [Path] is required.");
                    }

                    if (Path.StartsWith("/"))
                    {
                        throw new ArgumentNullException($"Metrics [Path={Path}] cannot start with a slash [/].");
                    }

                    if (!Path.EndsWith("/"))
                    {
                        throw new ArgumentNullException($"Metrics [Path={Path}] must end with a slash [/].");
                    }
                    break;

                case MetricsMode.Push:

                    if (string.IsNullOrEmpty(PushUrl))
                    {
                        throw new ArgumentNullException("Metrics [PushUrl] is required.");
                    }

                    if (!Uri.TryCreate(PushUrl, UriKind.Absolute, out var uri))
                    {
                        throw new ArgumentNullException($"Metrics [PushUrl={PushUrl}] is not a valid URL.");
                    }
                    break;

                default:

                    throw new NotImplementedException();
            }

            if (PushLabels != null)
            {
                var labelNameRegex = new Regex(@"[a-zA-Z_][a-zA-Z0-9_]*");

                foreach (var item in PushLabels)
                {
                    if (labelNameRegex.IsMatch(item.Item1))
                    {
                        throw new ArgumentException($"[{item.Item1}] is not a valid label.");
                    }
                }
            }
        }
    }
}
