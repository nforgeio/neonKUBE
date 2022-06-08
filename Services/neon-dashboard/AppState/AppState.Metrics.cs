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
        public class __Metrics : AppStateBase
        {
            /// <summary>
            /// Event action for updates to Kube properties.
            /// </summary>
            public event Action OnChange;
            private void NotifyStateChanged() => OnChange?.Invoke();

            private PrometheusClient mimirClient;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="state"></param>
            public __Metrics(AppState state)
                : base(state)
            {
                AppState = state;
                mimirClient = new PrometheusClient("http://10.10.100.2:1234/prometheus/");
            }

            public async Task<PrometheusResponse<PrometheusMatrixResult>> GetMemoryUsageAsync(DateTime start, DateTime end, string stepSize = "15s")
            {
                var query = $@"sum(container_memory_working_set_bytes{{cluster=~""{NeonDashboardService.ClusterInfo.Name}""}})";
                var result = await mimirClient.QueryRangeAsync(query, start, end, stepSize);

                NotifyStateChanged();

                return result;
            }
        }
    }
}