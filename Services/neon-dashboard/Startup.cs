//-----------------------------------------------------------------------------
// FILE:	    Startup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Web;

using Blazor.Analytics;
using Blazor.Analytics.Components;

using Blazored.LocalStorage;

using k8s;

using Prometheus;

using Segment;

using StackExchange.Redis;

using Neon.Tailwind;

namespace NeonDashboard
{
    /// <summary>
    /// Configures the operator's service controllers.
    /// </summary>
    public class Startup
    {
        public IConfiguration                       Configuration { get; }
        public Service                              NeonDashboardService;
        public KubernetesWithRetry                  k8s;
        public static Dictionary<string, string>    Svgs;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Specifies the service configuration.</param>
        /// <param name="service">Specifies the service.</param>
        public Startup(IConfiguration configuration, Service service)
        {
            Configuration             = configuration;
            this.NeonDashboardService = service;
            k8s                       = service.Kubernetes;
        }

        /// <summary>
        /// Configures depdendency injection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            Analytics.Initialize("nadwV6twqGHRLB451dblyqZVCwulUCFV",
                new Config()
                .SetAsync(!NeonHelper.IsDevWorkstation));

            bool.TryParse(NeonDashboardService.GetEnvironmentVariable("DO_NOT_TRACK", "false"), out var doNotTrack);
            Analytics.Client.Config.SetSend(doNotTrack);
            
            if (NeonHelper.IsDevWorkstation)
            {
                services.AddDistributedMemoryCache();
            }
            else
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = "neon-redis.neon-system";
                    options.InstanceName = "neon-redis";
                    options.ConfigurationOptions = new ConfigurationOptions()
                    {
                        AllowAdmin = true,
                        ServiceName = "master"
                    };

                    options.ConfigurationOptions.EndPoints.Add("neon-redis.neon-system:26379");
                });
            }

            services.AddServerSideBlazor();
            services.AddAuthentication(options => {
                options.DefaultScheme             = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = OpenIdConnectDefaults.AuthenticationScheme;
                options.DefaultSignInScheme       = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.ExpireTimeSpan         = TimeSpan.FromMinutes(20);
                options.SlidingExpiration      = true;
                options.AccessDeniedPath       = "/Forbidden/";
                options.DataProtectionProvider = new CookieProtector(NeonDashboardService.AesCipher);
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.ClientId                      = "kubernetes";
                options.ClientSecret                  = NeonDashboardService.SsoClientSecret;
                options.Authority                     = $"https://{ClusterDomain.Sso}.{NeonDashboardService.ClusterInfo.Domain}";
                options.ResponseType                  = OpenIdConnectResponseType.Code;
                options.SignInScheme                  = CookieAuthenticationDefaults.AuthenticationScheme;
                options.SaveTokens                    = true;
                options.RequireHttpsMetadata          = true;
                options.RemoteAuthenticationTimeout   = TimeSpan.FromSeconds(120);
                options.CallbackPath                  = "/oauth2/callback";
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("groups");
                options.UsePkce                       = false;
                options.DataProtectionProvider        = new CookieProtector(NeonDashboardService.AesCipher);
                options.UseTokenLifetime              = false;
                options.ProtocolValidator             = new OpenIdConnectProtocolValidator()
                {
                    RequireNonce = false,
                    RequireState = false
                };
                options.Events = new OpenIdConnectEvents
                {
                    OnTicketReceived = context =>
                    {
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        context.Response.Redirect("/login?redirectUri=/");
                        context.HandleResponse(); // Suppress the exception
                        return Task.CompletedTask;
                    },
                    OnRemoteSignOut = context =>
                    {

                        return Task.CompletedTask;
                    }
                };
            });

            services
                .AddHttpContextAccessor()
                .AddHttpClient()
                .AddBlazoredLocalStorage()
                .AddTailwind()
                .AddSingleton<ILogger>(Program.Service.Logger)
                .AddGoogleAnalytics("G-PYMLFS3FX4")
                .AddRouting()
                .AddScoped<AppState>()
                .AddMvc();

            services
                .AddRazorPages()
                .AddNeon();
        }

        /// <summary>
        /// Configures the operator service controllers.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="env">Specifies the web hosting environment.</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (NeonHelper.IsDevWorkstation)
            {
                app.RunTailwind(script: "dev");
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                app.UseCookiePolicy(new CookiePolicyOptions()
                {
                    MinimumSameSitePolicy = SameSiteMode.Lax
                });
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedProto
            });

            app.UseStaticFiles();
            app.UseRouting();
            app.UseHttpMetrics();
            app.UseCookiePolicy();
                app.UseAuthentication();
                app.UseAuthorization();
            app.UseHttpLogging();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
