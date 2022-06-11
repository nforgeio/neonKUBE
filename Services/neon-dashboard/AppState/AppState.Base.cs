﻿//-----------------------------------------------------------------------------
// FILE:	    AppState.Base.cs
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
    public class AppStateBase
    {
        /// <summary>
        /// The <see cref="AppState"/>
        /// </summary>
        public AppState AppState;

        /// <summary>
        /// The <see cref="Service"/>
        /// </summary>
        public Service NeonDashboardService => AppState.NeonDashboardService;

        /// <summary>
        /// The <see cref="IKubernetes"/>
        /// </summary>
        public IKubernetes K8s => NeonDashboardService.Kubernetes;

        /// <summary>
        /// The <see cref="AppState.__Cache"/>
        /// </summary>
        public AppState.__Cache Cache => AppState.Cache;

        /// <summary>
        /// The <see cref="INeonLogger"/>
        /// </summary>
        public INeonLogger Logger => AppState.Logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="state"></param>
        public AppStateBase(AppState state)
        {
            AppState = state;
        }
    }
}