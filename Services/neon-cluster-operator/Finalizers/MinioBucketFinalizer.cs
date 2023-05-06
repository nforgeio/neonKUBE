//-----------------------------------------------------------------------------
// FILE:	    MinioBucketFinalizer.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Minio;
using Neon.Tasks;

using k8s;
using k8s.Models;

using Minio;
using Neon.Kube.Operator.Finalizer;
using System.Reactive.Linq;

namespace NeonClusterOperator
{
    /// <summary>
    /// Finalizes deletion of <see cref="V1MinioBucket"/> resources.
    /// </summary>
    public class MinioBucketFinalizer : IResourceFinalizer<V1MinioBucket>
    {
        private readonly IKubernetes                   k8s;
        private readonly ILogger<MinioBucketFinalizer> logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s">The Kubernetes client.</param>
        /// <param name="logger">The logger.</param>
        public MinioBucketFinalizer(
            IKubernetes k8s,
            ILogger<MinioBucketFinalizer> logger)
        {
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            this.logger = logger;
            this.k8s    = k8s;
        }

        /// <inheritdoc/>
        public async Task FinalizeAsync(V1MinioBucket resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                logger.LogInformationEx(() => $"Finalizing {resource.Name()}");

                var minioClient = await GetMinioClientAsync(resource);
                bool exists     = await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(resource.Name()));

                if (exists)
                {
                    var headers = new Dictionary<string, string>()
                    {
                        { "X-Minio-Force-Delete", "true" }
                    };

                    await minioClient.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(resource.Name()).WithHeaders(headers));
                    logger.LogInformationEx(() => $"Bucket {resource.Name()} deleted.");
                }
                else
                {
                    logger.LogInformationEx(() => $"Bucket {resource.Name()} doesn't exist.");
                }
            }
        }

        private async Task<MinioClient> GetMinioClientAsync(V1MinioBucket resource)
        {
            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                var tenant  = await k8s.CustomObjects.ReadNamespacedCustomObjectAsync<V1MinioTenant>(
                    name:               resource.Spec.Tenant,
                    namespaceParameter: resource.Namespace());

                var minioEndpoint = $"{tenant.Name()}.{tenant.Namespace()}";
                var secretName    = ((JsonElement)(tenant.Spec)).GetProperty("credsSecret").GetProperty("name").GetString();
                var secret        = await k8s.CoreV1.ReadNamespacedSecretAsync(secretName, resource.Namespace());
                var minioClient   = new MinioClient()
                    .WithEndpoint(minioEndpoint)
                    .WithCredentials(Encoding.UTF8.GetString(secret.Data["accesskey"]), Encoding.UTF8.GetString(secret.Data["secretkey"]))
                    .Build();

                return minioClient;
            }
        }
    }
}
