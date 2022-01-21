//-----------------------------------------------------------------------------
// FILE:	    Startup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
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

using Blazored.LocalStorage;

using k8s;

using Prometheus;

using Segment;

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
        public void ConfigureServices(IServiceCollection services)
        {
            Analytics.Initialize("nadwV6twqGHRLB451dblyqZVCwulUCFV",
                new Config()
                .SetAsync(!NeonHelper.IsDevWorkstation));

            if (NeonHelper.IsDevWorkstation)
            {
                try
                {
                    var configFile = Environment.GetEnvironmentVariable("KUBECONFIG").Split(';').Where(variable => variable.Contains("config")).FirstOrDefault();
                    var k8sClient = new KubernetesWithRetry(KubernetesClientConfiguration.BuildDefaultConfig());

                    var configMap = k8sClient.ReadNamespacedConfigMapAsync("neon-dashboard", KubeNamespaces.NeonSystem).Result;
                    var secret = k8sClient.ReadNamespacedSecretAsync("neon-sso-dex", KubeNamespaces.NeonSystem).Result;

                    NeonDashboardService.SetEnvironmentVariable("CLUSTER_DOMAIN", configMap.Data["CLUSTER_DOMAIN"]);
                    NeonDashboardService.SetEnvironmentVariable("SSO_CLIENT_SECRET", Encoding.UTF8.GetString(secret.Data["KUBERNETES_CLIENT_SECRET"]));

                    // Check dex config
                    var dexConfigMap = k8sClient.ReadNamespacedConfigMapAsync("neon-sso-dex", KubeNamespaces.NeonSystem).Result;

                    var yamlConfig = NeonHelper.YamlDeserialize<dynamic>(dexConfigMap.Data["config.yaml"]);
                    var dexConfig = (DexConfig)NeonHelper.JsonDeserialize<DexConfig>(NeonHelper.JsonSerialize(yamlConfig));

                    var clientConfig = dexConfig.StaticClients.Where(c => c.Id == "kubernetes").First();
                    if (!clientConfig.RedirectUris.Contains("http://localhost:11001/oauth2/callback"))
                    {
                        clientConfig.RedirectUris.Add("http://localhost:11001/oauth2/callback");
                        dexConfigMap.Data["config.yaml"] = NeonHelper.ToLinuxLineEndings(NeonHelper.YamlSerialize(dexConfig));
                        k8sClient.ReplaceNamespacedConfigMapAsync(configMap, dexConfigMap.Metadata.Name, KubeNamespaces.NeonSystem).Wait();
                    }
                }
                catch (Exception ex)
                {
                    NeonDashboardService.Log.LogError(ex);
                }
            }

            services.AddServerSideBlazor();

            services.AddAuthentication(options => {
                options.DefaultScheme          = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
                options.SlidingExpiration = true;
                options.AccessDeniedPath = "/Forbidden/";
            })
            .AddOpenIdConnect("oidc", options =>
            {
                options.ClientId                      = "kubernetes";
                options.ClientSecret                  = NeonDashboardService.GetEnvironmentVariable("SSO_CLIENT_SECRET");
                options.Authority                     = $"https://{ClusterDomain.Sso}.{NeonDashboardService.GetEnvironmentVariable("CLUSTER_DOMAIN")}";
                options.ResponseType                  = OpenIdConnectResponseType.Code;
                //options.GetClaimsFromUserInfoEndpoint = true;
                options.SignInScheme                  = CookieAuthenticationDefaults.AuthenticationScheme;
                options.SaveTokens                    = true;
                options.RequireHttpsMetadata          = true;
                options.RemoteAuthenticationTimeout   = TimeSpan.FromSeconds(120);
                options.CallbackPath                  = "/oauth2/callback";
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("groups");
                options.UsePkce = false;
                options.UseTokenLifetime = false;
                options.ProtocolValidator = new OpenIdConnectProtocolValidator()
                {
                    RequireNonce = false,
                    RequireState = false
                    
                };
            });

            services
                .AddHttpContextAccessor()
                .AddHttpClient()
                .AddBlazoredLocalStorage()
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
