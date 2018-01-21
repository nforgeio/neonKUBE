//-----------------------------------------------------------------------------
// FILE:	    WebHelper.Extensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;

namespace Neon.Web
{
    public static partial class WebHelper
    {
        //---------------------------------------------------------------------
        // IServiceCollection extensions

        /// <summary>
        /// Adds the services from an <see cref="IServiceContainer"/> to the <see cref="IServiceCollection"/>.
        /// This is commonly used when configuring services for an ASP.NET application pipeline.
        /// </summary>
        /// <param name="services">The target collection.</param>
        /// <param name="source">The service source container or <c>null</c> to copy from <see cref="NeonHelper.ServiceContainer"/>.</param>
        public static void AddNeon(this IServiceCollection services, IServiceContainer source = null)
        {
            source = source ?? NeonHelper.ServiceContainer;

            foreach (var service in source)
            {
                services.Add(service);
            }
        }

        //---------------------------------------------------------------------
        // IApplicationBuilder extensions

        /// <summary>
        /// Adds default Neon functionality to the <see cref="IApplicationBuilder"/>
        /// HTTP request pipeline.
        /// </summary>
        /// <param name="app">The application pipeline builder.</param>
        /// <param name="loggerFactory">The application logger factory.</param>
        /// <returns>The <pararef name="app"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method adds the following capabilities:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     Sets the pipeline's <see cref="IApplicationBuilder.ApplicationServices"/> dependency injection
        ///     container to <see cref="NeonHelper.ServiceContainer"/>, the default Neon root container.
        ///     </item>
        ///     <item>
        ///     Adds a handler that logs unhandled exceptions.
        ///     </item>
        /// </list>
        /// </remarks>
        public static IApplicationBuilder UseNeon(this IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddProvider(app.ApplicationServices.GetService<ILoggerProvider>());

            return app;
        }

        //---------------------------------------------------------------------
        // Microsoft.AspNetCore.Http.HttpRequest extensions.

        /// <summary>
        /// Returns the full URI for an <see cref="HttpRequest"/> (not including the port number).
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The fully qualified URI including any query parameters.</returns>
        public static string GetUri(this HttpRequest request)
        {
            return $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
        }
    }
}
