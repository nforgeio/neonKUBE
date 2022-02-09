//-----------------------------------------------------------------------------
// FILE:	    NeonGrpcServices.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.Kube.GrpcProto.Desktop;
using Neon.Net;

using Grpc.Net.Client;
using ProtoBuf.Grpc;

namespace Neon.Kube.GrpcProto
{
    /// <summary>
    /// Creates clients for neonKUBE related gRPC services.
    /// </summary>
    public static class NeonGrpcServices
    {
        /// <summary>
        /// Creates a gRPC channel that can be used to access the Neon Desktop Service.
        /// </summary>
        /// <returns>A <see cref="IGrpcDesktopService"/>.</returns>
        public static GrpcChannel CreateDesktopServiceChannel()
        {
            var socketPath = KubeHelper.WinDesktopServiceSocketPath;

            if (!File.Exists(socketPath))
            {
                throw new FileNotFoundException($"The Neon Desktop Service is not running: no socket file at: {socketPath}");
            }

            // We need to enable support for gRPC on plain HTTP because we have not
            // configured the desktop service with a certificate:
            //
            //      https://github.com/nforgeio/neonCLOUD/issues/254

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var endPoint           = new UnixDomainSocketEndPoint(socketPath);
            var socketsHttpHandler = new SocketsHttpHandler()
            {
                ConnectCallback = 
                    async (context, cancellationToken) =>
                    {
                        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

                        try
                        {
                            await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
                            return new NetworkStream(socket, true);
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    }
            };

            return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions()
            {
                HttpHandler = socketsHttpHandler
            });
        }
    }
}
