//-----------------------------------------------------------------------------
// FILE:	    MinioBucketController.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.Logging;

using JsonDiffPatch;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Minio;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Newtonsoft.Json;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Minio;
using Minio.Exceptions;

namespace NeonClusterOperator
{
    /// <summary>
    /// Manages MinioBucket LDAP database.
    /// </summary>
    public class MinioBucketController : IOperatorController<V1MinioBucket>
    {
        //---------------------------------------------------------------------
        // Static members


        private static readonly ILogger log = TelemetryHub.CreateLogger<MinioBucketController>();
        private const string MinioExe = "/mc";
        private MinioClient minioClient;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static MinioBucketController()
        {
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;
        private readonly IFinalizerManager<V1MinioBucket> finalizerManager;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MinioBucketController(
            IKubernetes k8s,
            IFinalizerManager<V1MinioBucket> manager)
        {
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires(manager != null, nameof(manager));

            this.k8s = k8s;
            this.finalizerManager = manager;
        }

        /// <summary>
        /// Called periodically to allow the operator to perform global events.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task IdleAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx("[IDLE]");
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1MinioBucket resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("resource", nameof(V1MinioBucket)));

                await finalizerManager.RegisterAllFinalizersAsync(resource);

                try
                {
                    minioClient = await GetMinioClientAsync(resource);

                    // Create bucket if it doesn't exist.
                    bool exists = await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(resource.Name()));

                    if (exists)
                    {
                        log.LogInformationEx(() => $"BUCKET [{resource.Name()}] already exists.");
                    }
                    else
                    {
                        var args = new MakeBucketArgs().WithBucket(resource.Name());

                        if (!string.IsNullOrEmpty(resource.Spec.Region))
                        {
                            args.WithLocation(resource.Spec.Region);
                        }

                        if (resource.Spec.ObjectLocking)
                        {
                            args.WithObjectLock();
                        }


                        await minioClient.MakeBucketAsync(args);
                        log.LogInformationEx(() => $"BUCKET [{resource.Name()}] created successfully.");
                    }

                    await SetVersioningAsync(resource);
                    await SetQuotaAsync(resource);
                }
                catch (Exception e)
                {
                    log.LogErrorEx(e);
                }
                finally
                {
                    minioClient.Dispose();
                }

                log.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public async Task DeletedAsync(V1MinioBucket resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {

                // Ignore all events when the controller hasn't been started.

                log.LogInformationEx(() => $"DELETED: {resource.Name()}");
            }
        }

        /// <inheritdoc/>
        public async Task OnPromotionAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"PROMOTED");
        }

        /// <inheritdoc/>
        public async Task OnDemotionAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"DEMOTED");
        }

        /// <inheritdoc/>
        public async Task OnNewLeaderAsync(string identity)
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"NEW LEADER: {identity}");
        }

        private async Task<MinioClient> GetMinioClientAsync(V1MinioBucket resource)
        {
            var tenant        = await k8s.CustomObjects.ReadNamespacedCustomObjectAsync<V1MinioTenant>(resource.Namespace(), resource.Spec.Tenant);
            var minioEndpoint = $"{tenant.Name()}.{tenant.Namespace()}";
            var secretName    = ((JsonElement)(tenant.Spec)).GetProperty("credsSecret").GetProperty("name").GetString();
            var secret        = await k8s.CoreV1.ReadNamespacedSecretAsync(secretName, resource.Namespace());
            var accessKey     = Encoding.UTF8.GetString(secret.Data["accesskey"]);
            var secretKey     = Encoding.UTF8.GetString(secret.Data["secretkey"]);
            var minioClient   = new MinioClient()
                                  .WithEndpoint(minioEndpoint)
                                  .WithCredentials(accessKey, secretKey)
                                  .Build();

            await ExecuteMcCommandAsync(
                new string[]
                {
                    "alias",
                    "set",
                    $"{GetTenantAlias(resource)}",
                    $"http://{minioEndpoint}",
                    accessKey,
                    secretKey
                });

            return minioClient;
        }

        private async Task SetQuotaAsync(V1MinioBucket resource)
        {
            if (resource.Spec.Quota == null)
            {
                await ExecuteMcCommandAsync(
                    new string[]
                    {
                        "admin",
                        "bucket",
                        "quota",
                        $"{GetTenantAlias(resource)}/{resource.Name()}",
                        "--clear"
                    });
            }
            else
            {
                await ExecuteMcCommandAsync(
                    new string[]
                    {
                        "admin",
                        "bucket",
                        "quota",
                        $"{GetTenantAlias(resource)}/{resource.Name()}",
                        resource.Spec.Quota.Hard ? "--hard" : null,
                        resource.Spec.Quota.Limit
                    });
            }
        }

        private async Task ExecuteMcCommandAsync(string[] args)
        {
            try
            {
                log.LogDebugEx(() => $"command: {MinioExe} {string.Join(" ", args)}");

                var response = await NeonHelper.ExecuteCaptureAsync(MinioExe,
                    args);

                response.EnsureSuccess();
            }
            catch (Exception e)
            {
                log.LogErrorEx(e);
            }
        }

        private async Task SetVersioningAsync(V1MinioBucket resource)
        {
            var versioning = await minioClient.GetVersioningAsync(new GetVersioningArgs().WithBucket(resource.Name()));

            if (versioning.Status != resource.Spec.Versioning.ToMemberString())
            {
                log.LogInformationEx(() => $"BUCKET [{resource.Name()}] versioning needs to configured.");

                var args = new SetVersioningArgs().WithBucket(resource.Name());

                switch (resource.Spec.Versioning)
                {
                    case VersioningMode.Enabled:

                        args.WithVersioningEnabled();
                        break;

                    case VersioningMode.Suspended:

                        args.WithVersioningSuspended();
                        break;

                    case VersioningMode.Off:
                    default:

                        break;
                }

                await minioClient.SetVersioningAsync(args);

                log.LogInformationEx(() => $"BUCKET [{resource.Name()}] versioning configured successfully.");
            }
        }

        private static string GetTenantAlias(V1MinioBucket resource)
        {
            return $"{resource.Spec.Tenant}-{resource.Namespace()}";
        }
    }
}
