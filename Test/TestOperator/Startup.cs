using System.Collections.Generic;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Neon.Kube.Operator;
using Neon.Kube.Operator.Rbac;
using Neon.Kube.Operator.ResourceManager;

using k8s.Models;
using Microsoft.Extensions.Logging;

namespace TestOperator
{
    /// <summary>
    /// Configures the operator's service controllers.
    /// </summary>
    public class OperatorStartup
    {
        /// <summary>
        /// The <see cref="IConfiguration"/>.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Specifies the service configuration.</param>
        public OperatorStartup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        /// <summary>
        /// Configures depdendency injection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(c =>
            {
                c.SetMinimumLevel(LogLevel.Debug);
                c.ClearProviders();
                c.AddConsole();
            })
                .AddKubernetesOperator();
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
