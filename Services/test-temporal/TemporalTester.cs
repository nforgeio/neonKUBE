//------------------------------------------------------------------------------
// FILE:         TemporalTester.cs
// CONTRIBUTOR:  Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube.Service;
using Neon.Service;
using Neon.Temporal;

namespace TemporalService
{
    public partial class TemporalTester : KubeService
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceMap">The service map.</param>
        /// <param name="name">The service name.</param>
        public TemporalTester(ServiceMap serviceMap, string name)
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
            // Verify the environment variables.

            var settings   = new TemporalSettings();
            var hostPort   = GetEnvironmentVariable("TEMPORAL_HOSTPORT");
            var @namespace = GetEnvironmentVariable("TEMPORAL_NAMESPACE");
            var taskList   = GetEnvironmentVariable("TEMPORAL_TASKLIST");
            var error      = false;

            Log.LogInfo(() => $"TEMPORAL_HOSTPORT:  {hostPort}");
            Log.LogInfo(() => $"TEMPORAL_NAMESPACE: {@namespace}");
            Log.LogInfo(() => $"TEMPORAL_TASKLIST:  {taskList}");

            if (string.IsNullOrEmpty(hostPort))
            {
                error = true;
                Log.LogError("The [TEMPORAL_HOSTPORT] environment variable is required.");
            }

            settings.HostPort = hostPort;

            if (string.IsNullOrEmpty(@namespace))
            {
                error = true;
                Log.LogError("The [TEMPORAL_NAMESPACE] environment variable is required.");
            }

            if (string.IsNullOrEmpty(taskList))
            {
                error = true;
                Log.LogError("The [TEMPORAL_TASKLIST] environment variable is required.");
            }

            if (error)
            {
                return 1;
            }

            // Connect to Temporal and register the workflows and activities.

            Log.LogInfo("Connecting to Temporal.");

            settings.Namespace = @namespace;

            using (var client = await TemporalClient.ConnectAsync(settings))
            {
                // Register the workflows.

                Log.LogInfo("Registering workflows.");
                await client.RegisterAssemblyAsync(Assembly.GetExecutingAssembly());

                // Start the worker.

                Log.LogInfo("Starting worker.");

                using (var worker = client.StartWorkerAsync(taskList))
                {
                    // Let KubeService know that we're running.

                    Log.LogInfo("Ready for work.");
                    await SetRunningAsync();

                    // Wait for the process terminator to signal that the service is stopping.

                    await Terminator.StopEvent.WaitAsync();
                }
            }

            return 0;
        }
    }
}
