//-----------------------------------------------------------------------------
// FILE:	    ApplicationBuilderExtensions.cs
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
                async endpoints =>
                {
                    await SyncContext.Clear;

                    var k8s    = app.ApplicationServices.GetRequiredService<IKubernetes>();
                    var logger = app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger(nameof(ApplicationBuilderExtensions));

                    NgrokWebhookTunnel tunnel = null;

                    try
                    {
                        tunnel = app.ApplicationServices.GetServices<IHostedService>()
                            .OfType<NgrokWebhookTunnel>()
                            .Single();
                    }
                    catch { }

                    var componentRegistrar = app.ApplicationServices.GetRequiredService<ComponentRegister>();

                    foreach (var webhook in componentRegistrar.MutatingWebhookRegistrations)
                    {
                        try
                        {
                            (Type mutatorType, Type entityType) = webhook;

                            logger?.LogInformationEx(() => $"Registering mutating webhook [{mutatorType.Name}].");

                            var mutator = app.ApplicationServices.GetRequiredService(mutatorType);

                            var registerMethod = typeof(IAdmissionWebhook<,>)
                                .MakeGenericType(entityType, typeof(MutationResult))
                                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                .First(m => m.Name == "Register");

                            registerMethod.Invoke(mutator, new object[] { endpoints, app.ApplicationServices.GetService<ILoggerFactory>() });

                            if (tunnel == null)
                            {
                                var createMethod = typeof(IMutatingWebhook<>)
                                    .MakeGenericType(entityType)
                                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                    .First(m => m.Name == "Create");

                                createMethod.Invoke(mutator, new object[] { k8s, app.ApplicationServices.GetService<ILoggerFactory>() });
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
                            (Type validatorType, Type entityType) = webhook;

                            logger?.LogInformationEx(() => $"Registering validating webhook [{validatorType.Name}].");

                            var validator = app.ApplicationServices.GetRequiredService(validatorType);

                            var registerMethod = typeof(IAdmissionWebhook<,>)
                                .MakeGenericType(entityType, typeof(ValidationResult))
                                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                .First(m => m.Name == "Register");

                            registerMethod.Invoke(validator, new object[] { endpoints, app.ApplicationServices.GetService<ILoggerFactory>() });

                            if (tunnel == null)
                            {
                                var createMethod = typeof(IValidatingWebhook<>)
                                    .MakeGenericType(entityType)
                                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                    .First(m => m.Name == "Create");

                                createMethod.Invoke(validator, new object[] { k8s, app.ApplicationServices.GetService<ILoggerFactory>() });
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
