//------------------------------------------------------------------------------
// FILE:         Service.cs
// CONTRIBUTOR:  Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
                    // Indicate that the service is running.

                    Log.LogInfo("Ready for work.");
                    await StartedAsync();
                }
            }

            // Handle termination gracefully.

            await Terminator.StopEvent.WaitAsync();
            Terminator.ReadyToExit();

            return 0;
        }
    }
}
