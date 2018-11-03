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
    /// Call <see cref="ExecAsync(CommandLine)"/> to start, optionally passing 
    /// any HTTP headers you wish to add to the response like:
    /// </para>
    /// <example>
    /// "Expires: Wed, 21 Oct 2015 07:28:00 GMT"
    /// </example>
    /// </summary>
    public class InstanceIdServer
    {
        //---------------------------------------------------------------------
        // Statics and local types.

        private static Guid instanceId = Guid.NewGuid();

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
            await WebHost.CreateDefaultBuilder()
                .UseStartup<Startup>()
                .UseKestrel()
                .UseUrls("http://0.0.0.0:80")
                .Build()
                .RunAsync();
        }
    }
}
