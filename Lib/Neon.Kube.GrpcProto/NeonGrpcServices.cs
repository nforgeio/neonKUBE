//-----------------------------------------------------------------------------
// FILE:        NeonGrpcServices.cs
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
    /// Creates clients for NEONKUBE related gRPC services.
    /// </summary>
    public static class NeonGrpcServices
    {
        /// <summary>
        /// Creates a gRPC channel that can be used to access the NeonDESKTOP Service.
        /// </summary>
        /// <param name="socketPath">
        /// Optionally specifies an alternative path to the desktop services Unix domain socket
        /// for testing purposes.  This defaults to <see cref="KubeHelper.WinDesktopServiceSocketPath"/>
        /// where <b>NeonDESKTOP</b> and <b>NeonCLIENT</b> expect it to be.
        /// </param>
        /// <returns>A <see cref="IGrpcDesktopService"/> or <c>null</c> when the <b>neon-desktop-service</b> is not running.</returns>
        public static GrpcChannel? CreateDesktopServiceChannel(string? socketPath = null)
        {
            socketPath ??= KubeHelper.WinDesktopServiceSocketPath;

            if (!File.Exists(socketPath))
            {
                return null;
            }

            // $note(jefflill):
            //
            // We're not encrypting the neon-desktop-service channel since being a
            // Unix domain socket, it won't be reachable by other computers.
            //
            // We're also not securing this API with any kind of API key which means
            // that other users on the computer could potentially access the service
            // to manage Hyper-V.
            //
            // This is low risk, so we're not going to worry about it.

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

            try
            {
                return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions()
                {
                    HttpHandler = socketsHttpHandler
                });
            }
            catch
            {
                // neon-desktop-service must not be running.

                return null;
            }
        }
    }
}
