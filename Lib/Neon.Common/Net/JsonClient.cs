//-----------------------------------------------------------------------------
// FILE:	    JsonClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Collections;
using Neon.Diagnostics;
using Neon.Retry;

namespace Neon.Net
{
    /// <summary>
    /// Implements a light-weight JSON oriented HTTP client.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use <see cref="GetAsync(string, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>, 
    /// <see cref="PutAsync(string, object, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>, 
    /// <see cref="PostAsync(string, object, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>, 
    /// <see cref="DeleteAsync(string, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>,
    /// <see cref="OptionsAsync(string, object, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>,
    /// <see cref="HeadAsync(string, object, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>, or
    /// <see cref="PatchAsync(string, object, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>
    /// to perform HTTP operations that ensure that a non-error HTTP status code is returned by the servers.
    /// </para>
    /// <para>
    /// Use <see cref="GetUnsafeAsync(string, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>, 
    /// <see cref="PutUnsafeAsync(string, object, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>, 
    /// <see cref="PostUnsafeAsync(string, object, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>, 
    /// <see cref="DeleteUnsafeAsync(string, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>,
    /// <see cref="OptionsUnsafeAsync(string, object, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>,
    /// <see cref="HeadUnsafeAsync(string, object, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>, or
    /// <see cref="PatchUnsafeAsync(string, object, ArgDictionary, ArgDictionary, CancellationToken, LogActivity)"/>
    /// to perform an HTTP without ensuring a non-error HTTP status code.
    /// </para>
    /// <para>
    /// This class can also handle retrying operations when transient errors are detected.  Customize 
    /// <see cref="SafeRetryPolicy"/> and/or <see cref="UnsafeRetryPolicy"/> by setting a <see cref="IRetryPolicy"/> 
    /// implementation such as <see cref="LinearRetryPolicy"/> or <see cref="ExponentialRetryPolicy"/>.
    /// </para>
    /// <note>
    /// This class initializes <see cref="SafeRetryPolicy"/> to a reasonable <see cref="ExponentialRetryPolicy"/> by default
    /// and <see cref="UnsafeRetryPolicy"/> to <see cref="NoRetryPolicy"/>.  You can override the default
    /// retry policy for specific requests using the methods that take an <see cref="IRetryPolicy"/> as 
    /// their first parameter.
    /// </note>
    /// <note>
    /// <para>
    /// The <see cref="JsonClientPayload"/> class can be used to customize both the <b>Content-Type</b> header
    /// and the actual payload uploaded with <b>POST</b> and <b>PUT</b> requests.  This can be used for those
    /// <i>special</i> REST APIs that don't accept JSON payloads.
    /// </para>
    /// <para>
    /// All you need to do is construct a <see cref="JsonClientPayload"/> instance, specifying the value to
    /// be used as the <b>Content-Type</b> header and the payload data as text or a byte array and then pass
    /// this as the <b>document</b> parameter to the methods that upload content.  The methods will recognize
    /// this special type and just send the specified data rather than attempting to serailize the document
    /// to JSON.
    /// </para>
    /// </note>
    /// </remarks>
    public partial class JsonClient : IDisposable
    {
        //---------------------------------------------------------------------
        // Instance members

        private object          syncLock          = new object();
        private IRetryPolicy    safeRetryPolicy   = new ExponentialRetryPolicy(TransientDetector.NetworkOrHttp);
        private IRetryPolicy    unsafeRetryPolicy = new NoRetryPolicy();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="handler">The optional message handler.</param>
        /// <param name="disposeHandler">Indicates whether the handler passed will be disposed automatically (defaults to <c>false</c>).</param>
        public JsonClient(HttpMessageHandler handler = null, bool disposeHandler = false)
        {
            if (handler == null)
            {
                handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
                };

                disposeHandler = true;  // Always dispose handlers created by the constructor.
            }

            HttpClient = new HttpClient(handler, disposeHandler);

            HttpClient.DefaultRequestHeaders.Add("Accept", DocumentType);
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~JsonClient()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (syncLock)
                {
                    if (HttpClient != null)
                    {
                        HttpClient.Dispose();
                        HttpClient = null;
                    }
                }

                GC.SuppressFinalize(this);
            }

