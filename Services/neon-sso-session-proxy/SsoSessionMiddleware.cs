//-----------------------------------------------------------------------------
// FILE:	    SsoSessionMiddleware.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

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
            INeonLogger                     logger)
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

                        logger.LogDebug($"Request Query: [{NeonHelper.JsonSerialize(context.Request.Query)}]");

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
                            if (context.Request.Query.TryGetValue("client_id", out var client_id))
                            {
                                if (!NeonSsoSessionProxyService.Config.StaticClients.Where(client => client.Id == client_id).First().RedirectUris.Contains(redirectUri))
                                {
                                    logger.LogError("Invalid redirect URI");

                                    throw new HttpRequestException("Invalid redirect URI.");
                                }
                                
                                context.Response.StatusCode = StatusCodes.Status302Found;
                                context.Response.Headers.Location = QueryHelpers.AddQueryString(redirectUri, query);

                                logger.LogDebug($"Client and Redirect URI confirmed.");
                            }
                            else
                            {
                                logger.LogError("No Client ID specified.");

                                throw new HttpRequestException("Invalid Client ID.");
                            }
                            
                            return;
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
                NeonSsoSessionProxyService.Log.LogError(e);
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
