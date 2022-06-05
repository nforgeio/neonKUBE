//-----------------------------------------------------------------------------
// FILE:	    AppState.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Net;
using Neon.Tasks;

using NeonDashboard.Model;
using NeonDashboard.Shared;
using NeonDashboard.Shared.Components;

using Blazor.Analytics;
using Blazor.Analytics.Components;

using Blazored.LocalStorage;

using k8s;
using k8s.Models;

using Prometheus;
using System.Globalization;
using Neon.Collections;

namespace NeonDashboard
{
    public partial class AppState
    {
        public class __Metrics 
        {
            private AppState AppState;
            private Service NeonDashboardService => AppState.NeonDashboardService;
            private __Cache Cache => AppState.Cache;
            private INeonLogger Logger => AppState.Logger;
            private JsonClient mimirClient;


            public __Metrics(AppState state)
            {
                AppState = state;
                mimirClient = new JsonClient()
                {
                    BaseAddress = new Uri("http://10.10.100.2:1234/prometheus/")
                };
            }

            public async Task<PrometheusResponse<PrometheusMatrixResult>> GetMemoryUsageAsync(DateTime start, DateTime end, string stepSize = "15s")
            {
                var args = new ArgDictionary();

                args.Add("query", $@"sum(container_memory_working_set_bytes{{cluster=~""{NeonDashboardService.ClusterInfo.Name}""}})");
                args.Add("start", start.ToString("yyyy-MM-dd'T'HH:mm:ss.fffZ", DateTimeFormatInfo.InvariantInfo));
                args.Add("end", end.ToString("yyyy-MM-dd'T'HH:mm:ss.fffZ", DateTimeFormatInfo.InvariantInfo));
                args.Add("step", stepSize);

                var result = await QueryAsync<PrometheusResponse<PrometheusMatrixResult>>("api/v1/query_range", args);

                return result;
            }

            public async Task<T> QueryAsync<T>(string url, ArgDictionary args)
            {
                return await mimirClient.GetAsync<T>(url, args);
            }
        }
    }
}