//-----------------------------------------------------------------------------
// FILE:	    MockHttpServer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Net.Http.Server;

using Neon.Common;

namespace Neon.Xunit
{
    /// <summary>
    /// <para>
    /// Implements a very lightweight HTTP server suitable for locally
    /// mocking a service.
    /// </para>
    /// <note>
    /// This currently runs only on Windows.
    /// </note>
    /// </summary>
    /// <threadsafety instance="true"/>
    public sealed class MockHttpServer : IDisposable
    {
        private object                  syncLock = new object();
        private WebListener             listener;
        private Action<RequestContext>  handler;

        /// <summary>
        /// Constuctor.
        /// </summary>
        /// <param name="urlPrefix">Specifies the URL prefixes to be served.</param>
        /// <param name="handler">The custom request handler.</param>
        public MockHttpServer(string urlPrefix, Action<RequestContext> handler)
        {
            Covenant.Requires<ArgumentNullException>(urlPrefix != null);
            Covenant.Requires<ArgumentNullException>(handler != null);

            if (!NeonHelper.IsWindows)
            {
                throw new NotSupportedException($"[{nameof(MockHttpServer)}] works only on Windows.");
            }

            var settings = new WebListenerSettings();

            settings.UrlPrefixes.Add(urlPrefix);

            this.handler  = handler;
            this.listener = new WebListener(settings);
            this.listener.Start();

            // Handle received requests in a background task.

            Task.Run(() => RequestProcessor());
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (syncLock)
            {
                if (listener != null)
                {
                    listener.Dispose();
                    listener = null;
                }
            }
        }

        /// <summary>
        /// Handles received requests.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task RequestProcessor()
        {
            while (true)
            {
                try
                {
                    var context = await listener.AcceptAsync();

                    var task = Task.Run(
                        () =>
                        {
                            using (var contextCopy = context)
                            {
                                handler(contextCopy);
                            }
                        });
                }
                catch (ObjectDisposedException)
                {
                    return; // We're going to use this as the signal to stop.
                }
            }
        }
    }

    /// <summary>
    /// Misc extsnsions.
    /// </summary>
    public static class MockHttpServerExtensions
    {
        //---------------------------------------------------------------------
        // Request

        /// <summary>
        /// Returns the value of a request query argument.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="name">The query argument name.</param>
        /// <returns>The argument value or <c>null</c>.</returns>
        public static string QueryGet(this Request request, string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            if (string.IsNullOrEmpty(request.QueryString))
            {
                return null;
            }

            var queryString = request.QueryString; ;

            if (queryString[0] == '?')
            {
                queryString = request.QueryString.Substring(1);
            }

            var args = queryString.Split('&');

            foreach (var arg in args)
            {
                if (arg.StartsWith(name + "=", StringComparison.InvariantCultureIgnoreCase))
                {
                    return arg.Substring(name.Length + 1);
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a request body payload as text.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The body text.</returns>
        public static string GetBodyText(this Request request)
        {
            return new StreamReader(request.Body).ReadToEnd();
        }

        //---------------------------------------------------------------------
        // Response 

        /// <summary>
        /// Writes bytes to an HTTP response.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="bytes">The bytes.</param>
        public static void Write(this Response response, byte[] bytes)
        {
            response.Body.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Writes bytes to an HTTP response.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="bytes">The bytes.</param>
        /// <param name="offset">The offset of the first byte to write.</param>
        /// <param name="count">The number of bytes to be written.</param>
        public static void Write(this Response response, byte[] bytes, int offset, int count)
        {
            response.Body.Write(bytes, offset, count);
        }

        /// <summary>
        /// Writes a string to an HTTP response using UTF-8 encoding.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="text">The text to be written.</param>
        public static void Write(this Response response, string text)
        {
            response.Write(Encoding.UTF8.GetBytes(text));
        }
    }
}