            HttpClient = null;
        }

        /// <summary>
        /// The default base <see cref="Uri"/> the client will use when relative
        /// URIs are specified for requests.
        /// </summary>
        public Uri BaseAddress
        {
            get { return HttpClient.BaseAddress; }
            set { HttpClient.BaseAddress = value; }
        }

        /// <summary>
        /// The default base <see cref="Uri"/> the client will use when relative
        /// URIs are specified for requests.
        /// </summary>
        public TimeSpan Timeout
        {
            get { return HttpClient.Timeout; }
            set { HttpClient.Timeout = value; }
        }

        /// <summary>
        /// Returns the base client's default request headers property to make it easy
        /// to customize request headers.
        /// </summary>
        public HttpRequestHeaders DefaultRequestHeaders => HttpClient.DefaultRequestHeaders;

        /// <summary>
        /// Specifies the MIME type to use posting or putting documents to the endpoint.
        /// This defaults to the standard <b>application/json</b> but some services
        /// may require custom values.
        /// </summary>
        public string DocumentType { get; set; } = "application/json";

        /// <summary>
        /// Returns the underlying <see cref="System.Net.Http.HttpClient"/>.
        /// </summary>
        public HttpClient HttpClient { get; private set; }

        /// <summary>
        /// <para>
        /// The <see cref="IRetryPolicy"/> to be used to detect and retry transient network and HTTP
        /// errors for the <b>safe</b> methods.  This defaults to <see cref="ExponentialRetryPolicy"/> with 
        /// the transient detector function set to <see cref="TransientDetector.NetworkOrHttp(Exception)"/>.
        /// </para>
        /// <note>
        /// You may set this to <c>null</c> to disable safe transient error retry.
        /// </note>
        /// </summary>
        public IRetryPolicy SafeRetryPolicy
        {
            get { return safeRetryPolicy; }
            set { safeRetryPolicy = value ?? NoRetryPolicy.Instance; }
        }

        /// <summary>
        /// <para>
        /// The <see cref="IRetryPolicy"/> to be used to detect and retry transient network errors for the
        /// <b>unsafe</b> methods.  This defaults to <see cref="NoRetryPolicy"/>.
        /// </para>
        /// <note>
        /// You may set this to <c>null</c> to disable unsafe transient error retry.
        /// </note>
        /// </summary>
        public IRetryPolicy UnsafeRetryPolicy
        {
            get { return unsafeRetryPolicy; }
            set { unsafeRetryPolicy = value ?? NoRetryPolicy.Instance; }
        }

        /// <summary>
        /// Converts a relative URI into an absolute URI if necessary.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The absolute URI.</returns>
        private string AbsoluteUri(string uri)
        {
            if (string.IsNullOrEmpty(uri) || uri[0] == '/')
            {
                return new Uri(BaseAddress, uri).ToString();
            }
            else
            {
                return uri;
            }
        }

        /// <summary>
        /// Formats the URI by appending query arguments as required.
        /// </summary>
        /// <param name="uri">The base URI.</param>
        /// <param name="args">The query arguments.</param>
        /// <returns>The formatted URI.</returns>
        private string FormatUri(string uri, ArgDictionary args)
        {
            if (args == null || args.Count == 0)
            {
                return AbsoluteUri(uri);
            }

            var sb    = new StringBuilder(uri);
            var first = true;

            foreach (var arg in args)
            {
                if (first)
                {
                    sb.Append('?');
                    first = false;
                }
                else
                {
                    sb.Append('&');
                }

                string value;

                if (arg.Value == null)
                {
                    // Don't serialize NULL values.  Their absence
                    // will indicate their NULL-ness.

                    continue;
                }
                else if (arg.Value is bool)
                {
                    value = NeonHelper.ToBoolString((bool)arg.Value);
                }
                else
                {
                    value = arg.Value.ToString();
                }

                sb.Append($"{arg.Key}={Uri.EscapeDataString(value)}");
            }

            return AbsoluteUri(sb.ToString());
        }

        /// <summary>
        /// <para>
        /// Converts the object passed into JSON content suitable for transmitting in
        /// an HTTP request.
        /// </para>
        /// <note>
        /// This method handles <see cref="JsonClientPayload"/> documents specially 
        /// as described in the <see cref="JsonClient"/> remarks.
        /// </note>
        /// </summary>
        /// <param name="document">The document object or JSON text.</param>
        /// <returns>Tne <see cref="HttpContent"/>.</returns>
        private HttpContent CreateContent(object document)
        {
            if (document == null)
            {
                return null;
            }

            var custom = document as JsonClientPayload;

            if (custom != null)
            {
                var content = new ByteArrayContent(custom.ContentBytes);

                content.Headers.ContentType = new MediaTypeHeaderValue(custom.ContentType);

                return content;
            }
            else
            {
                var json = document as string;

                if (json != null)
                {
                    var jObject = document as JObject;

                    if (jObject != null)
                    {
                        json = jObject.ToString(Formatting.None);
                    }
                }

                return new StringContent(json ?? NeonHelper.JsonSerialize(document), Encoding.UTF8, DocumentType);
            }
        }
    }
}
