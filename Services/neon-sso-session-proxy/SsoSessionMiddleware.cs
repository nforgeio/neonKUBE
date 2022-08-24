//-----------------------------------------------------------------------------
// FILE:	    SsoSessionMiddleware.cs
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Tasks;
using Neon.Web;

namespace NeonSsoSessionProxy
{
    public class SsoSessionMiddleware
    {
        private readonly RequestDelegate _next;
        public SsoSessionMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>
        /// <para>
        /// Entrypoint called as part of the request pipeline.
        /// </para>
        /// <para>
        /// This method is responsible for intercepting token requests from clients.
        /// If the client has a valid cookie with a token response in it, we save the 
        /// token to cache and redirect them back with a code referencing the token in 
        /// the cache.
        /// </para>
        /// </summary>
        public async Task InvokeAsync(
                HttpContext                     context,
                Service                         NeonSsoSessionProxyService,
                IDistributedCache               cache, 
                AesCipher                       cipher,
                DistributedCacheEntryOptions    cacheOptions,
                ILogger                         logger)
        {
            try
            {
                if (context.Request.Cookies.TryGetValue(Service.SessionCookieName, out var requestCookieBase64))
                {
                    var requestCookie = NeonHelper.JsonDeserialize<Cookie>(cipher.DecryptBytesFrom(requestCookieBase64));

                    if (requestCookie.TokenResponse != null)
                    {
                        var code = NeonHelper.GetCryptoRandomPassword(10);
                        await cache.SetAsync(code, cipher.EncryptToBytes(NeonHelper.JsonSerializeToBytes(requestCookie.TokenResponse)), cacheOptions);

                        var query = new Dictionary<string, string>()
                        {
                            { "code", code }
                        };

                        if (context.Request.Query.TryGetValue("state", out var state))
                        {
                            query["state"] = state;
                        }

                        if (context.Request.Query.TryGetValue("redirect_uri", out var redirectUri))
                        {
                            if (context.Request.Query.TryGetValue("client_id", out var clientId))
                            {
                                if (!NeonSsoSessionProxyService.Config.StaticClients.Where(client => client.Id == clientId).First().RedirectUris.Contains(redirectUri))
                                {
                                    logger.LogErrorEx("Invalid redirect URI");

                                    throw new HttpRequestException("Invalid redirect URI.");
                                }

                                context.Response.StatusCode       = StatusCodes.Status302Found;
                                context.Response.Headers.Location = QueryHelpers.AddQueryString(redirectUri, query);

                                logger.LogDebugEx(() => $"Client and Redirect URI confirmed. [ClientID={clientId}] [RedirectUri={redirectUri}]");
                                return;
                            }
                            else
                            {
                                logger.LogErrorEx("No Client ID specified.");

                                throw new HttpRequestException("Invalid Client ID.");
                            }
                        }
                        else
                        {
                          throw new HttpRequestException("No redirect_uri specified.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                NeonSsoSessionProxyService.Logger.LogErrorEx(e);
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Helper method to add this middleware.
    /// </summary>
    public static class SsoSessionMiddlewareHelper
    {
        public static IApplicationBuilder UseSsoSessionMiddleware(
        this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SsoSessionMiddleware>();
        }
    }
}