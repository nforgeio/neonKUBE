//------------------------------------------------------------------------------
// FILE:         ClusterManager.cs
// CONTRIBUTOR:  Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
using Neon.Kube.Service;
using Neon.Service;
using Neon.Data;
using Neon.Net;

using Newtonsoft.Json;

using YamlDotNet.RepresentationModel;

using k8s;
using k8s.Models;

using Helm.Helm;

using Couchbase;
using Couchbase.Linq;

namespace ClusterManager
{
    public partial class ClusterManager : KubeService
    {
        private static TimeSpan logPurgerInterval;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceMap">The service map.</param>
        /// <param name="name">The service name.</param>
        public ClusterManager(ServiceMap serviceMap, string name)
            : base(serviceMap, name, ThisAssembly.Git.Branch, ThisAssembly.Git.Commit, ThisAssembly.Git.IsDirty)
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
            await SetupClusterAsync();
            
            logPurgerInterval = TimeSpan.FromSeconds(3600);

            Log.LogInfo(() => $"Using setting [logPurgeInterval={logPurgerInterval.TotalSeconds}]");
            
            // Launch the sub-tasks.  These will run until the service is terminated.

            var tasks = new List<Task>();

            // Start a task that checks for Elasticsearch [logstash] and [metricbeat] indexes
            // that are older than the number of retention days.

            tasks.Add(LogPurgerAsync(logPurgerInterval));
            
            // Wait for all tasks to exit cleanly for a normal shutdown.

            await NeonHelper.WaitAllAsync(tasks);
            return 0;
        }


        public async Task SetupClusterAsync()
        {
            KibanaSetup();
            await Task.FromResult(0);
        }
        /// <summary>
        /// Returns the program version as the Git branch and commit and an optional
        /// indication of whether the program was build from a dirty branch.
        /// </summary>
        public static string GitVersion
            {
                get
                {
                    var version = $"{ThisAssembly.Git.Branch}-{ThisAssembly.Git.Commit}";

#pragma warning disable 162 // Unreachable code

                    //if (ThisAssembly.Git.IsDirty)
                    //{
                    //    version += "-DIRTY";
                    //}

#pragma warning restore 162 // Unreachable code

                    return version;
                }
            }
    }
}
