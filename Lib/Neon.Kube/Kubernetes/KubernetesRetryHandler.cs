//-----------------------------------------------------------------------------
// FILE:	    KubernetesRetryHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Autorest;
using Neon.Retry;

namespace Neon.Kube
{
    /// <summary>
    /// A <see cref="DelegatingHandler"/> optionally used to retry transient errors encountered
    /// by <see cref="Kubernetes"/> clients.
    /// </summary>
    public class KubernetesRetryHandler : DelegatingHandler
    {
        //---------------------------------------------------------------------
        // Static members

        private static IRetryPolicy defaultRetryPolicy = new ExponentialRetryPolicy(
            transientDetector:
                exception =>
                {
                    var exceptionType = exception.GetType();

                    // Exceptions like this happen when a API server connection can't be established
                    // due to the server not running or ready.

                    if (exceptionType == typeof(HttpRequestException) && exception.InnerException != null && exception.InnerException.GetType() == typeof(SocketException))
                    {
                        return true;
                    }

                    var httpOperationException = exception as HttpOperationException;

                    if (httpOperationException != null)
                    {
                        var statusCode = httpOperationException.Response.StatusCode;

                        switch (statusCode)
                        {
                            case HttpStatusCode.GatewayTimeout:
                            case HttpStatusCode.InternalServerError:
                            case HttpStatusCode.RequestTimeout:
                            case HttpStatusCode.ServiceUnavailable:
                            case (HttpStatusCode)423:   // Locked
                            case (HttpStatusCode)429:   // Too many requests

                                return true;

                        }
                    }

                    // This might be another variant of the check just above.  This looks like an SSL negotiation problem.

                    if (exceptionType == typeof(HttpRequestException) && exception.InnerException != null && exception.InnerException.GetType() == typeof(IOException))
                    {
                        return true;
                    }

                    return false;
                },
                maxAttempts:          int.MaxValue,
                initialRetryInterval: TimeSpan.FromSeconds(1),
                maxRetryInterval:     TimeSpan.FromSeconds(5),
                timeout:              TimeSpan.FromMinutes(5));

        //---------------------------------------------------------------------
        // Instance members

        private IRetryPolicy retryPolicy;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="retryPolicy"></param>
        public KubernetesRetryHandler(IRetryPolicy retryPolicy = null)
            : base()
        {
            this.retryPolicy = retryPolicy ?? defaultRetryPolicy;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="retryPolicy"></param>
        /// <param name="innerHandler"></param>
        public KubernetesRetryHandler(IRetryPolicy retryPolicy, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            this.retryPolicy = retryPolicy ?? defaultRetryPolicy;
        }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return await retryPolicy.InvokeAsync(
                async () =>
                {
                    try
                    {
                        var result = await base.SendAsync(request, cancellationToken);
                        
                        if (result.IsSuccessStatusCode)
                        {
                            return result;
                        }
                        else
                        {
                            var content        = await result.Content.ReadAsStringAsync() ?? null;
                            var requestContent = (string)null;

                            if (request.Content != null)
                            {
                                requestContent = await request.Content.ReadAsStringAsync();
                            }
                            throw new HttpOperationException()
                            {
                                Body     = content,
                                Request  = new HttpRequestMessageWrapper(request, requestContent),
                                Response = new HttpResponseMessageWrapper(result, content)
                            };
                        }
                    }
                    catch (HttpOperationException e)
                    {
                        throw GetEnhancedHttpOperationException(e);
                    }
                });
        }

        private static HttpOperationException GetEnhancedHttpOperationException(HttpOperationException e)
        {
            return new HttpOperationException($"{e.Message}\r\n\r\nRESPONSE.CONTENT:\r\n\r\n{e.Response.Content}", e.InnerException)
            {
                Response = e.Response
            };
        }
    }
}
