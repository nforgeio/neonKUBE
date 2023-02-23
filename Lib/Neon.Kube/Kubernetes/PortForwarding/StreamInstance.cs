//-----------------------------------------------------------------------------
// FILE:	    StreamInstance.cs
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
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using k8s;
using System.Net.Sockets;
using System.Diagnostics.Contracts;
using Neon.Diagnostics;

namespace Neon.Kube
{
    internal class StreamInstance
    {
        private TcpClient             localConnection;
        private Func<Task<WebSocket>> remoteConnectionFactory;
        private ILogger               logger;
        private int                   remotePort;

        private SemaphoreSlim syncObject            = new SemaphoreSlim(1);
        private int           remoteStartRetryCount = 0;
        private NetworkStream localStream;
        private WebSocket     remote;
        private StreamDemuxer remoteStreams;
        private bool          stop;
        private bool          receiveStarted;

        private const int BUFFER_SIZE = 81920;

        public StreamInstance(
            TcpClient             localConnection,
            Func<Task<WebSocket>> remoteConnectionFactory,
            int                   remotePort,
            ILoggerFactory        loggerFactory)
        {
            Covenant.Requires(localConnection != null, nameof(localConnection));
            Covenant.Requires(remoteConnectionFactory != null, nameof(remoteConnectionFactory));

            this.logger                  = loggerFactory?.CreateLogger<StreamInstance>();
            this.localConnection         = localConnection;
            this.localStream             = localConnection.GetStream();
            this.remoteConnectionFactory = remoteConnectionFactory;
            this.remotePort              = remotePort;
        }

        public Task Run(CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() => Stop()))
            {
                return RunSendLoop();
            }
        }

        private async Task RunSendLoop()
        {
            byte[] buffer = new byte[BUFFER_SIZE];
            while (true)
            {
                int cRead = localStream != null ? await localStream.ReadAsync(buffer, 0, buffer.Length) : 0;
                if (cRead == 0)
                {
                    this.Stop();
                    break;
                }
                else
                {
                    bool sendSuccess = false;
                    try
                    {
                        await EnsureRemoteStartAsync();
                        var s = this.GetRemoteStream(remoteStreams, forWrite: true);
                        await s.WriteAsync(buffer, 0, cRead);
                        sendSuccess = true;
                    }
                    catch (Exception ex)
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

        private async Task RunReceiveLoop()
        {
            byte[] buffer = new byte[BUFFER_SIZE];
            var s = this.GetRemoteStream(remoteStreams, forRead: true);
            receiveStarted = true;
            long bytesReceived = 0;
            while (true)
            {
                try
                {
                    int cRead = await s.ReadAsync(buffer, 0, buffer.Length);
                    if (cRead == 0)
                    {
                        this.Stop();
                        break;
                    }
                    else
                    {
                        if (localStream != null)
                        {
                            bytesReceived += cRead;
                            if (bytesReceived == 2 && cRead == 2 && (remotePort >> 8) == buffer[1] && (remotePort % 256) == buffer[0])
                            {
                                // This is a bug in the K8s client library around port-forwarding. Some times at the first receiving, K8s will send
                                // back the port number in the first 2 bytes. K8s client library should filter out these 2 bytes but it didn't.
                                // Work around this issue here before the K8s client library fix. https://github.com/kubernetes-client/csharp/issues/229
                                continue;
                            }
                            await localStream.WriteAsync(buffer, 0, cRead);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogErrorEx(() => $"Write to local failed with {ex.Message}");
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
            await syncObject.WaitAsync();
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
                syncObject.Release();
            }
            if (remoteStreams != null)
            {
                remoteStreams.Start();
                _ = Task.Run(() => this.RunReceiveLoop());
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
            syncObject.Wait();
            try
            {
                localConnection?.Close();
                localConnection = null;
                localStream?.Close();
                localStream = null;
            }
            finally
            {
                syncObject.Release();
            }
        }

        private void StopRemote()
        {
            syncObject.Wait();
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
                syncObject.Release();
            }
        }
    }
}
