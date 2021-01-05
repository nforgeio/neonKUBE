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

using Neon.Cadence;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Service;

namespace CadenceService
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

            var settings = new CadenceSettings();
            var servers  = GetEnvironmentVariable("CADENCE_SERVERS");
            var domain   = GetEnvironmentVariable("CADENCE_DOMAIN");
            var taskList = GetEnvironmentVariable("CADENCE_TASKLIST");
            var error    = false;

            Log.LogInfo(() => $"CADENCE_SERVERS:  {servers}");
            Log.LogInfo(() => $"CADENCE_DOMAIN:   {domain}");
            Log.LogInfo(() => $"CADENCE_TASKLIST: {taskList}");

            if (string.IsNullOrEmpty(servers))
            {
                error = true;
                Log.LogError("The [CADENCE_SERVERS] environment variable is required.");
            }

            try
            {
                foreach (var item in servers.Split(','))
                {
                    var uri = new Uri(item.Trim(), UriKind.Absolute);

                    settings.Servers.Add(uri.ToString());
                }
            }
            catch
            {
                error = true;
                Log.LogError(() => $"One or more URIs are invalid: CADENCE_SERVERS={servers}");
            }

            if (string.IsNullOrEmpty(domain))
            {
                error = true;
                Log.LogError("The [CADENCE_DOMAIN] environment variable is required.");
            }

            if (string.IsNullOrEmpty(taskList))
            {
                error = true;
                Log.LogError("The [CADENCE_TASKLIST] environment variable is required.");
            }

            if (error)
            {
                return 1;
            }

            // Connect to Cadence and register the workflows and activities.

            Log.LogInfo("Connecting to Cadence.");

            settings.DefaultDomain = domain;

            using (var client = await CadenceClient.ConnectAsync(settings))
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
