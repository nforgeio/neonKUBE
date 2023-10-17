//-----------------------------------------------------------------------------
// FILE:        MinioBucketController.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.Logging;

using Minio;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Resources.Minio;
using Neon.Net;
using Neon.Operator.Attributes;
using Neon.Operator.Controllers;
using Neon.Operator.Rbac;
using Neon.Operator.Util;
using Neon.Tasks;

using OpenTelemetry.Trace;

namespace NeonClusterOperator
{
    /// <summary>
    /// Manages MinioBucket LDAP database.
    /// </summary>
    [RbacRule<V1MinioBucket>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1MinioTenant>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [RbacRule<V1Secret>(Verbs = RbacVerb.Get)]
    [RbacRule<V1Pod>(Verbs = RbacVerb.List)]
    [ResourceController]
    public class MinioBucketController : ResourceControllerBase<V1MinioBucket>
    {
        //---------------------------------------------------------------------
        // Static members

        private const string            MinioExe = "mc";
        private MinioClient             minioClient;
        private CancellationTokenSource portForwardCts;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static MinioBucketController()
        {
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes                      k8s;
        private readonly ILogger<MinioBucketController>   logger;
        private readonly Service                          service;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MinioBucketController(
            IKubernetes                    k8s,
            ILogger<MinioBucketController> logger,
            Service                        service)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));
            Covenant.Requires<ArgumentNullException>(service != null, nameof(service));

            this.k8s     = k8s;
            this.logger  = logger;
            this.service = service;
        }

        /// <inheritdoc/>
        public override async Task<ResourceControllerResult> ReconcileAsync(V1MinioBucket resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("resource", nameof(V1MinioBucket)));
                
                logger?.LogInformationEx(() => $"Reconciling {resource.GetType().FullName} [{resource.Namespace()}/{resource.Name()}].");

                var patch = OperatorHelper.CreatePatch<V1MinioBucket>();

                patch.Replace(path => path.Status, new V1MinioBucket.V1MinioBucketStatus());
                patch.Replace(path => path.Status.State, "reconciling");

                await k8s.CustomObjects.PatchNamespacedCustomObjectStatusAsync<V1MinioBucket>(
                    patch:              OperatorHelper.ToV1Patch<V1MinioBucket>(patch),
                    name:               resource.Name(),
                    namespaceParameter: resource.Namespace());

                // $debug(jefflill): RESTORE THIS!

#if !TODO
                try
                {
                    minioClient = await GetMinioClientAsync(resource);

                    // Create bucket if it doesn't exist.

                    if (await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(resource.Name())))
                    {
                        logger?.LogInformationEx(() => $"BUCKET [{resource.Name()}] already exists.");
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
                        logger?.LogInformationEx(() => $"BUCKET [{resource.Name()}] created successfully.");
                    }

                    await SetVersioningAsync(resource);
                    await SetQuotaAsync(resource);
                }
                catch (Exception e)
                {
                    logger?.LogErrorEx(e);

                    throw;
                }
                finally
                {
                    minioClient.Dispose();
                    portForwardCts?.Cancel();
                }
#endif

                patch = OperatorHelper.CreatePatch<V1MinioBucket>();

                patch.Replace(path => path.Status, new V1MinioBucket.V1MinioBucketStatus());
                patch.Replace(path => path.Status.State, "reconciled");

                await k8s.CustomObjects.PatchNamespacedCustomObjectStatusAsync<V1MinioBucket>(
                    patch:              OperatorHelper.ToV1Patch<V1MinioBucket>(patch), 
                    name:               resource.Name(), 
                    namespaceParameter: resource.Namespace());

                logger?.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public override async Task DeletedAsync(V1MinioBucket resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {

                // Ignore all events when the controller hasn't been started.

                logger?.LogInformationEx(() => $"DELETED: {resource.Name()}");
            }
        }

        private async Task<MinioClient> GetMinioClientAsync(V1MinioBucket resource)
        {
            var minioClient = new MinioClient();

            var tenant = await k8s.CustomObjects.ReadNamespacedCustomObjectAsync<V1MinioTenant>(
                name:               resource.Spec.Tenant, 
                namespaceParameter: resource.Namespace());

            var minioEndpoint = $"{tenant.Name()}.{tenant.Namespace()}";
            var secretName    = ((JsonElement)(tenant.Spec)).GetProperty("credsSecret").GetProperty("name").GetString();
            var secret        = await k8s.CoreV1.ReadNamespacedSecretAsync(secretName, resource.Namespace());
            var accessKey     = Encoding.UTF8.GetString(secret.Data["accesskey"]);
            var secretKey     = Encoding.UTF8.GetString(secret.Data["secretkey"]);
            var minioPort     = 80;

            if (NeonHelper.IsDevWorkstation) 
            { 
                var pod = (await k8s.CoreV1.ListNamespacedPodAsync(resource.Namespace(), labelSelector: $"v1.min.io/tenant={resource.Spec.Tenant}")).Items.First();

                minioPort      = NetHelper.GetUnusedTcpPort(IPAddress.Loopback);
                portForwardCts = new CancellationTokenSource();

                service.PortForwardManager.StartPodPortForward(
                    name:              pod.Name(), 
                    @namespace:        pod.Namespace(), 
                    localPort:         minioPort, 
                    remotePort:        9000, 
                    cancellationToken: portForwardCts.Token);

                minioEndpoint = $"localhost";
            }

            minioClient
                .WithEndpoint(minioEndpoint, minioPort)
                .WithCredentials(accessKey, secretKey)
                .Build();

            await ExecuteMcCommandAsync(
                new string[]
                {
                    "alias",
                    "set",
                    $"{GetTenantAlias(resource)}",
                    $"http://{minioEndpoint}:{minioPort}",
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
                        "quota",
                        "clear",
                        $"{GetTenantAlias(resource)}/{resource.Name()}"
                    });
            }
            else
            {
                await ExecuteMcCommandAsync(
                    new string[]
                    {
                        "quota",
                        "set",
                        $"{GetTenantAlias(resource)}/{resource.Name()}",
                        "--size",
                        resource.Spec.Quota.Limit
                    });
            }
        }

        private async Task ExecuteMcCommandAsync(string[] args)
        {
            try
            {
                logger?.LogDebugEx(() => $"command: {MinioExe} {string.Join(" ", args)}");

                var response = await NeonHelper.ExecuteCaptureAsync(MinioExe,
                    args);

                response.EnsureSuccess();
            }
            catch (Exception e)
            {
                logger?.LogErrorEx(e);
            }
        }

        private async Task SetVersioningAsync(V1MinioBucket resource)
        {
            var versioning = await minioClient.GetVersioningAsync(new GetVersioningArgs().WithBucket(resource.Name()));

            if (versioning.Status != resource.Spec.Versioning.ToMemberString())
            {
                logger?.LogInformationEx(() => $"BUCKET [{resource.Name()}] versioning needs to configured.");

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

                logger?.LogInformationEx(() => $"BUCKET [{resource.Name()}] versioning configured successfully.");
            }
        }

        private static string GetTenantAlias(V1MinioBucket resource)
        {
            return $"{resource.Spec.Tenant}-{resource.Namespace()}";
        }
    }
}
