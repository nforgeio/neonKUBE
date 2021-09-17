//-----------------------------------------------------------------------------
// FILE:	    KubeKvStartup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2019 by Loopie Laundry, LLC.  All rights reserved.

using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Neon.Kube;
using Neon.Retry;

using k8s;

using Npgsql;
using Neon.Web;

namespace NeonKubeKv
{
    /// <summary>
    /// Handles ASP.NET related service configuration.
    /// </summary>
    public class KubeKv
    {
        /// <summary>
        /// The <see cref="Service"/> service.
        /// </summary>
        private Service service;



        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">The service configuration.</param>
        /// <param name="service"></param>
        public KubeKv(IConfiguration configuration, Service service)
        {
            this.Configuration = configuration;
            this.service = service;
        }

        /// <summary>
        /// Returns the service configuration.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Configures the required ASP.NET services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureServices(IServiceCollection services)
        {

            var secret = service.k8s.ReadNamespacedSecret(KubeConst.NeonSystemDbAdminSecret, KubeNamespaces.NeonSystem);
            var username = Encoding.UTF8.GetString(secret.Data["username"]);
            var password = Encoding.UTF8.GetString(secret.Data["password"]);

            var dbHost = $"db-citus-postgresql.{KubeNamespaces.NeonSystem}";

            service.DbConnectionString = $"Host={dbHost};Username={username};Password={password};Database={KubeConst.NeonClusterOperatorDatabase}";

            services
                .AddMvc()
                .AddNeon();
        }

        /// <summary>
        /// Configures the service.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="env">The environment.</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (!string.IsNullOrEmpty(service.GetEnvironmentVariable("DEBUG")))
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}