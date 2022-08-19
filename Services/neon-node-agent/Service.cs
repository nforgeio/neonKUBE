//------------------------------------------------------------------------------
// FILE:        Service.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using Neon.Kube.Operator;
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
    /// <remarks>
    /// <para><b>ENVIRONMENT VARIABLES</b></para>
    /// <para>
    /// The <b>neon-node-agent</b> is configured using these environment variables:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>WATCHER_TIMEOUT_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the maximum time the resource watcher will wait without
    ///     a response before creating a new request.  This defaults to <b>2 minutes</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>WATCHER_MAX_RETRY_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the maximum time the KubeOps resource watcher will wait
    ///     after a watch failure.  This defaults to <b>15 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>CONTAINERREGISTRY_IDLE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the interval at which IDLE reconcile events will be raised
    ///     for <b>ContainerRegistry</b>.  This defaults to <b>60 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>CONTAINERREGISTRY_ERROR_MIN_REQUEUE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the minimum requeue interval to use when an
    ///     exception is thrown when handling ContainerRegistry events.  This
    ///     value will be doubled when subsequent events also fail until the
    ///     requeue time maxes out at <b>CONTAINERREGISTRY_ERROR_MIN_REQUEUE_INTERVAL</b>.
    ///     This defaults to <b>15 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>CONTAINERREGISTRY_ERROR_MIN_REQUEUE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the interval at which IDLE reconcile events will be raised
    ///     for <b>ContainerRegistry</b>.  This defaults to <b>5 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>CONTAINERREGISTRY_RELOGIN_INTERVAL</b></term>
    ///     <description>
    ///     <para>
    ///     <b>timespan:</b> Specifies the interval at which \<b>ContainerRegistry</b> will
    ///     force a login to upstream registries to ensure that they're reachable even when
    ///     it appears that the the correct login is active.  This helps to ensure that 
    ///     nodes will converge on having proper registery credentials even after these
    ///     got corrupted somehow (e.g. somebody logged out manually or CRI-O was reinstalled).
    ///     </para>
    ///     <para>
    ///     The control loop randomizes actual logins to prevent the cluster nodes from all
    ///     slamming an upstream registry with logins at the same time.  It does this by
    ///     scheduling the re-logins after:
    ///     </para>
    ///     <code>
    ///     CONTAINERREGISTRY_RELOGIN_INTERVAL + random(CONTAINERREGISTRY_RELOGIN_INTERVAL/4)
    ///     </code>
    ///     <para>
    ///     where `random(CONTAINERREGISTRY_RELOGIN_INTERVAL/4)` is a random interval between
    ///     `0..CONTAINERREGISTRY_RELOGIN_INTERVAL/4`.
    ///     </para>
    ///     <para>
    ///     This defaults to <b>24 hours</b>.
    ///     </para>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NODETASK_IDLE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the interval at which IDLE events will be raised
    ///     for <b>NodeTask</b> resources, giving the operator the chance to manage tasks
    ///     assigned to the node.  This defaults to <b>60 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NODETASK_ERROR_MIN_REQUEUE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the minimum requeue interval to use when an
    ///     exception is thrown when handling NodeTask events.  This
    ///     value will be doubled when subsequent events also fail until the
    ///     requeue time maxes out at <b>CONTAINERREGISTRY_ERROR_MIN_REQUEUE_INTERVAL</b>.
    ///     This defaults to <b>5 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NODETASK_ERROR_MIN_REQUEUE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the maximum requeue time for NodeTask
    ///     handler exceptions.  This defaults to <b>60 seconds</b>.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    public partial class Service : NeonService
    {
        private const string StateTable = "state";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        public Service(string name)
            : base(name, version: KubeVersions.NeonKube, metricsPrefix: "neonnodeagent", logFilter: OperatorHelper.LogFilter)
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
            //-----------------------------------------------------------------
            // Start the controllers: these need to be started before starting KubeOps

            var k8s = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());

            //################################
            // $debug(jefflill): RESTORE THIS!
            //await NodeTaskController.StartAsync(k8s);
            //################################

            await ContainerRegistryController.StartAsync(k8s);

            //-----------------------------------------------------------------
            // Start KubeOps.

            // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1599
            //
            // We're temporarily using our poor man's operator

#if DISABLED
            _ = Host.CreateDefaultBuilder()
                    .ConfigureHostOptions(
                        options =>
                        {
                            // Ensure that the processor terminator and ASP.NET shutdown times match.

                            options.ShutdownTimeout = ProcessTerminator.DefaultMinShutdownTime;
                        })
                    .ConfigureAppConfiguration(
                        (hostingContext, config) =>
                        {
                            // $note(jefflill): 
                            //
                            // The .NET runtime watches the entire file system for configuration
                            // changes which can cause real problems on Linux.  We're working around
                            // this by removing all configuration sources which we aren't using
                            // anyway for Kubernetes apps.
                            //
                            // https://github.com/nforgeio/neonKUBE/issues/1390

                            config.Sources.Clear();
                        })
                    .ConfigureLogging(
                        logging =>
                        {
                            logging.ClearProviders();
                            logging.AddProvider(base.TelemetryHub);
                        })
                    .ConfigureWebHostDefaults(builder => builder.UseStartup<Startup>())
                    .Build()
                    .RunOperatorAsync(Array.Empty<string>());
#endif

            // Indicate that the service is running.

            await StartedAsync();

            // Handle termination gracefully.

            await Terminator.StopEvent.WaitAsync();
            Terminator.ReadyToExit();

            return 0;
        }
    }
}
