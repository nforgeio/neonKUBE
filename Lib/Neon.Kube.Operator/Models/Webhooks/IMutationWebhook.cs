using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using k8s.Models;
using k8s;
using k8s.KubeConfigModels;
using Microsoft.AspNetCore.Routing;
using k8s.Autorest;
using Neon.Diagnostics;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.JsonDiffPatch.Diffs.Formatters;
using System.Text.Json.JsonDiffPatch;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Represents a mutating webhook.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public interface IMutationWebhook<TEntity> : IAdmissionWebhook<TEntity, MutationResult>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
    {
        /// <summary>
        /// Logger.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// The webhook configuration.
        /// </summary>
        public V1MutatingWebhookConfiguration WebhookConfiguration { get; }

        /// <inheritdoc />
        string IAdmissionWebhook<TEntity, MutationResult>.Endpoint
        {
            get => WebhookHelper.CreateEndpoint<TEntity>(this.GetType(), WebhookType);
        }

        /// <inheritdoc/>
        WebhookType IAdmissionWebhook<TEntity, MutationResult>.WebhookType
        {
            get => WebhookType.Mutate;
        }

        /// <inheritdoc />
        MutationResult IAdmissionWebhook<TEntity, MutationResult>.Create(TEntity newEntity, bool dryRun)
            => AdmissionResult.NotImplemented<MutationResult>();

        /// <inheritdoc />
        MutationResult IAdmissionWebhook<TEntity, MutationResult>.Update(
            TEntity oldEntity,
            TEntity newEntity,
            bool dryRun)
            => AdmissionResult.NotImplemented<MutationResult>();

        /// <inheritdoc />
        MutationResult IAdmissionWebhook<TEntity, MutationResult>.Delete(TEntity oldEntity, bool dryRun)
            => AdmissionResult.NotImplemented<MutationResult>();

        AdmissionResponse IAdmissionWebhook<TEntity, MutationResult>.TransformResult(
            MutationResult result,
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

            if (result.ModifiedObject != null)
            {
                response.PatchType = AdmissionResponse.JsonPatch;

                var node1 = JsonNode.Parse(KubernetesJson.Serialize(
                    request.Operation == "DELETE"
                        ? request.OldObject
                        : request.Object));

                Logger?.LogInformationEx(() => $"node1: {KubernetesJson.Serialize(node1)}");

                var node2 = JsonNode.Parse(KubernetesJson.Serialize(result.ModifiedObject));

                Logger?.LogInformationEx(() => $"node2: {KubernetesJson.Serialize(node2)}");

                var diff = node1.Diff(node2, new JsonPatchDeltaFormatter());

                Logger?.LogInformationEx(() => $"diff: {KubernetesJson.Serialize(diff)}");

                response.Patch = Convert.ToBase64String(Encoding.UTF8.GetBytes(KubernetesJson.Serialize(diff)));
                response.PatchType = AdmissionResponse.JsonPatch;
                
            }

            return response;
        }

        internal async Task Create(IKubernetes k8s)
        {
            try
            {
                var webhook = await k8s.ReadMutatingWebhookConfigurationAsync(WebhookConfiguration.Name());
                if (webhook != null) 
                {
                    await k8s.ReplaceMutatingWebhookConfigurationAsync(WebhookConfiguration, WebhookConfiguration.Name());
                }
            }
            catch (HttpOperationException e) 
            {
                if (e.Response.StatusCode== System.Net.HttpStatusCode.NotFound) 
                {
                    await k8s.CreateMutatingWebhookConfigurationAsync(WebhookConfiguration);
                }
                else 
                {
                    throw e;
                }
            }
        }
    }
}
