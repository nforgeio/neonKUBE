using k8s.Models;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KubeOps.Operator;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Extension methods to register kubernetes operator components with the <see cref="IApplicationBuilder"/>.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// <para>
        /// Use the kubernetes operator. Registers controllers and webhooks.
        /// </para>
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
        public static void UseKubernetesOperator(
            this IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(
                endpoints =>
                {
                    var k8s = (IKubernetes)app.ApplicationServices.GetRequiredService<IKubernetes>();
                    var logger = (ILogger)app.ApplicationServices.GetRequiredService<ILogger>();

                    using var scope = app.ApplicationServices.CreateScope();
                    var componentRegistrar = scope.ServiceProvider.GetRequiredService<ComponentRegister>();

                    foreach (var webhook in componentRegistrar.MutatingWebhookRegistrations)
                    {
                        (Type mutatorType, Type entityType) = webhook;

                        var mutator = scope.ServiceProvider.GetRequiredService(mutatorType);

                        var registerMethod = typeof(IAdmissionWebhook<,>)
                            .MakeGenericType(entityType, typeof(MutationResult))
                            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                            .First(m => m.Name == "Register");

                        registerMethod.Invoke(mutator, new object[] { endpoints });

                        var createMethod = typeof(IMutationWebhook<>)
                            .MakeGenericType(entityType)
                            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                            .First(m => m.Name == "Create");

                        createMethod.Invoke(mutator, new object[] { k8s });
                    }
                });
        }
    }
}
