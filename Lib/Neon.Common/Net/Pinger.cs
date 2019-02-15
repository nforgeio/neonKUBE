//-----------------------------------------------------------------------------
// FILE:	    Pinger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

// $todo(jeff.lill):
//
// Investigate implementing a property (perhaps called [MaxParallel]) that caps the 
// number of ping requests that can be in flight.

namespace Neon.Net
{
    /// <summary>
    /// Implements a threadsafe subset of the .NET Framework <see cref="Ping"/> class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unfortunately, the .NET Framework <see cref="Ping"/> class is not threadsafe (even
    /// the async methods).  So, we need to ensure that only one ping request
    /// is performed on any given instance.
    /// </para>
    /// <para>
    /// My original idea was to simply create and dispose <see cref="Ping"/>] instances on 
    /// the fly for each request, but I changed my mind after thinking about
    /// the potential performance overhead as well as the potential for exhausting
    /// ephemeral socket ports.
    /// </para>
    /// <para>
    /// Instead, I'm going to maintain a queue of <see cref="Ping"/> instances that that can
    /// be reused for subsequent queries.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class Pinger : IDisposable
    {
        private object      syncLock    = new object();
        private Queue<Ping> unusedQueue = new Queue<Ping>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public Pinger()
        {
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~Pinger()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (syncLock)
                {
                    if (unusedQueue != null)
                    {
                        foreach (var ping in unusedQueue)
                        {
                            ping.Dispose();
                        }

                        unusedQueue.Clear();
                    }
                }

                unusedQueue = null;
                GC.SuppressFinalize(this);
            }

            unusedQueue = null;
        }

        /// <summary>
        /// Disposes any unused underlying <see cref="Ping"/> instances.
        /// </summary>
        public void Clear()
        {
            lock (syncLock)
            {
                if (unusedQueue != null)
                {
                    foreach (var ping in unusedQueue)
                    {
                        ping.Dispose();
                    }

                    unusedQueue.Clear();
                }
            }
        }

        /// <summary>
        /// Pings a IP address.
        /// </summary>
        /// <param name="address">The target address.</param>
        /// <param name="timeoutMilliseconds">Optional timeout in milliseconds (defaults to 2000).</param>
        /// <returns>A <see cref="PingReply"/>.</returns>
        public async Task<PingReply> SendPingAsync(IPAddress address, int timeoutMilliseconds = 2000)
        {
            Ping ping;

            lock (syncLock)
            {
                if (unusedQueue == null)
                {
                    throw new ObjectDisposedException(nameof(Pinger));
                }

                if (unusedQueue.Count > 0)
                {
                    ping = unusedQueue.Dequeue();
                }
                else
                {
                    ping = new Ping();
                }
            }

            try
            {
                return await ping.SendPingAsync(address, timeoutMilliseconds);
            }
            finally
            {
                lock (syncLock)
                {
                    if (unusedQueue != null)
                    {
                        unusedQueue.Enqueue(ping);
                    }
                    else
                    {
                        ping.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Pings a IP address passed as a <c>string</c>.
        /// </summary>
        /// <param name="address">The target address.</param>
        /// <param name="timeoutMilliseconds">Optional timeout in milliseconds (defaults to 2000).</param>
        /// <returns>A <see cref="PingReply"/>.</returns>
        public async Task<PingReply> SendPingAsync(string address, int timeoutMilliseconds = 2000)
        {
            return await SendPingAsync(IPAddress.Parse(address), timeoutMilliseconds);
        }
    }
}
