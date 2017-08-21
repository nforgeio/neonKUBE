//-----------------------------------------------------------------------------
// FILE:	    WebHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

using Neon.Common;
using Neon.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Neon.Web
{
    /// <summary>
    /// Utility methods for <b>AspNetCore</b> applications.
    /// </summary>
    public static class WebHelper
    {
        /// <summary>
        /// Performs common web app initialization including setting the correct
        /// working directory.  Call this method from your application's main
        /// entrypoint method.
        /// </summary>
        public static void Initialize()
        {
            // AspNetCore expects the current working directory to be where
            // the main executable is located.

            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        }

        /// <summary>
        /// Generates an opaque globally unique activity ID.
        /// </summary>
        /// <returns>The activity ID string.</returns>
        public static string GenerateActivityId()
        {
            return NeonHelper.UrlTokenEncode(Guid.NewGuid().ToByteArray());
        }

        //---------------------------------------------------------------------
        // IApplicationBuilder extensions

        /// <summary>
        /// Adds default Neon functionality to the <see cref="IApplicationBuilder"/>
        /// HTTP request pipeline.
        /// </summary>
        /// <param name="app">The application pipeline builder.</param>
        /// <param name="loggerFactory">The application logger factory.</param>
        /// <returns>The <pararef name="app"/></returns>
        /// <remarks>
        /// <para>
        /// This method adds the following capabilities:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     Sets the pipeline's <see cref="IApplicationBuilder.ApplicationServices"/> dependency injection
        ///     container to <see cref="NeonHelper.ServicesContainer"/>, the default Neon root container.
        ///     </item>
        ///     <item>
        ///     Adds a handler that logs unhandled exceptions.
        ///     </item>
        /// </list>
        /// </remarks>
        public static IApplicationBuilder UseNeon(this IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddProvider(LogManager.Default);  // $todo(jeff.lill): Need this via dependency injection.

            return app;
        }
    }
}
