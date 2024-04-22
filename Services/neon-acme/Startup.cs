//-----------------------------------------------------------------------------
// FILE:        Startup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright � 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

using Neon.Web;

using Prometheus;

namespace NeonAcme
{
    /// <summary>
    /// Configures the operator's service controllers.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// The <see cref="IConfiguration"/>.
        /// </summary>
        public IConfiguration Configuration { get; }
        
        /// <summary>
        /// The <see cref="Service"/>.
        /// </summary>
        public Service NeonAcmeService;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Specifies the service configuration.</param>
        /// <param name="service">Specifies the service.</param>
        public Startup(IConfiguration configuration, Service service)
        {
            this.Configuration   = configuration;
            this.NeonAcmeService = service;
        }

        /// <summary>
        /// Configures depdendency injection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddSingleton(NeonAcmeService)
                .AddSingleton<ILogger>(Program.Service.Logger)
                .AddSwaggerGen(
                    options =>
                    {
                        options.SwaggerDoc("v1alpha1",
                            new OpenApiInfo
                            {
                                Title   = "neon-acme",
                                Version = "v1alpha1"
                            });
                    });

            services.AddControllers()
                .AddNeon();
        }

        /// <summary>
        /// Configures the service controllers.
        /// </summary>
        /// <param name="app">Specifies the application builder.</param>
        /// <param name="env">Specifies the web hosting environment.</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (NeonAcmeService.DebugMode)
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger(
                options =>
                {
                    options.RouteTemplate = "/apis/{documentName}";
                });
            app.UseRouting();
            app.UseHttpMetrics();
            app.UseEndpoints
                (endpoints =>
                {
                    endpoints.MapControllers();
                });

            // Indicate that the service is ready for business.

            NeonAcmeService.StartedAsync().GetAwaiter().GetResult();
        }
    }
}
