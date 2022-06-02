//-----------------------------------------------------------------------------
// FILE:	    Startup.cs
// CONTRIBUTOR: Marcus Bowyer, Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Net.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Neon.Kube;

using KubeOps.Operator;
using k8s;

namespace NeonClusterOperator
{
    /// <summary>
    /// Configures the operator's service controllers.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Configures depdendency injection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            var operatorBuilder = services
                .AddSingleton<KubernetesWithRetry>(new KubernetesWithRetry(KubernetesClientConfiguration.BuildDefaultConfig()))
                .AddKubernetesOperator(
                    settings =>
                    {
                        settings.EnableAssemblyScanning = true;
                        settings.EnableLeaderElection   = false;    // ResourceManager is managing leader election
                    });

            Program.AddResourceAssemblies(operatorBuilder);
        }

        /// <summary>
        /// Configures the operator service controllers.
        /// </summary>
        /// <param name="app">Specifies the application builder.</param>
        public void Configure(IApplicationBuilder app)
        {
            app.UseKubernetesOperator();
        }
    }
}
