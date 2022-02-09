//-----------------------------------------------------------------------------
// FILE:	    DesktopGrpcServer.cs
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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

using ProtoBuf.Grpc.Server;

namespace Neon.Kube.DesktopServer
{
    /// <summary>
    /// Implements a gPRC server that implements Hyper-V and other operations that may
    /// require elevated permissions.  The idea is to deploy this within a Windows Service
    /// that runs as administrator so that this service can perform these operations on
    /// behalf of the neon-desktop or neon-cli applications that do not have these
    /// rights.
    /// </summary>
    public sealed class DesktopGrpcService : IDisposable
    {
        private bool                        isDisposed = false;
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
        /// <param name="socketPath">Path to the Unix domain socket path.</param>
        public DesktopGrpcService(string socketPath)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(socketPath), nameof(socketPath));

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
            task.Wait();
        }
    }
}
