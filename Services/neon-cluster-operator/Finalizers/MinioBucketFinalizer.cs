//-----------------------------------------------------------------------------
// FILE:        MinioBucketFinalizer.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
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
using Neon.K8s;
using Neon.Kube.Resources.Minio;
using Neon.Net;
using Neon.Operator.Finalizers;
using Neon.Tasks;

namespace NeonClusterOperator
{
    /// <summary>
    /// Finalizes deletion of <see cref="V1MinioBucket"/> resources.
    /// </summary>
    public class MinioBucketFinalizer : ResourceFinalizerBase<V1MinioBucket>
    {
        private readonly IKubernetes                    k8s;
        private readonly ILogger<MinioBucketFinalizer>  logger;
        private readonly Service                        service;
        private CancellationTokenSource                 portForwardCts;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s">Specifies the Kubernetes client.</param>
        /// <param name="logger">Specifies the logger.</param>
        /// <param name="service">Specifies the parent service.</param>
        public MinioBucketFinalizer(
            IKubernetes                   k8s,
            ILogger<MinioBucketFinalizer> logger,
            Service                       service)
        {
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(service != null, nameof(service));

            this.logger  = logger;
            this.k8s     = k8s;
            this.service = service;
        }

        /// <inheritdoc/>
        public override async Task FinalizeAsync(V1MinioBucket resource, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                logger.LogInformationEx(() => $"Finalizing {resource.Name()}");

                try
                {
                    var minioClient = await GetMinioClientAsync(resource);
                    bool exists     = await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(resource.Name()));

                    if (exists)
                    {
                        var headers = new Dictionary<string, string>()
                        {
                            { "X-Minio-Force-Delete", "true" }
                        };

                        await minioClient.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(resource.Name()).WithHeaders(headers));
                        logger.LogInformationEx(() => $"Bucket [{resource.Name()}] deleted.");
                    }
                    else
                    {
                        logger.LogInformationEx(() => $"Bucket [{resource.Name()}] doesn't exist.");
                    }
                }
                finally
                {
                    portForwardCts?.Cancel();
                }
            }
        }

        private async Task<MinioClient> GetMinioClientAsync(V1MinioBucket resource)
        {
            var minioClient = new MinioClient();

            var tenant = await k8s.CustomObjects.GetNamespacedCustomObjectAsync<V1MinioTenant>(
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

                minioPort = NetHelper.GetUnusedTcpPort(IPAddress.Loopback);
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

            return minioClient;
        }
    }
}
