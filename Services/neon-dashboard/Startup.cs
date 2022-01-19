//-----------------------------------------------------------------------------
// FILE:	    Startup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Web;

using Blazor.Analytics;
using Blazor.Analytics.Components;

using k8s;

using Prometheus;

using Segment;
using System.Text;

namespace NeonDashboard
{
    /// <summary>
    /// Configures the operator's service controllers.
    /// </summary>
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public Service NeonDashboardService;
        public static Dictionary<string, string> Svgs;
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Specifies the service configuration.</param>
        /// <param name="service">Specifies the service.</param>
        public Startup(IConfiguration configuration, Service service)
        {
            Configuration             = configuration;
            this.NeonDashboardService = service;
        }

        /// <summary>
        /// Configures depdendency injection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public async void ConfigureServices(IServiceCollection services)
        {
            Analytics.Initialize("nadwV6twqGHRLB451dblyqZVCwulUCFV",
                new Config()
                .SetAsync(!NeonHelper.IsDevWorkstation));

            if (NeonHelper.IsDevWorkstation)
            {
                var configFile = Environment.GetEnvironmentVariable("KUBECONFIG").Split(';').Where(variable => variable.Contains("config")).FirstOrDefault();
                var k8sClient  = new KubernetesWithRetry(KubernetesClientConfiguration.BuildDefaultConfig());

                var configMap  = await k8sClient.ReadNamespacedConfigMapAsync("neon-dashboard", KubeNamespaces.NeonSystem);
                var secret     = await k8sClient.ReadNamespacedSecretAsync("neon-sso-dex", KubeNamespaces.NeonSystem);

                NeonDashboardService.SetEnvironmentVariable("CLUSTER_DOMAIN", configMap.Data["CLUSTER_DOMAIN"]);
                NeonDashboardService.SetEnvironmentVariable("SSO_CLIENT_SECRET", Encoding.UTF8.GetString(secret.Data["KUBERNETES_CLIENT_SECRET"]));
            }

            services.AddServerSideBlazor();

            services.AddAuthentication(options => {
                options.DefaultScheme          = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect("oidc", options =>
            {
                options.ClientId                      = "kubernetes";
                options.ClientSecret                  = NeonDashboardService.GetEnvironmentVariable("SSO_CLIENT_SECRET");
                options.Authority                     = $"https://{ClusterDomain.Sso}.{NeonDashboardService.GetEnvironmentVariable("CLUSTER_DOMAIN")}";
                options.ResponseType                  = OpenIdConnectResponseType.Code;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SignInScheme                  = CookieAuthenticationDefaults.AuthenticationScheme;
                options.SaveTokens                    = true;
                options.RequireHttpsMetadata          = false;
                options.RemoteAuthenticationTimeout   = TimeSpan.FromSeconds(120);
                options.CallbackPath                  = "/oauth2/callback";
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("groups");
                options.UseTokenLifetime = false;
                options.TokenValidationParameters = new TokenValidationParameters { NameClaimType = "name" };
            });

            services
                .AddHttpContextAccessor()
                .AddHttpClient()
                .AddSingleton<INeonLogger>(NeonDashboardService.LogManager.GetLogger())
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

            app.UseStaticFiles();

            //app.UseRouting();
            //app.UseHttpMetrics();
            //app.UseCookiePolicy();
            //app.UseAuthentication();
            //app.UseAuthorization();
            //app.UseHttpLogging();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
