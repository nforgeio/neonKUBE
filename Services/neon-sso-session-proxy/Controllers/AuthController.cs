//-----------------------------------------------------------------------------
// FILE:	    AuthController.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;
using Neon.Service;
using Neon.Web;
using Newtonsoft.Json;
using Yarp;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Forwarder;

namespace NeonSsoSessionProxy.Controllers
{
    [ApiController]
    public class AuthController : NeonControllerBase
    {
        private NeonSsoSessionProxyService NeonSsoSessionProxyService;
        private HttpMessageInvoker  httpClient;
        private IHttpForwarder      forwarder;
        private SessionTransformer   transformer;
        private IDistributedCache   cache;
        private AesCipher           cipher;
        private DexClient           dexClient;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="NeonSsoSessionProxyService"></param>
        /// <param name="httpClient"></param>
        /// <param name="forwarder"></param>
        /// <param name="cache"></param>
        /// <param name="aesCipher"></param>
        /// <param name="dexClient"></param>
        public AuthController(
            NeonSsoSessionProxyService NeonSsoSessionProxyService,
            HttpMessageInvoker  httpClient,
            IHttpForwarder      forwarder,
            IDistributedCache   cache,
            AesCipher           aesCipher,
            DexClient           dexClient)
        {
            this.NeonSsoSessionProxyService = NeonSsoSessionProxyService;
            this.httpClient          = httpClient;
            this.forwarder           = forwarder;
            this.cache               = cache;
            this.cipher              = aesCipher;    
            this.transformer         = new SessionTransformer(cache, aesCipher, dexClient,NeonSsoSessionProxyService.Log);
            this.dexClient           = dexClient;
        }

        /// <summary>
        /// Catchall method responsible for forwarding requests to Dex.
        /// </summary>
        /// <returns></returns>
        [Route("{**catchAll}")]
        public async Task CatchAllAsync()
        {
            var error = await forwarder.SendAsync(
                HttpContext, 
                NeonSsoSessionProxyService.ServiceMap[KubeService.Dex].Endpoints.Default.Uri.ToString(), 
                httpClient, new ForwarderRequestConfig(), 
                transformer);

            if (error != ForwarderError.None)
            {
                var errorFeature = HttpContext.GetForwarderErrorFeature();
                var exception = errorFeature.Exception;
                LogError(exception);
            }
        }
        
        /// <summary>
        /// Token request endpoint. Returns the token from cache with given code.
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        [Route("/token")]
        public async Task<ActionResult<TokenResponse>> TokenAsync([FromForm] string code)
        {
            LogDebug($"Processing request for code: [{code}]");
            var responseJson = NeonHelper.JsonDeserialize<TokenResponse>(cipher.DecryptBytesFrom(await cache.GetAsync(code)));
            
            _ = cache.RemoveAsync(code);

            return Ok(responseJson);
        }
    }
}