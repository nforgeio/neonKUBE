//-----------------------------------------------------------------------------
// FILE:	    PortForwardStream.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Buffers.Binary;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using k8s;

using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Net;

namespace Neon.Kube.PortForward
{
    /// <summary>
    /// Implements a a stream that forwards traffic from a port on the client
    /// workstation to a remote port on a pod running in the cluster.
    /// </summary>
    internal class PortForwardStream
    {
        private const int BUFFER_SIZE = 8192;

        private SemaphoreSlim           syncLock = new SemaphoreSlim(1);
        private TcpClient               localConnection;
        private RemoteConnectionFactory remoteConnectionFactory;
        private ILogger                 logger;
        private int                     remotePort;
        private int                     remoteStartRetryCount = 0;
        private NetworkStream           localStream;
        private WebSocket               remote;
        private StreamDemuxer           remoteStreams;
        private bool                    stop;
        private bool                    receiveStarted;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="localConnection">Specfies the local side of the connection.</param>
        /// <param name="remoteConnectionFactory">Specifies the factory that returns the websocket for the remote side of the connection.</param>
        /// <param name="remotePort">Specfies the remote port.</param>
        /// <param name="loggerFactory">Optionally specfies a logger factory.</param>
        public PortForwardStream(
            TcpClient               localConnection,
            RemoteConnectionFactory remoteConnectionFactory,
            int                     remotePort,
            ILoggerFactory          loggerFactory = null)
        {
            Covenant.Requires(localConnection != null, nameof(localConnection));
            Covenant.Requires(remoteConnectionFactory != null, nameof(remoteConnectionFactory));
            Covenant.Requires<ArgumentNullException>(NetHelper.IsValidPort(remotePort), nameof(remotePort), $"Invalid TCP port: {remotePort}");

            this.logger                  = loggerFactory?.CreateLogger<PortForwardStream>();
            this.localConnection         = localConnection;
            this.localStream             = localConnection.GetStream();
            this.remoteConnectionFactory = remoteConnectionFactory;
            this.remotePort              = remotePort;
        }

        /// <summary>
        /// Forwards traffic between the local and remote ports until the operation is cancelled.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public Task RunAsync(CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() => Stop()))
            {
                return SendLoop();
            }
        }

        /// <summary>
        /// Handles traffic forwarded from the local workstation to the remote pod.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task SendLoop()
        {
            var buffer = new byte[BUFFER_SIZE];

            while (true)
            {
                var cbRead = localStream != null ? await localStream.ReadAsync(buffer, 0, buffer.Length) : 0;

                if (cbRead == 0)
                {
                    this.Stop();
                    break;
                }
                else
                {
                    var sendSuccess = false;

                    try
                    {
                        await EnsureRemoteStartAsync();

                        var stream = this.GetRemoteStream(remoteStreams, forWrite: true);

                        await stream.WriteAsync(buffer, 0, cbRead);

                        sendSuccess = true;
                    }
                    catch
                    {
                        this.StopRemote();
                    }
                    if (!sendSuccess)
                    {
                        this.Stop();
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Handles traffic forwarded from the remote pod to the workstation.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ReceiveLoop()
        {
            var buffer        = new byte[BUFFER_SIZE];
            var stream        = this.GetRemoteStream(remoteStreams, forRead: true);
            var bytesReceived = 0;

            receiveStarted = true;

            while (true)
            {
                try
                {
                    var cbRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (cbRead == 0)
                    {
                        this.Stop();
                        break;
                    }
                    else
                    {
                        if (localStream != null)
                        {
                            bytesReceived += cbRead;

                            if (bytesReceived == 2 && cbRead == 2 && (remotePort >> 8) == buffer[1] && (remotePort % 256) == buffer[0])
                            {
                                // This is a bug in the K8s client library around port-forwarding. Some times at the first receiving, K8s will send
                                // back the port number in the first 2 bytes. K8s client library should filter out these 2 bytes but it didn't.
                                // Work around this issue here before the K8s client library fix:
                                //
                                //      https://github.com/kubernetes-client/csharp/issues/229

                                continue;
                            }

                            await localStream.WriteAsync(buffer, 0, cbRead);
                        }
                    }
                }
                catch (Exception e)
                {
                    logger?.LogErrorEx(() => $"Write to local failed with: {e.Message}");
                    this.Stop();
                    break;
                }
            }
        }

        private async Task EnsureRemoteStartAsync()
        {
            if (remote != null)
            {
                return;
            }
            StreamDemuxer remoteStreams = null;
            await syncLock.WaitAsync();
            try
            {
                if (remote == null)
                {
                    remote = await remoteConnectionFactory();
                    this.remoteStreams = new StreamDemuxer(remote);
                    this.remoteStreams.ConnectionClosed += this.RemoteConnectionClosed;
                    remoteStreams = this.remoteStreams;
                }
            }
            finally
            {
                syncLock.Release();
            }
            if (remoteStreams != null)
            {
                remoteStreams.Start();
                _ = Task.Run(() => this.ReceiveLoop());
            }
        }

        private void RemoteConnectionClosed(object sender, EventArgs e)
        {
            logger?.LogErrorEx(() => "RemoteConnection closed.");
            if (!stop)
            {
                if (!receiveStarted && remoteStartRetryCount == 0)
                {
                    remoteStartRetryCount++;
                    this.StopRemote();

                    // There is a bug still under investigation: some times when a port-forwarding connection was made via K8s client SDK,
                    // it will close the connection immediately at the web socket. This code is specific to detect this code and try to
                    // re-create the underlying WebSocket.
                    remote = remoteConnectionFactory().Result;
                    remoteStreams = new StreamDemuxer(remote);
                    remoteStreams.ConnectionClosed += this.RemoteConnectionClosed;
                    remoteStreams.Start();
                }
                else
                {
                    this.Stop();
                }
            }
        }

        private Stream GetRemoteStream(StreamDemuxer remoteStreams, bool forRead = false, bool forWrite = false)
        {
            // We need this lock to get around a race condition in the SDK.
            // We won't need this when we upgrade to use the latest verison of the SDK.
            lock (remoteStreams)
            {
                return remoteStreams.GetStream(forRead ? (byte?)0 : null, forWrite ? (byte?)0 : null);
            }
        }

        private void Stop()
        {
            stop = true;
            StopRemote();
            StopLocal();
        }

        private void StopLocal()
        {
            syncLock.Wait();
            try
            {
                localConnection?.Close();
                localConnection = null;
                localStream?.Close();
                localStream = null;
            }
            finally
            {
                syncLock.Release();
            }
        }

        private void StopRemote()
        {
            syncLock.Wait();
            try
            {
                var remoteStreams = this.remoteStreams;
                if (remoteStreams != null)
                {
                    // There is a potential deadlock from K8s client SDK. If remoteStreams.Dispose is invoked directly from
                    // its ConnectionStopped event handler at during connection start, it will deadlock. Move remoteStreams.Dispose
                    // off to a different thread to work around.
                    Task.Run(() => remoteStreams.Dispose());
                }
                this.remoteStreams = null;
                remote?.Dispose();
                remote = null;
            }
            finally
            {
                syncLock.Release();
            }
        }
    }
}
