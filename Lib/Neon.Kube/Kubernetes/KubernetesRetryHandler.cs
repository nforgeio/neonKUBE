//-----------------------------------------------------------------------------
// FILE:	    KubernetesRetryHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
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

        /// <summary>
        /// Implements default transient error detection.
        /// </summary>
        /// <param name="exception">Specifies the exception being tested.</param>
        /// <returns><c>true</c> for exceptions considered to be transient.</returns>
        public static bool DefaultTransientDetector(Exception exception)
        {
            Covenant.Requires<ArgumentNullException>(exception != null, nameof(exception));

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
        }

        /// <summary>
        /// Returns the default <see cref="KubernetesRetryHandler"/>'s retry policy.  This policy is
        /// relatively conservative and will retry operations for up to <b>120 seconds</b>.  For situations
        /// where you need to see errors before that, use <see cref="AggressiveRetryPolicy"/> or construct
        /// your own retry policy using <see cref="DefaultTransientDetector(Exception)"/> (if you wish)
        /// use that when constructing a <see cref="KubernetesRetryHandler"/>.
        /// </summary>
        public static IRetryPolicy DefaultRetryPolicy =
            new ExponentialRetryPolicy(
                transientDetector:    DefaultTransientDetector,
                initialRetryInterval: TimeSpan.FromSeconds(1),
                maxRetryInterval:     TimeSpan.FromSeconds(10),
                timeout:              TimeSpan.FromSeconds(120));

        /// <summary>
        /// Returns a more aggressive <see cref="KubernetesRetryHandler"/> retry policy.  This policy
        /// will retry operations for up to <b>15 seconds</b>.
        /// </summary>
        public static IRetryPolicy AggressiveRetryPolicy =
            new ExponentialRetryPolicy(
                transientDetector:    DefaultTransientDetector,
                initialRetryInterval: TimeSpan.FromSeconds(1),
                maxRetryInterval:     TimeSpan.FromSeconds(5),
                timeout:              TimeSpan.FromSeconds(15));

        //---------------------------------------------------------------------
        // Instance members

        private IRetryPolicy retryPolicy;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="retryPolicy">Optionally specifies a retry policy that overrides <see cref="DefaultRetryPolicy"/>.</param>
        public KubernetesRetryHandler(IRetryPolicy retryPolicy = null)
            : base()
        {
            this.retryPolicy = retryPolicy ?? DefaultRetryPolicy;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="innerHandler">Specifies an overriding HTTP handler.</param>
        /// <param name="retryPolicy">Optionally specifies a retry policy that overrides <see cref="DefaultRetryPolicy"/>.</param>
        public KubernetesRetryHandler(HttpMessageHandler innerHandler, IRetryPolicy retryPolicy = null)
            : base(innerHandler)
        {
            this.retryPolicy = retryPolicy ?? DefaultRetryPolicy;
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

        /// <summary>
        /// Creates a new exception with additional message details.
        /// </summary>
        /// <param name="e">The exception being augmented.</param>
        /// <returns>The new augmented exception.</returns>
        private static HttpOperationException GetEnhancedHttpOperationException(HttpOperationException e)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            return new HttpOperationException($"{e.Message}\r\n\r\nRESPONSE.CONTENT:\r\n\r\n{e.Response.Content}", e.InnerException)
            {
                Response = e.Response
            };
        }
    }
}
