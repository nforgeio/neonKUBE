//-----------------------------------------------------------------------------
// FILE:	    Startup.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Consul;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Cluster;
using Neon.Web;

namespace NeonDns
{
    /// <summary>
    /// Configures the web application.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Initializes the application configuration.
        /// </summary>
        /// <param name="env">The hosting environment.</param>
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        /// <summary>
        /// Returns the application configuration.
        /// </summary>
        public IConfigurationRoot Configuration { get; }

        /// <summary>
        /// Called by the runtime to allow additional services to be added to the application.
        /// </summary>
        /// <param name="services">The application service collection.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddNeon();
            services.AddSingleton<ConsulClient>(NeonClusterHelper.Consul);
            services.AddMvc();
        }

        /// <summary>
        /// Called by the runtime to allow for HTTP request pipeline customization.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="env">The hbosting environment.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            app.UseNeon(loggerFactory);
            app.UseMvc();
        }
    }
}
