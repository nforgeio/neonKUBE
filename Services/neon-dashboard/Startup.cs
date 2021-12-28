//-----------------------------------------------------------------------------
// FILE:	    Startup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

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

using Neon.Diagnostics;
using Neon.Kube;
using Neon.Web;

using Blazor.Analytics;
using Blazor.Analytics.Components;

namespace NeonDashboard
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public NeonDashboardService NeonDashboardService;

        public Startup(IConfiguration configuration, NeonDashboardService service)
        {
            Configuration = configuration;
            this.NeonDashboardService = service;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            if (string.IsNullOrEmpty(NeonDashboardService.GetEnvironmentVariable("CLUSTER_DOMAIN")))
            {
                NeonDashboardService.SetEnvironmentVariable("CLUSTER_DOMAIN", Environment.GetEnvironmentVariable("CLUSTER_DOMAIN"));
            }

            if (string.IsNullOrEmpty(NeonDashboardService.GetEnvironmentVariable("SSO_CLIENT_SECRET")))
            {
                NeonDashboardService.SetEnvironmentVariable("SSO_CLIENT_SECRET", Environment.GetEnvironmentVariable("SSO_CLIENT_SECRET"));
            }

            services.AddServerSideBlazor();

            services.AddAuthentication(options => {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect("oidc", options =>
            {
                options.ClientId = "kubernetes";
                options.ClientSecret = NeonDashboardService.GetEnvironmentVariable("SSO_CLIENT_SECRET");
                options.Authority = $"https://{ClusterDomain.Sso}.{NeonDashboardService.GetEnvironmentVariable("CLUSTER_DOMAIN")}";
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.SaveTokens = true;
                options.RequireHttpsMetadata = false;
                options.RemoteAuthenticationTimeout = TimeSpan.FromSeconds(120);
                options.CallbackPath = "/oauth2/callback";
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
                .AddScoped<AppState>()
                .AddMvc();

            services
                .AddRazorPages()
                .AddNeon();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
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

            app.UseRouting();
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
