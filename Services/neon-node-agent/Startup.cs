//-----------------------------------------------------------------------------
// FILE:	    Startup.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using KubeOps.Operator;

namespace NeonNodeAgent
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
                .AddKubernetesOperator(
                    settings =>
                    {
                        settings.EnableAssemblyScanning = true;
                        settings.EnableLeaderElection   = false;    // node agents run in parallel to manage their cluster nodes
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
