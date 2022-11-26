//-----------------------------------------------------------------------------
// FILE:	    MinioBucketFinalizer.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Kube.Operator;
using Neon.Kube.ResourceDefinitions;
using Neon.Tasks;

using k8s.Models;
using Minio;
using Neon.Kube;
using System.Text.Json;
using k8s;
using System.Diagnostics.Contracts;

namespace NeonClusterOperator
{
    /// <summary>
    /// Finalizes deletion of <see cref="V1MinioBucket"/> resources.
    /// </summary>
    public class MinioBucketFinalizer : IResourceFinalizer<V1MinioBucket>
    {
        private ILogger logger;
        private IKubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="k8s">The Kubernetes client.</param>
        public MinioBucketFinalizer(
            ILogger logger,
            IKubernetes k8s)
        {
            Covenant.Requires(logger != null, nameof(logger));
            Covenant.Requires(k8s != null, nameof(k8s));

            this.logger = logger;
            this.k8s = k8s;
        }

        /// <inheritdoc/>
        public async Task FinalizeAsync(V1MinioBucket resource)
        {
            await SyncContext.Clear;

            logger.LogInformationEx(() => $"Finalizing {resource.Name()}");

            var minioClient = await GetMinioClientAsync(resource);

            bool exists = await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(resource.Name()));

            if (exists) 
            { 
                await minioClient.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(resource.Name()));
                logger.LogInformationEx(() => $"Bucket {resource.Name()} deleted.");
            }
            else
            {
                logger.LogInformationEx(() => $"Bucket {resource.Name()} doesn't exist.");
            }
        }

        private async Task<MinioClient> GetMinioClientAsync(V1MinioBucket resource)
        {
            var tenant        = await k8s.ReadNamespacedCustomObjectAsync<V1MinioTenant>(resource.Namespace(), resource.Spec.Tenant);
            var minioEndpoint = $"{tenant.Name()}.{tenant.Namespace()}";
            var secretName    = ((JsonElement)(tenant.Spec)).GetProperty("credsSecret").GetProperty("name").GetString();
            var secret        = await k8s.ReadNamespacedSecretAsync(secretName, resource.Namespace());
            var minioClient   = new MinioClient()
                                  .WithEndpoint(minioEndpoint)
                                  .WithCredentials(Encoding.UTF8.GetString(secret.Data["accesskey"]), Encoding.UTF8.GetString(secret.Data["secretkey"]))
                                  .Build();

            return minioClient;
        }
    }
}
