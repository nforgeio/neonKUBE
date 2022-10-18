using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

using Newtonsoft.Json;

using k8s;
using k8s.Models;
using Neon.Diagnostics;
using Neon.Common;
using Microsoft.Extensions.Logging;

namespace Neon.Kube.Operator
{
    public interface IAdmissionWebhook<TEntity, TResult>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        where TResult : AdmissionResult, new()
    {
        public AdmissionOperations Operations { get; }

        internal string Name =>
            $"{GetType().Namespace ?? "root"}.{typeof(TEntity).Name}.{GetType().Name}".ToLowerInvariant();

        internal string Endpoint { get; }

        internal WebhookType WebhookType { get; }

        /// <summary>
        /// Operation for <see cref="AdmissionOperations.Create"/>.
        /// </summary>
        /// <param name="newEntity">The newly created entity that should be validated.</param>
        /// <param name="dryRun">A boolean that indicates if this call was initiated from a dry run (kubectl ... --dry-run).</param>
        /// <returns>A result that is transmitted to kubernetes.</returns>
        TResult Create(TEntity newEntity, bool dryRun);

        /// <inheritdoc cref="Create"/>
        Task<TResult> CreateAsync(TEntity newEntity, bool dryRun)
        {
            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                return Task.FromResult(Create(newEntity, dryRun));
            }
        }

        /// <summary>
        /// Operation for <see cref="AdmissionOperations.Update"/>.
        /// </summary>
        /// <param name="oldEntity">The old entity. This is the "old" version before the update.</param>
        /// <param name="newEntity">The new entity. This is the "new" version after the update is performed.</param>
        /// <param name="dryRun">A boolean that indicates if this call was initiated from a dry run (kubectl ... --dry-run).</param>
        /// <returns>A result that is transmitted to kubernetes.</returns>
        TResult Update(TEntity oldEntity, TEntity newEntity, bool dryRun);

        /// <inheritdoc cref="Update"/>
        Task<TResult> UpdateAsync(TEntity oldEntity, TEntity newEntity, bool dryRun)
        {
            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                return Task.FromResult(Update(oldEntity, newEntity, dryRun));
            }
        }

        /// <summary>
        /// Operation for <see cref="AdmissionOperations.Delete"/>.
        /// </summary>
        /// <param name="oldEntity">The entity that is being deleted.</param>
        /// <param name="dryRun">A boolean that indicates if this call was initiated from a dry run (kubectl ... --dry-run).</param>
        /// <returns>A result that is transmitted to kubernetes.</returns>
        TResult Delete(TEntity oldEntity, bool dryRun);

        /// <inheritdoc cref="Delete"/>
        Task<TResult> DeleteAsync(TEntity oldEntity, bool dryRun)
        {
            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                return Task.FromResult(Delete(oldEntity, dryRun));
            }
        }

        internal AdmissionResponse TransformResult(
            TResult result,
            AdmissionRequest<TEntity> request);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <returns></returns>
        internal void Register(IEndpointRouteBuilder endpoints) =>
                endpoints.MapPost(
                Endpoint,
                async context =>
                {
                    using (var activity = TelemetryHub.ActivitySource.StartActivity())
                    {
                        var logger = TelemetryHub.LoggerFactory.CreateLogger(GetType().Name);

                        try
                        {
                            if (!context.Request.HasJsonContentType())
                            {
                                logger.LogErrorEx(() => "Admission request has no json content type.");
                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                return;
                            }

                            using var reader = new StreamReader(context.Request.Body);

                            var review = KubernetesJson.Deserialize<AdmissionReview<TEntity>>(await reader.ReadToEndAsync());

                            if (review.Request == null)
                            {
                                logger.LogErrorEx(() => "The admission request contained no request object.");
                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                await context.Response.WriteAsync("The request must contain AdmissionRequest information.");
                                return;
                            }

                            AdmissionResponse response;
                            try
                            {
                                using var scope = context.RequestServices.CreateScope();
                                if (scope.ServiceProvider.GetRequiredService(GetType()) is not
                                    IAdmissionWebhook<TEntity, TResult>
                                    webhook)
                                {
                                    throw new Exception("Object is not a valid IAdmissionWebhook<TEntity, TResult>");
                                }

                                var @object = KubernetesJson.Deserialize<TEntity>(KubernetesJson.Serialize(review.Request.Object));
                                var oldObject = KubernetesJson.Deserialize<TEntity>(KubernetesJson.Serialize(review.Request.OldObject));
                                
                                logger.LogInformationEx(() => @$"Admission with method ""{review.Request.Operation}"".");

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
                            catch (Exception ex)
                            {
                                logger.LogErrorEx(ex, "An error happened during admission.");
                                response = new AdmissionResponse()
                                {
                                    Allowed = false,
                                    Status = new()
                                    {
                                        Code = StatusCodes.Status500InternalServerError,
                                        Message = "There was an internal server error.",
                                    },
                                };
                            }

                            review.Response = response;
                            review.Response.Uid = review.Request.Uid;

                            logger.LogInformationEx(() =>
                                @$"AdmissionHook ""{Name}"" did return ""{review.Response?.Allowed}"" for ""{review.Request.Operation}"".");

                            review.Request = null;

                            await context.Response.WriteAsJsonAsync(review);
                        }
                        catch (Exception e)
                        {
                            logger.LogErrorEx(e);
                        }
                    }
                });
    }
}
