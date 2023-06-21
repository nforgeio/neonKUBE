//-----------------------------------------------------------------------------
// FILE:        IAdmissionWebhook.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;

using Newtonsoft.Json;

using k8s;
using k8s.Models;
using Prometheus;
using System.Diagnostics.Contracts;

namespace Neon.Kube.Operator.Webhook
{
    /// <summary>
    /// Represents an Admission webhook.
    /// </summary>
    /// <typeparam name="TEntity">Specifies the entity type.</typeparam>
    /// <typeparam name="TResult">Specifies the result type.</typeparam>
    public interface IAdmissionWebhook<TEntity, TResult>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        where TResult : AdmissionResult, new()
    {
        /// <summary>
        /// The number of seconds to apply to timeouts when using Dev Tunnels.
        /// </summary>
        internal int DevTimeoutSeconds => 30;

        /// <summary>
        /// Returns the webhook name.
        /// </summary>
        internal string Name => $"{GetType().Namespace ?? "root"}.{typeof(TEntity).Name}.{GetType().Name}".ToLowerInvariant();

        /// <summary>
        /// Returns the webhook endpoint.
        /// </summary>
        internal string Endpoint { get; }

        /// <summary>
        /// Returns the webhook endpoint.
        /// </summary>
        internal WebhookType WebhookType { get; }

        /// <summary>
        /// Operation for <see cref="AdmissionOperations.Create"/>.
        /// </summary>
        /// <param name="newEntity">The newly created entity that should be validated.</param>
        /// <param name="dryRun">A boolean that indicates if this call was initiated from a dry run (kubectl ... --dry-run).</param>
        /// <returns>A result that is transmitted to Kubernetes.</returns>
        TResult Create(TEntity newEntity, bool dryRun);

        /// <inheritdoc cref="Create"/>
        Task<TResult> CreateAsync(TEntity newEntity, bool dryRun)
        {
            using var activity = TraceContext.ActivitySource?.StartActivity();

            return Task.FromResult(Create(newEntity, dryRun));
        }

        /// <summary>
        /// Operation for <see cref="AdmissionOperations.Update"/>.
        /// </summary>
        /// <param name="oldEntity">The old entity. This is the "old" version before the update.</param>
        /// <param name="newEntity">The new entity. This is the "new" version after the update is performed.</param>
        /// <param name="dryRun">A boolean that indicates if this call was initiated from a dry run (kubectl ... --dry-run).</param>
        /// <returns>A result that is transmitted to Kubernetes.</returns>
        TResult Update(TEntity oldEntity, TEntity newEntity, bool dryRun);

        /// <inheritdoc cref="Update"/>
        Task<TResult> UpdateAsync(TEntity oldEntity, TEntity newEntity, bool dryRun)
        {
            using var activity = TraceContext.ActivitySource?.StartActivity();

            return Task.FromResult(Update(oldEntity, newEntity, dryRun));
        }

        /// <summary>
        /// Operation for <see cref="AdmissionOperations.Delete"/>.
        /// </summary>
        /// <param name="oldEntity">The entity that is being deleted.</param>
        /// <param name="dryRun">A boolean that indicates if this call was initiated from a dry run (kubectl ... --dry-run).</param>
        /// <returns>A result that is transmitted to Kubernetes.</returns>
        TResult Delete(TEntity oldEntity, bool dryRun);

        /// <inheritdoc cref="Delete"/>
        Task<TResult> DeleteAsync(TEntity oldEntity, bool dryRun)
        {
            using var activity = TraceContext.ActivitySource?.StartActivity();

            return Task.FromResult(Delete(oldEntity, dryRun));
        }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        /// <param name="result"></param>
        /// <param name="request"></param>
        /// <returns>The <see cref="AdmissionResponse"/>.</returns>
        internal AdmissionResponse TransformResult(TResult result, AdmissionRequest<TEntity> request);

