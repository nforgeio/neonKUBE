//-----------------------------------------------------------------------------
// FILE:	    StreamManager.cs
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
    internal class StreamManager : IStreamManager
    {
        private readonly ILoggerFactory         loggerFactory;
        private readonly ILogger<StreamManager> logger;
        public StreamManager(
            ILoggerFactory loggerFactory = null)
        {
            this.loggerFactory = loggerFactory;
            this.logger        = loggerFactory?.CreateLogger<StreamManager>();
        }

        public void Start(
            TcpClient             localConnection, 
            Func<Task<WebSocket>> remoteConnectionFactory, 
            int                   remotePort, 
            CancellationToken     cancellationToken = default)
        {
            var stream = new StreamInstance(localConnection, remoteConnectionFactory, remotePort, loggerFactory);
            stream.Run(cancellationToken);
        }
    }
}
