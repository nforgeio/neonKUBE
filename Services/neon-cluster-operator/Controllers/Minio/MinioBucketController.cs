//-----------------------------------------------------------------------------
// FILE:	    MinioBucketController.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using Neon.Kube.Operator;
using Neon.Kube.ResourceDefinitions;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using k8s;
using k8s.Autorest;
using k8s.Models;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

using Newtonsoft.Json;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz.Impl;
using Quartz;
using Npgsql;
using k8s.KubeConfigModels;
using Microsoft.AspNetCore.Mvc;
using Neon.Cryptography;
using Octokit;
using System.Text.RegularExpressions;
using Minio;
using Minio.Exceptions;
using Google.Protobuf.WellKnownTypes;

namespace NeonClusterOperator
{
    /// <summary>
    /// Manages MinioBucket LDAP database.
    /// </summary>
    [EntityRbac(typeof(V1MinioBucket), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Patch | RbacVerb.Watch | RbacVerb.Update)]
    public class MinioBucketController : IOperatorController<V1MinioBucket>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger log = TelemetryHub.CreateLogger<MinioBucketController>();

        private static ResourceManager<V1MinioBucket, MinioBucketController> resourceManager;
        private MinioClient minioClient;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static MinioBucketController()
        {
        }

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client to use.</param>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task StartAsync(
            IKubernetes k8s,
            IServiceProvider serviceProvider)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            // Load the configuration settings.

            var leaderConfig =
                new LeaderElectionConfig(
                    k8s,
                    @namespace: KubeNamespace.NeonSystem,
                    leaseName: $"{Program.Service.Name}.miniobucket",
                    identity: Pod.Name,
                    promotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_promoted", "Leader promotions"),
                    demotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_demoted", "Leader demotions"),
                    newLeaderCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_new_leader", "Leadership changes"));

            var options = new ResourceManagerOptions()
            {
                ErrorMaxRetryCount = int.MaxValue,
                ErrorMaxRequeueInterval = TimeSpan.FromMinutes(10),
                ErrorMinRequeueInterval = TimeSpan.FromSeconds(60),
                IdleCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_idle", "IDLE events processed."),
                ReconcileCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_idle", "RECONCILE events processed."),
                DeleteCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_idle", "DELETED events processed."),
                FinalizeCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_finalize", "FINALIZE events processed."),
                StatusModifyCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_idle", "STATUS-MODIFY events processed."),
                IdleErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_idle_error", "Failed IDLE event processing."),
                ReconcileErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_reconcile_error", "Failed RECONCILE event processing."),
                DeleteErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_delete_error", "Failed DELETE event processing."),
                StatusModifyErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_statusmodify_error", "Failed STATUS-MODIFY events processing."),
                FinalizeErrorCounter     = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}miniobucket_finalize_error", "Failed FINALIZE events processing.")
            };

            resourceManager = new ResourceManager<V1MinioBucket, MinioBucketController>(
                k8s,
                options: options,
                leaderConfig: leaderConfig,
                serviceProvider: serviceProvider);

            await resourceManager.StartAsync();
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MinioBucketController(
            IKubernetes k8s,
            MinioClient minioClient)
        {
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires(minioClient != null, nameof(minioClient));

            this.k8s         = k8s;
            this.minioClient = minioClient;
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

                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null)
                {
                    return null;
                }

                try
                {
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
                }
                catch (MinioException e)
                {
                    Console.WriteLine("Error occurred: " + e);
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

                if (resourceManager == null)
                {
                    return;
                }

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
    }
}
