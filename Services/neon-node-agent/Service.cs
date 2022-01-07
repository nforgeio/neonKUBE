//------------------------------------------------------------------------------
// FILE:        Service.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Net.Sockets;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Net;
using Neon.Retry;
using Neon.Service;

using k8s;
using k8s.Models;
using KubeOps.Operator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using YamlDotNet.RepresentationModel;

namespace NeonNodeAgent
{
    /// <summary>
    /// Implements the <b>neon-node-agent</b> service.
    /// </summary>
    public partial class Service : NeonService
    {
        private const string StateTable = "state";
        
        private static KubernetesWithRetry k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        public Service(string name)
            : base(name, version: KubeVersions.NeonKube)
        {
            k8s = new KubernetesWithRetry(KubernetesClientConfiguration.BuildDefaultConfig());

            k8s.RetryPolicy = new ExponentialRetryPolicy(
                e => true,
                maxAttempts:          int.MaxValue,
                initialRetryInterval: TimeSpan.FromSeconds(0.25),
                maxRetryInterval:     TimeSpan.FromSeconds(10),
                timeout:              TimeSpan.FromMinutes(5));
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            // Start the operator controllers.  Note that we're not going to await
            // this and will use the termination signal to known when to exit.

            _ = Host.CreateDefaultBuilder()
                    .ConfigureLogging(
                        logging =>
                        {
                            logging.ClearProviders();
                            logging.AddProvider(new LogManager(version: base.Version));
                        })
                    .ConfigureWebHostDefaults(builder => builder.UseStartup<Startup>())
                    .Build()
                    .RunOperatorAsync(Array.Empty<string>());

            // Let NeonService know that we're running.

            await StartedAsync();

            // Launch the sub-tasks.  These will run until the service is terminated.

            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(5));

                // $todo(jefflill):
                // 
                // We're disabling all activities until we code proper operators.

                // await CheckNodeImagesAsync();
            }
        }
    }
}