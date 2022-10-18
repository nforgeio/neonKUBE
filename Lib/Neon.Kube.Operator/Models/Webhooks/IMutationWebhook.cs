using JsonDiffPatch;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
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

namespace Neon.Kube.Operator
{
    public interface IMutationWebhook<TEntity> : IAdmissionWebhook<TEntity, MutationResult>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
    {
        public V1MutatingWebhookConfiguration WebhookConfiguration { get; }

        /// <inheritdoc />
        string IAdmissionWebhook<TEntity, MutationResult>.Endpoint
        {
            get => WebhookHelper.CreateEndpoint<TEntity>(this.GetType(), WebhookType);
        }

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
            AdmissionRequest<TEntity> request,
            JsonSerializerSettings jsonSettings)
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
                var serializer = JsonSerializer.Create(jsonSettings);
                response.PatchType = AdmissionResponse.JsonPatch;
                var @object = JToken.FromObject(
                    request.Operation == "DELETE"
                        ? request.OldObject
                        : request.Object,
                    serializer);
                var patch = new JsonDiffer().Diff(@object, JToken.FromObject(result.ModifiedObject, serializer), false);
                response.Patch = Convert.ToBase64String(Encoding.UTF8.GetBytes(patch.ToString()));
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
