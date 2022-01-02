//-----------------------------------------------------------------------------
// FILE:	    Startup.cs
// CONTRIBUTOR: Marcus Bowyer, Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using KubeOps.Operator;

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
            services.AddKubernetesOperator()
                .AddResourceAssembly(typeof(Neon.Kube.Entities.V1ContainerRegistry).Assembly);
        }

        /// <summary>
        /// Configures the operator web service controllers.
        /// </summary>
        /// <param name="app">Specifies the application builder.</param>
        public void Configure(IApplicationBuilder app)
        {
            app.UseKubernetesOperator();
        }
    }
}
