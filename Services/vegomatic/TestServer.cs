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
    /// Call <see cref="ExecAsync(CommandLine)"/> to start.  The following
    /// command line arguments are supported.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>server-id=VALUE</b></term>
    ///     <description>
    ///     Specifies the <b>server-id</b> for the instance.  A UUID will be generated otherwise.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>expires=SECONDS</b></term>
    ///     <description>
    ///     The default response expiration seconds.  This defaults to <b>0</b>.
    ///     </description>
    /// </item>
    /// </list>
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
    ///     <item>
    ///         <term><b>server-id</b></term>
    ///         <description>
    ///         Generate a UUID for each instance and then return that as
    ///         the response body.  This is the default mode.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>text:VALUE</b></term>
    ///         <description>
    ///         Returns the static text after the colon.
    ///         </description>
    ///     </item>
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

        private static string defaultServerId = Guid.NewGuid().ToString("D").ToLowerInvariant();
        private static double defaultExpires  = 0;

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
                        var request  = context.Request;
                        var bodyMode = "server-id";
                        var bodyText = string.Empty;
                        var delay    = TimeSpan.Zero;
                        var expires  = TimeSpan.FromSeconds(defaultExpires);

                        // Parse any query parameters.

                        if (request.Query.TryGetValue("body", out var bodyArg))
                        {
                            bodyMode = bodyArg.Single().ToLowerInvariant();

                            if (bodyMode.StartsWith("text:"))
                            {
                                bodyText = bodyMode.Substring("text:".Length);
                                bodyMode = "text";
                            }
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

                        switch (bodyMode)
                        {
                            case "text":

                                await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(bodyText));
                                break;

                            default:
                            case "server-id":

                                await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(defaultServerId));
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
            // Process the command line arguments.

            foreach (var arg in commandLine.Arguments)
            {
                var fields = arg.Split(new char[] { '=' }, 2);

                if (fields.Length != 2)
                {
                    Console.WriteLine($"Invalid argument: [{arg}].  [NAME=VALUE] expected.");
                    Program.Exit(1);
                }

                var name  = fields[0].ToLowerInvariant();
                var value = fields[1];

                switch (name)
                {
                    case "server-id":

                        if (!string.IsNullOrEmpty(value))
                        {
                            defaultServerId = value;
                        }
                        break;

                    case "expires":

                        if (!double.TryParse(value, out defaultExpires) || defaultExpires < 0.0)
                        {
                            Console.WriteLine($"Invalid expiration seconds: [{arg}]");
                            Program.Exit(1);
                        }
                        break;

                    default:

                        Console.WriteLine($"Unexpected commandline argument: [{arg}]");
                        Program.Exit(1);
                        break;
                }
            }

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
