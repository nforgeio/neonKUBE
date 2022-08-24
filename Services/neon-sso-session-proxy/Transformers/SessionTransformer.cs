//-----------------------------------------------------------------------------
// FILE:	    SessionTransformer.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using System.Text;
using System.Web;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;

using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

namespace NeonSsoSessionProxy
{
    public class SessionTransformer : HttpTransformer
    {
        private IDistributedCache            cache;
        private AesCipher                    cipher;
        private DexClient                    dexClient;
        private string                       dexHost;
        private ILogger                      logger;
        private DistributedCacheEntryOptions cacheOptions;
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cache"></param>
        /// <param name="aesCipher"></param>
        /// <param name="dexClient"></param>
        /// <param name="logger"></param>
        public SessionTransformer(
            IDistributedCache cache,
            AesCipher aesCipher,
            DexClient dexClient,
            ILogger logger,
            DistributedCacheEntryOptions cacheOptions)
        { 
            this.cache        = cache;
            this.cipher       = aesCipher;
            this.dexClient    = dexClient;
            this.dexHost      = dexClient.BaseAddress.Host;
            this.logger       = logger;
            this.cacheOptions = cacheOptions;
        }

        /// <summary>
        /// Transforms the request before sending it upstream.
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="proxyRequest"></param>
        /// <param name="destinationPrefix"></param>
        /// <returns></returns>
        public override async ValueTask TransformRequestAsync(HttpContext httpContext,
                HttpRequestMessage proxyRequest, string destinationPrefix)
        {
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);
        }

        /// <summary>
        /// Transforms the response before returning it to the client. 
        /// 
        /// <para>
        /// This method will add a <see cref="Cookie"/> to each response containing relevant information
        /// about the current authentication flow. It also intercepts redirects from Dex and saves any relevant
        /// tokens to a cache for reuse.
        /// </para>
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="proxyResponse"></param>
        /// <returns></returns>
        public override async ValueTask<bool> TransformResponseAsync(HttpContext httpContext,
                HttpResponseMessage proxyResponse)
        {
            await base.TransformResponseAsync(httpContext, proxyResponse);

            Cookie cookie = null;

            if (httpContext.Request.Cookies.TryGetValue(Service.SessionCookieName, out var requestCookieBase64))
            {
                try
                {
                    logger.LogDebugEx(() => $"Decrypting existing cookie.");
                    cookie = NeonHelper.JsonDeserialize<Cookie>(cipher.DecryptBytesFrom(requestCookieBase64));
                }
                catch (Exception e)
                {
                    logger.LogErrorEx(e);
                    cookie = new Cookie();
                }
            }
            else
            {
                logger.LogDebugEx("Cookie not present.");
                cookie = new Cookie();
            }

            // If we're being redirected, intercept request and save token to cookie.
            if (httpContext.Response.Headers.Location.Count > 0
                && Uri.IsWellFormedUriString(httpContext.Response.Headers.Location.Single(), UriKind.Absolute))
            {
                var location = new Uri(httpContext.Response.Headers.Location.Single());
                var code     = HttpUtility.ParseQueryString(location.Query).Get("code");

                if (!string.IsNullOrEmpty(code))
                {
                    if (cookie != null)
                    {
                        var redirect = cookie.RedirectUri;
                        var token    = await dexClient.GetTokenAsync(cookie.ClientId, code, redirect, "authorization_code");

                        await cache.SetAsync(code, cipher.EncryptToBytes(NeonHelper.JsonSerializeToBytes(token)), cacheOptions);
                        logger.LogDebugEx(NeonHelper.JsonSerialize(token));
                        cookie.TokenResponse = token;

                        httpContext.Response.Cookies.Append(
                            Service.SessionCookieName,
                            cipher.EncryptToBase64(NeonHelper.JsonSerialize(cookie)),
                            new CookieOptions()
                            {
                                Path     = "/",
                                Expires  = DateTime.UtcNow.AddSeconds(token.ExpiresIn.Value).AddMinutes(-60),
                                Secure   = true,
                                SameSite = SameSiteMode.Strict
                            });

                        return true;
                    }
                }
            }

            // Add query parameters to the cookie.
            if (httpContext.Request.Query.TryGetValue("client_id", out var clientId))
            {
                logger.LogDebugEx(() => $"Client ID: [{clientId}]");
                cookie.ClientId = clientId;
            }

            if (httpContext.Request.Query.TryGetValue("state", out var state))
            {
                logger.LogDebugEx(() => $"State: [{state}]");
                cookie.State = state;
            }

            if (httpContext.Request.Query.TryGetValue("redirect_uri", out var redirectUri))
            {
                logger.LogDebugEx(() => $"Redirect Uri: [{redirectUri}]");
                cookie.RedirectUri = redirectUri;
            }

            if (httpContext.Request.Query.TryGetValue("scope", out var scope))
            {
                logger.LogDebugEx(() => $"Scope: [{scope}]");
                cookie.Scope = scope;
            }

            if (httpContext.Request.Query.TryGetValue("response_type", out var responseType))
            {
                logger.LogDebugEx(() => $"Response Type: [{responseType}]");
                cookie.ResponseType = responseType;
            }

            httpContext.Response.Cookies.Append(
                Service.SessionCookieName,
                cipher.EncryptToBase64(NeonHelper.JsonSerialize(cookie)),
                new CookieOptions()
                {
                    Path = "/",
                    Expires = DateTime.UtcNow.AddHours(24),
                    Secure = true,
                    SameSite = SameSiteMode.Strict
                });

            return true;
        }
    }
}
