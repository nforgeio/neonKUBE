//-----------------------------------------------------------------------------
// FILE:	    TimestampServer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
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
    /// Implements a simple web server on <b>port 80</b> that returns the current 
    /// time (UTC) formatted like: <b>2018-06-05T14:30:13.000Z</b>.  This is used 
    /// for testing services based on the <b>neon-proxy-cache</b> image to verify 
    /// that Varnish is actually working.
    /// </para>
    /// <para>
    /// Call <see cref="ExecAsync(CommandLine)"/> to start, optionally passing 
    /// any HTTP headers you wish to add to the response like:
    /// </para>
    /// <example>
    /// "Expires: Wed, 21 Oct 2015 07:28:00 GMT"
    /// </example>
    /// </summary>
    public class TimestampServer
    {
        //---------------------------------------------------------------------
        // Statics and local types.

        private static List<string>     headers = new List<string>();

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
                        var response  = context.Response;
                        var timestamp = DateTime.UtcNow.ToString(NeonHelper.DateFormatTZ);

                        foreach (var header in headers)
                        {
                            var fields = header.Split(new char[] { ':' }, 2);

                            if (fields.Length == 2)
                            {
                                response.Headers.Add(fields[0].Trim(), fields[1].Trim());
                            }
                        }

                        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(timestamp));
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
            headers.Clear();
            foreach (var arg in commandLine.Arguments)
            {
                headers.Add(arg);
            }

            await WebHost.CreateDefaultBuilder()
                .UseStartup<Startup>()
                .UseKestrel()
                .UseUrls("http://0.0.0.0:80")
                .Build()
                .RunAsync();
        }
    }
}
