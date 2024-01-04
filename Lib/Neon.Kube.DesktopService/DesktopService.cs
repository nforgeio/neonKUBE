//-----------------------------------------------------------------------------
// FILE:        DesktopService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.Kube.GrpcProto;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

using OpenTelemetry.Exporter;
using OpenTelemetry;

using ProtoBuf.Grpc.Server;

namespace Neon.Kube.DesktopService
{
    /// <summary>
    /// Implements a gPRC service that implements Hyper-V and other operations that may
    /// require elevated permissions.  The idea is to deploy this within a Windows Service
    /// that runs as administrator so that this service can perform these operations on
    /// behalf of the neon-desktop or neon-cli applications that do not have these
    /// rights.
    /// </summary>
    public sealed class DesktopService : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the log exporter used to relay logs from <b>neon-cli</b> and
        /// <b>neon-desktop</b> to the headend.
        /// </summary>
        public static OtlpLogExporterWrapper LogExporter { get; private set; }

        /// <summary>
        /// Returns the trace exporter used to relay traces from <b>neon-cli</b> and
        /// <b>neon-desktop</b> to the headend.
        /// </summary>
        public static OtlpTraceExporter TraceExporter { get; private set; }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static DesktopService()
        {
            // Initialize the log and trace exporters we'll use to relay logs
            // and traces from neon-desktop and neon-cli to the headend.

            // $note(jefflill):
            //
            // We're not using any batch processors here, so logs and traces will
            // be forwarded immediately to the headend.  This is probably fine for
            // this scenario.
            //
            // It would be easy to add batch processors for this when configuring
            // logging and tracing in neon-cli and neon-desktop, but I don't
            // believe those are the right places for doing batching, especially
            // for neon-cli which will terminate immediately after executing
            // commands.
            //
            // It would probably be relatively easy to do batching here using the
            // standard batch processor and emulating standard pipeline behavior.
            // But frankly, I think that sending logs and traces as soon as we
            // get them is probably the correct behavior anyway.

            var logExporterOptions =
                new OtlpExporterOptions()
                {
                    Endpoint            = KubeEnv.TelemetryLogsUri,
                    ExportProcessorType = ExportProcessorType.Simple,
                    Protocol            = OtlpExportProtocol.Grpc,
                    TimeoutMilliseconds = 1000
                };

            var traceExporterOptions =
                new OtlpExporterOptions()
                {
                    Endpoint            = KubeEnv.TelemetryTracesUri,
                    ExportProcessorType = ExportProcessorType.Simple,
                    Protocol            = OtlpExportProtocol.Grpc,
                    TimeoutMilliseconds = 1000
                };

            LogExporter   = OtlpLogExporterWrapper.Create(logExporterOptions);
            TraceExporter = new OtlpTraceExporter(traceExporterOptions);
        }

        //---------------------------------------------------------------------
        // Instance members

        private bool                                isDisposed = false;
        private readonly string                     socketPath;
        private readonly Task                       task;
        private readonly CancellationTokenSource    cts;

        /// <summary>
        /// <para>
        /// This constructor starts the server using a Unix domain socket at the 
        /// specified file system path.  The server will run until disposed.
        /// </para>
        /// <note>
        /// This service is currently exposed as HTTP, not HTTPS.
        /// </note>
        /// </summary>
        /// <param name="socketPath">
        /// Optionally overrides the path to the Unix domain socket path.  This defaults to 
        /// <see cref="KubeHelper.WinDesktopServiceSocketPath"/> where <b>neon-desktop</b> 
        /// and <b>neon-cli</b> expect it to be.
        /// </param>
        /// <exception cref="GrpcServiceException">Thrown when the service could not be started.</exception>
        public DesktopService(string socketPath = null)
        {
            this.socketPath = socketPath ??= KubeHelper.WinDesktopServiceSocketPath;

            // Try to remove any existing socket file and if that fails we're
            // going to assume that another service is already running on the
            // socket.

            try
            {
                if (File.Exists(socketPath))
                {
                    File.Delete(socketPath);
                }
            }
            catch (Exception e)
            {
                throw new GrpcServiceException($"Cannot start service using Unix socket at: {socketPath}", e);
            }

            // Start the gRPC server.

            var builder = WebApplication.CreateBuilder();

            builder.Services.AddCodeFirstGrpc();

            builder.WebHost.UseUrls();
            builder.WebHost.UseKestrel(
                options =>
                {
                    options.ListenUnixSocket(socketPath, configure => configure.Protocols = HttpProtocols.Http2);
                });

            var app = builder.Build();

            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<GrpcDesktopService>());

            cts  = new CancellationTokenSource();
            task = app.StartAsync(cts.Token);

            // Ensure that everyone can read/write the unix domain socket.

            NeonExtendedHelper.SetUnixDomainSocketEveryonePermissions(socketPath, read: true, write: true);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            cts.Cancel();
            task.WaitWithoutAggregate();
            NeonHelper.DeleteFile(socketPath);

            // Flush any bactched logs and traces to the headend
            // in parallel on separate threads.

            Task.WaitAll(
                Task.Run(() => LogExporter.Shutdown(5000)),
                Task.Run(() => TraceExporter.Shutdown(5000)));
        }
    }
}
