//-----------------------------------------------------------------------------
// FILE:	    WebHelper.Extensions.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
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
        /// <param name="services">The target service collection.</param>
        /// <param name="source">The service source container or <c>null</c> to copy from <see cref="NeonHelper.ServiceContainer"/>.</param>
        public static void AddNeon(this IServiceCollection services, IServiceContainer source = null)
        {
            source = source ?? NeonHelper.ServiceContainer;

            foreach (var service in source)
            {
                services.Add(service);
            }
        }

        /// <summary>
        /// <para>
        /// This method adds a custom <see cref="IControllerFactory"/> that resolves every 
        /// request to the <typeparamref name="TController"/> controller type.
        /// </para>
        /// <note>
        /// This method works only for one <typeparamref name="TController"/> type within
        /// the ASP.NET request pipeline.  You should not call this more than once.
        /// </note>
        /// </summary>
        /// <typeparam name="TController">The controller type.</typeparam>
        /// <param name="services">The target service collection.</param>
        /// <remarks>
        /// This is handy for unit tests that want to constrain the active controller
        /// to a specific class being tested.
        /// </remarks>
        public static void AddNeonSingletonController<TController>(this IServiceCollection services)
            where TController : ControllerBase
        {
            services.AddSingleton<IControllerFactory, SingletonControllerFactory<TController>>();
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
