//-----------------------------------------------------------------------------
// FILE:	    AppState.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
        /// <summary>
        /// Handles the Cache.
        /// </summary>
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

            /// <summary>
            /// Generate a cache key.
            /// </summary>
            /// <param name="key"></param>
            /// <returns></returns>
            public string CreateKey(string key)
            {
                return $"neon-dashboard_{key}";
            }

            /// <summary>
            /// Add an object to the cache.
            /// </summary>
            /// <param name="key"></param>
            /// <param name="value"></param>
            /// <returns></returns>
            public async Task SetAsync(string key, object value)
            {
                await SyncContext.Clear;

                await cache.SetAsync(key, NeonHelper.JsonSerializeToBytes(value), cacheEntryOptions);
            }

            /// <summary>
            /// Get a generic object from the cache.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="key"></param>
            /// <returns></returns>
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