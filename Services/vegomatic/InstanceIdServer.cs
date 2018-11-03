//-----------------------------------------------------------------------------
// FILE:	    InstanceIdServer.cs
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
    /// Implements a simple web server on <b>port 80</b> that returns a UUDI
    /// for the instance.  This can be used to verify that load balancing
    /// is actually working.
    /// </para>
    /// <para>
    /// Call <see cref="ExecAsync(CommandLine)"/> to start.  You may optionally
    /// specify the following options by passing <b>OPTION=VALUE</b> as 
    /// command line arguments.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>expire-seconds=SECONDS</b></term>
    ///     <description>
    ///     This will include the <b>Expires</b> header in the response set to the
    ///     specified number of seconds in the future (since the request was received).
    ///     </description>
    /// </item>
    /// </list>
    /// </summary>
    public class InstanceIdServer
    {
        //---------------------------------------------------------------------
        // Statics and local types.

        private static Guid         instanceId = Guid.NewGuid();
        private static TimeSpan     expires    = TimeSpan.Zero;

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
                        if (expires > TimeSpan.Zero)
                        {
                            var expiresDate = DateTime.UtcNow + expires;

                            context.Response.Headers.Add("Cache-Control", "public");
                            context.Response.Headers.Add("Expires", expiresDate.ToString("r"));
                        }

                        context.Response.Headers.Add("X-Vegomatic", "true");

                        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(instanceId.ToString("D")));
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
            // Process the command line.  We're going to ignore anything that
            // doesn't make sense.

            foreach (var arg in commandLine.Arguments)
            {
                var fields = arg.Split(new char[] { '=' }, 2);

                if (fields.Length != 2)
                {
                    continue;
                }

                switch (fields[0].ToLowerInvariant())
                {
                    case "expire-seconds":

                        if (double.TryParse(fields[1], out var expiresArg) && expiresArg > 0.0)
                        {
                            expires = TimeSpan.FromSeconds(expiresArg);
                        }
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
