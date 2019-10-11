//------------------------------------------------------------------------------
// FILE:         CadenceTester.cs
// CONTRIBUTOR:  Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube.Service;
using Neon.Service;

namespace CadenceTester
{
    public partial class CadenceTester : KubeService
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceMap">The service map.</param>
        /// <param name="name">The service name.</param>
        public CadenceTester(ServiceMap serviceMap, string name)
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

            var settings = new CadenceSettings();
            var servers  = GetEnvironmentVariable("CADENCE_SERVERS");
            var domain   = GetEnvironmentVariable("CADENCE_DOMAIN");
            var taskList = GetEnvironmentVariable("CADENCE_TASKLIST");
            var error    = false;

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

            using (var client = await CadenceClient.ConnectAsync(settings))
            {
            }

            // Let KubeService know that we're running.

            SetRunning();

            return 0;
        }
    }
}
