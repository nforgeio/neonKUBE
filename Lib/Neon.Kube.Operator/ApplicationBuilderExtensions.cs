//-----------------------------------------------------------------------------
// FILE:        ApplicationBuilderExtensions.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Tasks;
using Neon.Kube.Operator.Builder;
using Neon.Kube.Operator.Webhook;
using Neon.Kube.Operator.Webhook.Ngrok;

using k8s;

using Prometheus;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Extension methods to register Kubernetes operator components with the <see cref="IApplicationBuilder"/>.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// <para>
        /// Use the Kubernetes operator. Registers controllers and webhooks.
        /// </para>
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
        public static void UseKubernetesOperator(this IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseEndpoints(
                async endpoints =>
                {
                    await SyncContext.Clear;

                    var k8s              = app.ApplicationServices.GetRequiredService<IKubernetes>();
                    var operatorSettings = app.ApplicationServices.GetRequiredService<OperatorSettings>();
                    var logger           = app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger(nameof(ApplicationBuilderExtensions));

                    endpoints.MapMetrics(operatorSettings.MetricsEndpoint);

                    endpoints.MapHealthChecks(operatorSettings.StartupEndpooint, new HealthCheckOptions()
                    {
                        Predicate = (healthCheck =>
                        {
                            return healthCheck.Tags.Contains(OperatorBuilder.StartupHealthProbeTag);
                        })
                    });

                    endpoints.MapHealthChecks(operatorSettings.LivenessEndpooint, new HealthCheckOptions()
                    {
                        Predicate = (healthCheck =>
                        {
                            return healthCheck.Tags.Contains(OperatorBuilder.LivenessHealthProbeTag);
                        })
                    });

                    endpoints.MapHealthChecks(operatorSettings.ReadinessEndpooint, new HealthCheckOptions()
                    {
                        Predicate = (healthCheck =>
                        {
                            return healthCheck.Tags.Contains(OperatorBuilder.ReadinessHealthProbeTag);
                        })
                    });

                    var tunnel = app.ApplicationServices.GetServices<IHostedService>()
                        .OfType<NgrokWebhookTunnel>()
                        .SingleOrDefault();

                    var componentRegistrar = app.ApplicationServices.GetRequiredService<ComponentRegister>();

                    foreach (var webhook in componentRegistrar.MutatingWebhookRegistrations)
                    {
                        try
                        {
                            using (var scope = app.ApplicationServices.CreateScope())
                            {
                                (Type mutatorType, Type entityType) = webhook;

                                logger?.LogInformationEx(() => $"Registering mutating webhook [{mutatorType.Name}].");

                                var mutator = scope.ServiceProvider.GetRequiredService(mutatorType);

                                var registerMethod = typeof(IAdmissionWebhook<,>)
                                    .MakeGenericType(entityType, typeof(MutationResult))
                                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                    .First(m => m.Name == "Register");

                                registerMethod.Invoke(mutator, new object[] { endpoints, app.ApplicationServices });

                                if (tunnel == null)
                                {
                                    var createMethod = typeof(IMutatingWebhook<>)
                                        .MakeGenericType(entityType)
                                        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                        .First(m => m.Name == "Create");

                                    var task = (Task)createMethod.Invoke(mutator, new object[] { k8s, scope.ServiceProvider });

                                    await task;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            logger?.LogErrorEx(e);
                        }
                    }

                    foreach (var webhook in componentRegistrar.ValidatingWebhookRegistrations)
                    {
                        try
                        {
                            using (var scope = app.ApplicationServices.CreateScope())
                            {
                                (Type validatorType, Type entityType) = webhook;

                                logger?.LogInformationEx(() => $"Registering validating webhook [{validatorType.Name}].");

                                var validator = scope.ServiceProvider.GetRequiredService(validatorType);

                                var registerMethod = typeof(IAdmissionWebhook<,>)
                                    .MakeGenericType(entityType, typeof(ValidationResult))
                                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                    .First(m => m.Name == "Register");

                                registerMethod.Invoke(validator, new object[] { endpoints, app.ApplicationServices });

                                if (tunnel == null)
                                {
                                    var createMethod = typeof(IValidatingWebhook<>)
                                        .MakeGenericType(entityType)
                                        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                        .First(m => m.Name == "Create");

                                    var task = (Task)createMethod.Invoke(validator, new object[] { k8s, operatorSettings, scope.ServiceProvider.GetService<ILoggerFactory>() });

                                    await task;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            logger?.LogErrorEx(e);
                        }
                    }
                });
        }
    }
}
