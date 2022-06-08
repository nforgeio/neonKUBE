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
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Tasks;

using NeonDashboard.Shared;
using NeonDashboard.Shared.Components;

using Blazor.Analytics;
using Blazor.Analytics.Components;

using Blazored.LocalStorage;

using k8s;
using k8s.Models;

namespace NeonDashboard
{
    public partial class AppState 
    {
        public class __Cache : AppStateBase
        {
            private static DistributedCacheEntryOptions cacheEntryOptions;
            private IDistributedCache cache => AppState.DistributedCache;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="state"></param>
            public __Cache(AppState state)
                : base(state)
            {
                cacheEntryOptions = new DistributedCacheEntryOptions()
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                };
            }

            public async Task SetAsync(string key, object value)
            {
                await SyncContext.Clear;

                await cache.SetAsync(key, NeonHelper.JsonSerializeToBytes(value), cacheEntryOptions);
            }

            public string CreateKey(string key)
            {
                return $"neon-dashboard_{key}";
            }

            public async Task<T> GetAsync<T>(string key)
            {
                await SyncContext.Clear;
                
                var value = await cache.GetAsync(key);

                if (value != null)
                {
                    return NeonHelper.JsonDeserialize<T>(value);
                }

                return default;
            }
        }
    }
}