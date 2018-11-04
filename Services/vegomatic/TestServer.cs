//-----------------------------------------------------------------------------
// FILE:	    InstanceIdServer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Hive;
using Neon.Net;

namespace NeonVegomatic
{
    /// <summary>
    /// <para>
    /// Implements a simple web server on <b>port 80</b> that returns a UUDI
    /// for the instance.  This can be used to verify that load balancing
    /// is actually working.
    /// </para>
    /// <para>
    /// Call <see cref="ExecAsync(CommandLine)"/> to start.
    /// </para>
    /// <para>
    /// The server supports the following URI query parameters:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>body=VALUE</b></term>
    ///     <description>
    ///     <para>
    ///     This controls what the server returns.  The current options are:
    ///     </para>
    ///     <list type="table">
    ///         <term><b>server-id</b></term>
    ///         <description>
    ///         Generate a UUID for each instance and then return that as
    ///         the response body.  This is the default mode.
    ///         </description>
    ///     </list>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>delay=SECONDS</b></term>
    ///     <description>
    ///     The time to delay before returning the response.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>expire=SECONDS</b></term>
    ///     <description>
    ///     This will include the <b>Expires</b> header in the response set to the
    ///     specified number of seconds in the future (since the request was received).
    ///     </description>
    /// </item>
    /// </list>
    /// </summary>
    public class TestServer
    {
        //---------------------------------------------------------------------
        // Statics and local types.

        private static Guid     serverId = Guid.NewGuid();

        /// <summary>
        /// Implements the service.
        /// </summary>
        public class Startup
        {
            /// <summary>
            /// Configures the service.
            /// </summary>
            /// <param name="app">The app builder.</param>
            public void Configure(IApplicationBuilder app)
            {
                app.Run(
                    async context =>
                    {
                        var request = context.Request;
                        var body    = "server-id";
                        var delay   = TimeSpan.Zero;
                        var expires = TimeSpan.Zero;

                        // Parse any query parameters.

                        if (request.Query.TryGetValue("mode", out var modeArgs))
                        {
                            body = modeArgs.Single().ToLowerInvariant();
                        }

                        if (request.Query.TryGetValue("delay", out var delayArgs))
                        {
                            var delayArg = delayArgs.Single();

                            if (double.TryParse(delayArg, NumberStyles.Number, CultureInfo.InvariantCulture, out var delayValue) && delayValue > 0)
                            {
                                delay = TimeSpan.FromSeconds(delayValue);
                            }
                        }

                        if (request.Query.TryGetValue("expires", out var expiresArgs))
                        {
                            var expiresArg = expiresArgs.Single();

                            if (double.TryParse(expiresArg, NumberStyles.Number, CultureInfo.InvariantCulture, out var expiresValue) && expiresValue > 0)
                            {
                                expires = TimeSpan.FromSeconds(expiresValue);
                            }
                        }

                        // Implement the operation.

                        if (expires > TimeSpan.Zero)
                        {
                            var expiresDate = DateTime.UtcNow + expires;

                            context.Response.Headers.Add("Cache-Control", "public");
                            context.Response.Headers.Add("Expires", expiresDate.ToString("r"));
                        }

                        if (delay > TimeSpan.Zero)
                        {
                            await Task.Delay(delay);
                        }

                        context.Response.Headers.Add("X-Vegomatic", "true");

                        switch (body)
                        {
                            default:
                            case "server-id":

                                await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(serverId.ToString("D")));
                                break;
                        }
                    });
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Implements a simple Timestamp web server on port 80.
        /// </summary>
        /// <param name="commandLine">The command line arguments will be returned as response headers.</param>
        public async Task ExecAsync(CommandLine commandLine)
        {
            // Start the web server.

            await WebHost.CreateDefaultBuilder()
                .UseStartup<Startup>()
                .UseKestrel()
                .UseUrls("http://0.0.0.0:80")
                .Build()
                .RunAsync();
        }
    }
}
