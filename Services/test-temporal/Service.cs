//------------------------------------------------------------------------------
// FILE:         Service.cs
// CONTRIBUTOR:  Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Service;
using Neon.Temporal;

namespace TemporalService
{
    public partial class Service : NeonService
    {
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
            // Verify the environment variables.

            var settings   = new TemporalSettings();
            var hostPort   = GetEnvironmentVariable("TEMPORAL_HOSTPORT");
            var @namespace = GetEnvironmentVariable("TEMPORAL_NAMESPACE");
            var taskQueue  = GetEnvironmentVariable("TEMPORAL_TASKQUEUE");
            var error      = false;

            Log.LogInfo(() => $"TEMPORAL_HOSTPORT:  {hostPort}");
            Log.LogInfo(() => $"TEMPORAL_NAMESPACE: {@namespace}");
            Log.LogInfo(() => $"TEMPORAL_TASKQUEUE: {taskQueue}");

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

            if (string.IsNullOrEmpty(taskQueue))
            {
                error = true;
                Log.LogError("The [TEMPORAL_TASKQUEUE] environment variable is required.");
            }

            if (error)
            {
                return 1;
            }

            // Connect to Temporal and register the workflows and activities.

            Log.LogInfo("Connecting to Temporal.");

            settings.Namespace = @namespace;
            settings.TaskQueue = taskQueue;

            using (var client = await TemporalClient.ConnectAsync(settings))
            {
                // Create a worker and register the workflow and activity 
                // implementations to let Temporal know we're open for business.

                using (var worker = await client.NewWorkerAsync())
                {
                    Log.LogInfo("Registering workflows.");
                    await worker.RegisterAssemblyAsync(Assembly.GetExecutingAssembly());

                    Log.LogInfo("Starting worker.");
                    await worker.StartAsync();

                    // Let NeonService know that we're running.

                    Log.LogInfo("Ready for work.");
                    await StartedAsync();

                    // Wait for the process terminator to signal that the service is stopping.

                    await Terminator.StopEvent.WaitAsync();
                    Terminator.ReadyToExit();
                }
            }

            return 0;
        }
    }
}