        /// <summary>
        /// Registers the webhook endpoints.
        /// </summary>
        /// <param name="endpoints">Specifies the endpoints.</param>
        /// <param name="serviceProvider">Specifies the dependencu injection service provider.</param>
        internal void Register( IEndpointRouteBuilder endpoints, IServiceProvider serviceProvider)
        {
            Covenant.Requires<ArgumentNullException>(endpoints != null, nameof(endpoints));
            Covenant.Requires<ArgumentNullException>(serviceProvider != null, nameof(serviceProvider));

            var logger           = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<IAdmissionWebhook<TEntity, TResult>>();
            var operatorSettings = serviceProvider.GetRequiredService<OperatorSettings>();
            var metrics          = new WebhookMetrics<TEntity>(operatorSettings, Endpoint);

            logger?.LogInformationEx(() => $"Registered webhook at [{Endpoint}]");

            endpoints.MapPost(
                Endpoint,
                async context =>
                {
                    using var activity = Activity.Current;
                    using var inFlight = metrics.RequestsInFlight.TrackInProgress();
                    using var timer    = metrics.LatencySeconds.NewTimer();

                    try
                    {
                        if (!context.Request.HasJsonContentType())
                        {
                            logger?.LogErrorEx(() => "Admission request has no json content type.");
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            return;
                        }

                        using var reader = new StreamReader(context.Request.Body);

                        var review = KubernetesJson.Deserialize<AdmissionReview<TEntity>>(await reader.ReadToEndAsync());

                        if (review.Request == null)
                        {
                            logger?.LogErrorEx(() => "The admission request contained no request object.");
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            await context.Response.WriteAsync("The request must contain AdmissionRequest information.");
                            return;
                        }

                        AdmissionResponse response;

                        try
                        {
                            if (context.RequestServices.GetRequiredService(GetType()) is not
                                IAdmissionWebhook<TEntity, TResult>
                                webhook)
                            {
                                throw new Exception("Object is not a valid IAdmissionWebhook<TEntity, TResult>");
                            }

                            var @object   = KubernetesJson.Deserialize<TEntity>(KubernetesJson.Serialize(review.Request.Object));
                            var oldObject = KubernetesJson.Deserialize<TEntity>(KubernetesJson.Serialize(review.Request.OldObject));

                            logger?.LogInformationEx(() => @$"Admission with method ""{review.Request.Operation}"".");

                            TResult result;

                            switch (review.Request.Operation)
                            {
                                case "CREATE":

                                    result = await webhook.CreateAsync(@object, review.Request.DryRun);

                                    break;

                                case "UPDATE":

                                    result = await webhook.UpdateAsync(oldObject, @object, review.Request.DryRun);
                                    break;

                                case "DELETE":

                                    result = await webhook.DeleteAsync(@object, review.Request.DryRun);
                                    break;

                                default:
                                    throw new InvalidOperationException();
                            }

                            response = TransformResult(result, review.Request);

                        }
                        catch (Exception e)
                        {
                            logger?.LogErrorEx(e, "An error happened during admission.");

                            response = new AdmissionResponse()
                            {
                                Allowed = false,
                                Status  = new()
                                {
                                    Code    = StatusCodes.Status500InternalServerError,
                                    Message = "There was an internal server error.",
                                },
                            };
                        }

                        review.Response     = response;
                        review.Response.Uid = review.Request.Uid;

                        logger?.LogInformationEx(() => @$"AdmissionHook ""{Name}"" did return ""{review.Response?.Allowed}"" for ""{review.Request.Operation}"".");

                        review.Request = null;

                        metrics.RequestsTotal.WithLabels(new string[] { operatorSettings.Name, Endpoint, response.Status?.Code.ToString()}).Inc();
                        await context.Response.WriteAsJsonAsync(review);
                    }
                    catch (Exception e)
                    {
                        logger?.LogErrorEx(e);
                    }
                });
        }
    }
}
