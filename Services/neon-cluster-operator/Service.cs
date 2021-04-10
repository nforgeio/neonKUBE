//------------------------------------------------------------------------------
// FILE:         NeonClusterOperator.cs
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

namespace NeonClusterOperator
{
    public partial class Service : NeonService
    {
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

            // $todo(marcusbooyah):
            // Implement the cluster manager. For now we're just having it sleep/loop

            // Launch the sub-tasks.  These will run until the service is terminated.

            while (true)
            {
                await Task.Delay(10000);
            }

            //return 0;
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
