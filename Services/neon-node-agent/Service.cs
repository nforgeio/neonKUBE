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
    ///     <term><b>CONTAINERREGISTRY_RECONCILED_NOCHANGE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the interval at which <b>reconcile</b> events will be requeued
    ///     for <b>ContainerRegistry</b> resources as a backstop to ensure that the operator state
    ///     remains in sync with the API server.  This defaults to <b>5 minutes</b>.
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
    ///     <b>timespan:</b> Specifies the maximum requeue time for ContainerRegistry
    ///     handler exceptions.  This defaults to <b>10</b> minutes.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NODETASK_RECONCILED_NOCHANGE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the interval at which <b>reconcile</b> events will be requeued
    ///     for <b>NodeTask</b> resources as a backstop to ensure that the operator state
    ///     remains in sync with the API server.  This defaults to <b>5 minutes</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NODETASK_ERROR_MIN_REQUEUE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the minimum requeue interval to use when an
    ///     exception is thrown when handling NodeTask events.  This
    ///     value will be doubled when subsequent events also fail until the
    ///     requeue time maxes out at <b>CONTAINERREGISTRY_ERROR_MIN_REQUEUE_INTERVAL</b>.
    ///     This defaults to <b>15 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NODETASK_ERROR_MIN_REQUEUE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the maximum requeue time for NodeTask
    ///     handler exceptions.  This defaults to <b>10</b> minutes.
    ///     </description>
    /// </item>
    /// <item>
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
            // Start the operator controllers.  Note that we're not going to await
            // this and will use the termination signal to exit.

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
                            logging.AddProvider(base.LogManager);
                        })
                    .ConfigureWebHostDefaults(builder => builder.UseStartup<Startup>())
                    .Build()
                    .RunOperatorAsync(Array.Empty<string>());

            // Indicate that the service is running.

            await StartedAsync();

            // Handle termination gracefully.

            await Terminator.StopEvent.WaitAsync();
            Terminator.ReadyToExit();

            return 0;
        }
    }
}
