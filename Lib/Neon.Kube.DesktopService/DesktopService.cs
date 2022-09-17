//-----------------------------------------------------------------------------
// FILE:	    DesktopService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

using ProtoBuf.Grpc.Server;
using System.IO;
using Neon.Kube.GrpcProto;

namespace Neon.Kube.DesktopService
{
    /// <summary>
    /// Implements a gPRC server that implements Hyper-V and other operations that may
    /// require elevated permissions.  The idea is to deploy this within a Windows Service
    /// that runs as administrator so that this service can perform these operations on
    /// behalf of the neon-desktop or neon-cli applications that do not have these
    /// rights.
    /// </summary>
    public sealed class DesktopService : IDisposable
    {
        private bool                        isDisposed = false;
        private string                      socketPath;
        private Task                        task;
        private CancellationTokenSource     cts;

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
        }
    }
}
