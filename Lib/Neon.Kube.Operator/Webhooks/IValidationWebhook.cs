//-----------------------------------------------------------------------------
// FILE:	    IValidatingWebhook.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.JsonDiffPatch.Diffs.Formatters;
using System.Text.Json.JsonDiffPatch;
using System.Reflection;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube.Operator.Attributes;
using Neon.Kube.Operator.Builder;

using k8s;
using k8s.Autorest;
using k8s.KubeConfigModels;
using k8s.Models;
using Microsoft.AspNetCore;
using Microsoft.Extensions.DependencyInjection;

namespace Neon.Kube.Operator.Webhook
{
    /// <summary>
    /// Represents a Validating webhook.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    [OperatorComponent(OperatorComponentType.ValidationWebhook)]
    [ValidatingWebhook]
    public interface IValidatingWebhook<TEntity> : IAdmissionWebhook<TEntity, ValidationResult>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
    {
        /// <summary>
        /// The namespace selector.
        /// </summary>
        public V1LabelSelector NamespaceSelector => null;

        /// <summary>
        /// The Object selector.
        /// </summary>
        public V1LabelSelector ObjectSelector => null;



        /// <summary>
        /// The webhook configuration.
        /// </summary>
        public V1ValidatingWebhookConfiguration WebhookConfiguration(
            OperatorSettings   operatorSettings,
            bool               useTunnel = false, 
            string             tunnelUrl = null)
        {
            var hook = this.GetType().GetCustomAttribute<WebhookAttribute>();

            var clientConfig = new Admissionregistrationv1WebhookClientConfig()
            {
                Service = new Admissionregistrationv1ServiceReference()
                {
                    Name              = operatorSettings.Name,
                    NamespaceProperty = operatorSettings.Namespace,
                    Path              = WebhookHelper.CreateEndpoint<TEntity>(this.GetType(), WebhookType.Mutate)
                }
            };

            if (useTunnel && !string.IsNullOrEmpty(tunnelUrl))
            {
                clientConfig.Service = null;
                clientConfig.CaBundle = null;
                clientConfig.Url = tunnelUrl.TrimEnd('/') + WebhookHelper.CreateEndpoint<TEntity>(this.GetType(), WebhookType.Validate);
            }

            var webhookConfig = new V1ValidatingWebhookConfiguration().Initialize();
            webhookConfig.Metadata.Name = hook.Name;

            if (!useTunnel && operatorSettings.certManagerEnabled)
            {
                webhookConfig.Metadata.EnsureAnnotations().Add("cert-manager.io/inject-ca-from", $"{operatorSettings.Namespace}/{operatorSettings.Name}");
            }

            webhookConfig.Webhooks = new List<V1ValidatingWebhook>()
            {
                new V1ValidatingWebhook()
                {
                    Name = hook.Name,
                    Rules = new List<V1RuleWithOperations>(),
                    ClientConfig = clientConfig,
                    AdmissionReviewVersions = hook.AdmissionReviewVersions,
                    FailurePolicy = hook.FailurePolicy,
                    SideEffects = hook.SideEffects,
                    TimeoutSeconds = hook.TimeoutSeconds,
                    NamespaceSelector = NamespaceSelector,
                    MatchPolicy = hook.MatchPolicy,
                    ObjectSelector = ObjectSelector,
                }
            };

            var rules = this.GetType().GetCustomAttributes<WebhookRuleAttribute>();

            foreach (var rule in rules)
            {
                webhookConfig.Webhooks.FirstOrDefault().Rules.Add(
                    new V1RuleWithOperations()
                    {
                        ApiGroups = rule.ApiGroups,
                        ApiVersions = rule.ApiVersions,
                        Operations = rule.Operations.ToList(),
                        Resources = rule.Resources,
                        Scope = rule.Scope
                    }
                );
            }

            return webhookConfig;
        }

        /// <inheritdoc />
        string IAdmissionWebhook<TEntity, ValidationResult>.Endpoint
        {
            get => WebhookHelper.CreateEndpoint<TEntity>(this.GetType(), WebhookType);
        }

        /// <inheritdoc/>
        WebhookType IAdmissionWebhook<TEntity, ValidationResult>.WebhookType
        {
            get => WebhookType.Validate;
        }

        /// <inheritdoc />
        ValidationResult IAdmissionWebhook<TEntity, ValidationResult>.Create(TEntity newEntity, bool dryRun)
            => AdmissionResult.NotImplemented<ValidationResult>();

        /// <inheritdoc />
        ValidationResult IAdmissionWebhook<TEntity, ValidationResult>.Update(
            TEntity oldEntity,
            TEntity newEntity,
            bool dryRun)
            => AdmissionResult.NotImplemented<ValidationResult>();

        /// <inheritdoc />
        ValidationResult IAdmissionWebhook<TEntity, ValidationResult>.Delete(TEntity oldEntity, bool dryRun)
            => AdmissionResult.NotImplemented<ValidationResult>();

        AdmissionResponse IAdmissionWebhook<TEntity, ValidationResult>.TransformResult(
            ValidationResult result,
            AdmissionRequest<TEntity> request)
        {
            var response = new AdmissionResponse
            {
                Allowed = result.Valid,
                Status = result.StatusMessage == null
                    ? null
                    : new AdmissionResponse.Reason { Code = result.StatusCode ?? 0, Message = result.StatusMessage, },
                Warnings = result.Warnings.ToArray(),
            };

            return response;
        }

        internal async Task Create(IKubernetes k8s, IServiceProvider serviceProvider)
        {
            var operatorSettings   = serviceProvider.GetRequiredService<OperatorSettings>();
            var certManagerOptions = serviceProvider.GetService<CertManagerOptions>();
            var logger             = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<IValidatingWebhook<TEntity>>();

            logger?.LogInformationEx(() => $"Checking for webhook {this.GetType().Name}.");

            bool useDevTunnel      = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VS_TUNNEL_URL"));
            string certificateName = operatorSettings.certManagerEnabled ? operatorSettings.Name : null;
            var webhookConfig      = WebhookConfiguration(
                                            operatorSettings: operatorSettings,
                                            useTunnel: true, 
                                            tunnelUrl: Environment.GetEnvironmentVariable("VS_TUNNEL_URL"));

            try
            {
                var webhook = await k8s.AdmissionregistrationV1.ReadValidatingWebhookConfigurationAsync(webhookConfig.Name());

                webhook.Webhooks = webhookConfig.Webhooks;
                await k8s.AdmissionregistrationV1.ReplaceValidatingWebhookConfigurationAsync(webhook, webhook.Name());

                logger?.LogInformationEx(() => $"Webhook {this.GetType().Name} updated.");
            }
            catch (HttpOperationException e) 
            {
                logger?.LogInformationEx(() => $"Webhook {this.GetType().Name} not found, creating.");

                if (e.Response.StatusCode == System.Net.HttpStatusCode.NotFound) 
                {
                    await k8s.AdmissionregistrationV1.CreateValidatingWebhookConfigurationAsync(webhookConfig);

                    logger?.LogInformationEx(() => $"Webhook {this.GetType().Name} created.");
                }
                else 
                {
                    logger?.LogErrorEx(e);

                    throw e;
                }
            }
        }
    }
}
