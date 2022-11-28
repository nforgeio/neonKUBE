//-----------------------------------------------------------------------------
// FILE:	    PrometheusClient.cs
// CONTRIBUTOR: Marcus Bowyer
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
        /// The JsonClient.
        /// </summary>
        public JsonClient JsonClient;

        /// <summary>
        /// Constructor.
        /// </summary>
        public PrometheusClient(
            string prometheusUrl,
            string username = null,
            string password = null)
        {
            if (!prometheusUrl.EndsWith('/'))
            {
                prometheusUrl.Append('/');
            }

            JsonClient = new JsonClient()
            {
                BaseAddress = new Uri(prometheusUrl)
            };

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                JsonClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
            }
        }

        /// <summary>
        /// Gets a result from the Prometheus API.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="args"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<T> GetAsync<T>(
            string path,
            ArgDictionary args,
            CancellationToken cancellationToken = default)
        {
            return await JsonClient.GetAsync<T>(path, args, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets a regular query from the Prometheus API.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<PrometheusResponse<PrometheusVectorResult>> QueryAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            var args = new ArgDictionary();
            args.Add("query", query);

            return await GetAsync<PrometheusResponse<PrometheusVectorResult>>("api/v1/query", args, cancellationToken);
        }

        /// <summary>
        /// Gets a range query result from the Prometheus API.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="stepSize"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<PrometheusResponse<PrometheusMatrixResult>> QueryRangeAsync(
            string query,
            DateTime start,
            DateTime end,
            string stepSize = "15s",
            CancellationToken cancellationToken = default)
        {
            var args = new ArgDictionary();
            args.Add("query", query);
            args.Add("start", start.ToString("yyyy-MM-dd'T'HH:mm:ss.fffZ", DateTimeFormatInfo.InvariantInfo));
            args.Add("end", end.ToString("yyyy-MM-dd'T'HH:mm:ss.fffZ", DateTimeFormatInfo.InvariantInfo));
            args.Add("step", stepSize);

            return await GetAsync<PrometheusResponse<PrometheusMatrixResult>>("api/v1/query_range", args, cancellationToken);
        }
    }
}
