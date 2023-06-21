//-----------------------------------------------------------------------------
// FILE:        AuthController.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright � 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.CommandLine.Help;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Service;
using Neon.Tasks;
using Neon.Web;

using Newtonsoft.Json;

using Yarp;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Forwarder;

namespace NeonSsoSessionProxy.Controllers
{
    /// <summary>
    /// IMplements authorization.
    /// </summary>
    [ApiController]
    public class AuthController : NeonControllerBase
    {
        private Service                         NeonSsoSessionProxyService;
        private HttpMessageInvoker              httpClient;
        private IHttpForwarder                  forwarder;
        private SessionTransformer              transformer;
        private IDistributedCache               cache;
        private AesCipher                       cipher;
        private DexClient                       dexClient;
        private DistributedCacheEntryOptions    cacheOptions;
        private ForwarderRequestConfig          forwarderRequestConfig;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="NeonSsoSessionProxyService"></param>
        /// <param name="httpClient"></param>
        /// <param name="forwarder"></param>
        /// <param name="cache"></param>
        /// <param name="aesCipher"></param>
        /// <param name="dexClient"></param>
        /// <param name="sessionTransformer"></param>
        /// <param name="cacheOptions"></param>
        /// <param name="forwarderRequestConfig"></param>
        public AuthController(
            Service   NeonSsoSessionProxyService,
            HttpMessageInvoker           httpClient,
            IHttpForwarder               forwarder,
            IDistributedCache            cache,
            AesCipher                    aesCipher,
            DexClient                    dexClient,
            SessionTransformer           sessionTransformer,
            DistributedCacheEntryOptions cacheOptions,
            ForwarderRequestConfig       forwarderRequestConfig
            )
        {
            this.NeonSsoSessionProxyService = NeonSsoSessionProxyService;
            this.httpClient                 = httpClient;
            this.forwarder                  = forwarder;
            this.cache                      = cache;
            this.cipher                     = aesCipher;    
            this.transformer                = sessionTransformer;
            this.dexClient                  = dexClient;
            this.cacheOptions               = cacheOptions;
            this.forwarderRequestConfig     = forwarderRequestConfig;
        }

        /// <summary>
        /// Catchall method responsible for forwarding requests to Dex.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        [Route("{**catchAll}")]
        public async Task CatchAllAsync()
        {
            Logger.LogDebugEx(() => $"Processing catch-all request");

            var error = await forwarder.SendAsync(HttpContext, $"http://{KubeService.Dex}:5556", httpClient, forwarderRequestConfig, transformer);

            if (error != ForwarderError.None)
            {
                var errorFeature = HttpContext.GetForwarderErrorFeature();
                var exception    = errorFeature.Exception;

                Logger.LogErrorEx(exception, "CatchAll");
            }
        }

        /// <summary>
        /// Token request endpoint. Returns the token from cache with given code.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="grant_type"></param>
        [Route("/token")]
        public async Task TokenAsync([FromForm] string code, [FromForm] string grant_type)
        {
            Logger.LogDebugEx(() => $"Processing request for code: [{code}]");

            if (grant_type == "refresh_token")
            {
                var error = await forwarder.SendAsync(
                HttpContext,
                NeonSsoSessionProxyService.DexUri.ToString(),
                httpClient,
                forwarderRequestConfig,
                transformer);

                if (error != ForwarderError.None)
                {
                    var errorFeature = HttpContext.GetForwarderErrorFeature();
                    var exception    = errorFeature.Exception;

                    Logger.LogErrorEx(exception, "Token");
                }

                return;
            }
            else
            {
                HttpContext.Response.ContentType = "application/json";
                
                var response = cipher.DecryptBytesFrom(await cache.GetAsync(code));

                _ = cache.RemoveAsync(code);

                await HttpContext.Response.BodyWriter.WriteAsync(response);

                return;
            }

            
        }
    }
}
