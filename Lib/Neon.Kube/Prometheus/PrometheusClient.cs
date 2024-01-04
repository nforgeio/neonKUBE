//-----------------------------------------------------------------------------
// FILE:        PrometheusClient.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Collections;
using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Client for interacting with the Prometheus API.
    /// </summary>
    public class PrometheusClient
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="uri">Specifies the Prometheus server URI</param>
        /// <param name="username">Optionally specifies the user name.</param>
        /// <param name="password">Optionally Specifies the password.</param>
        public PrometheusClient(string uri, string username = null, string password = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri), nameof(uri));

            if (!uri.EndsWith('/'))
            {
                uri.Append('/');
            }

            JsonClient = new JsonClient()
            {
                BaseAddress = new Uri(uri)
            };

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

                JsonClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
            }
        }

        /// <summary>
        /// Returns backing <see cref="JsonClient"/>.
        /// </summary>
        public JsonClient JsonClient { get; private set; }

        /// <summary>
        /// Fetches a value from the Prometheus API via the backing <see cref="JsonClient"/>.
        /// </summary>
        /// <typeparam name="T">Specifies the type of the value being fetched.</typeparam>
        /// <param name="path">Specifies the value path.</param>
        /// <param name="args">Specifies any arguments.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The <typeparamref name="T"/> value fechted.</returns>
        private async Task<T> GetAsync<T>(string path, ArgDictionary args, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            return await JsonClient.GetAsync<T>(path, args, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes a Prometheus query.
        /// </summary>
        /// <param name="query">Specifies the Prometheus query.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The <see cref="PrometheusResponse{T}"/>.</returns>
        public async Task<PrometheusResponse<PrometheusVectorResult>> QueryAsync(string query, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(query), nameof(query));

            var args = new ArgDictionary();

            args.Add("query", query);

            return await GetAsync<PrometheusResponse<PrometheusVectorResult>>("api/v1/query", args, cancellationToken);
        }

        /// <summary>
        /// Executes a date range Prometheus query.
        /// </summary>
        /// <param name="query">Specifies the Prometheus query.</param>
        /// <param name="start">Specifies the starting time for the query.</param>
        /// <param name="end">Specifies the ending time for the query.</param>
        /// <param name="stepSize">Optionally specifies the query step size.  This defaults to <b>15 seconds)</b>.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The <see cref="PrometheusResponse{T}"/>.</returns>
        public async Task<PrometheusResponse<PrometheusMatrixResult>> QueryRangeAsync(
            string              query,
            DateTime            start,
            DateTime            end,
            TimeSpan            stepSize          = default,
            CancellationToken   cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(query), nameof(query));
            Covenant.Requires<ArgumentException>(start < end, nameof(end));

            if (stepSize < TimeSpan.FromSeconds(1))
            {
                stepSize = TimeSpan.FromSeconds(15);
            }

            var step = $"{Math.Ceiling(stepSize.TotalSeconds)}s";
            var args = new ArgDictionary();

            args.Add("query", query);
            args.Add("start", start.ToString("yyyy-MM-dd'T'HH:mm:ss.fffZ", DateTimeFormatInfo.InvariantInfo));
            args.Add("end", end.ToString("yyyy-MM-dd'T'HH:mm:ss.fffZ", DateTimeFormatInfo.InvariantInfo));
            args.Add("step", step);

            return await GetAsync<PrometheusResponse<PrometheusMatrixResult>>("api/v1/query_range", args, cancellationToken);
        }
    }
}
