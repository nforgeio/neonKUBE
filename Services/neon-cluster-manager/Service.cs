//------------------------------------------------------------------------------
// FILE:         NeonClusterManager.cs
// CONTRIBUTOR:  Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;
using System.Net.Sockets;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Service;
using Neon.Data;
using Neon.Net;

using Helm.Helm;
using Newtonsoft.Json;
using YamlDotNet.RepresentationModel;

using k8s;
using k8s.Models;

namespace NeonClusterManager
{
    public partial class Service : NeonService
    {
        private static TimeSpan logPurgerInterval;

        private static Kubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="serviceMap">Optionally specifies the service map.</param>
        public Service(string name, ServiceMap serviceMap = null)
            : base(name, serviceMap: serviceMap)
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            // Let KubeService know that we're running.

            await SetRunningAsync();
            await SetupClusterAsync();

            logPurgerInterval = TimeSpan.FromSeconds(int.Parse(GetEnvironmentVariable("LOG_PURGE_INTERVAL") ?? "3600"));

            Log.LogInfo(() => $"Using setting [logPurgeInterval={logPurgerInterval.TotalSeconds}]");

            var retentionDays = int.Parse(Environment.GetEnvironmentVariable("RETENTION_DAYS") ?? "14");

            Log.LogInfo(() => $"Using setting [retentionDays={retentionDays}]");

            // Launch the sub-tasks.  These will run until the service is terminated.

            var tasks = new List<Task>();

            // Start a task that checks for Elasticsearch [logstash] and [metricbeat] indexes
            // that are older than the number of retention days.

            tasks.Add(LogPurgerAsync(logPurgerInterval, retentionDays));

            // Wait for all tasks to exit cleanly for a normal shutdown.

            await NeonHelper.WaitAllAsync(tasks);
            return 0;
        }

        /// <summary>
        /// Setus up the cluster.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task SetupClusterAsync()
        {
            var isInCluster = KubernetesClientConfiguration.IsInCluster();

            Log.LogInfo(isInCluster ? "Running in Kubernetes Cluster." : "Not running in Kubernetes Cluster.");

            k8s = new Kubernetes(isInCluster ? KubernetesClientConfiguration.InClusterConfig() : KubernetesClientConfiguration.BuildDefaultConfig());

            var tasks = new List<Task>();

            tasks.Add(NeonSystemSetup());

            await NeonHelper.WaitAllAsync(tasks);

            await Task.CompletedTask;
        }
    }
}
