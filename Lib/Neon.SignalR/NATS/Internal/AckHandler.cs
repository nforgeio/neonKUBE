//-----------------------------------------------------------------------------
// FILE:	    AckHandler.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Internal;

namespace Neon.SignalR
{
    internal class AckHandler : IDisposable
    {
        private readonly ConcurrentDictionary<int, AckInfo> _acks = new ConcurrentDictionary<int, AckInfo>();
        private readonly Timer _timer;
        private readonly long _ackThreshold = (long)TimeSpan.FromSeconds(30).TotalMilliseconds;
        private readonly TimeSpan _ackInterval = TimeSpan.FromSeconds(5);
        private readonly object _lock = new object();
        private bool _disposed;

        /// <summary>
        /// Constructor.
        /// </summary>
        public AckHandler()
        {
            _timer = new Timer(state => ((AckHandler)state!).CheckAcks(), state: this, dueTime: _ackInterval, period: _ackInterval);
        }

        /// <summary>
        /// Createes an Ack.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Task CreateAck(int id)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return Task.CompletedTask;
                }

                return _acks.GetOrAdd(id, _ => new AckInfo()).Tcs.Task;
            }
        }

        /// <summary>
        /// Trigger an Ack.
        /// </summary>
        /// <param name="id"></param>
        public void TriggerAck(int id)
        {
            if (_acks.TryRemove(id, out var ack))
            {
                ack.Tcs.TrySetResult();
            }
        }

        /// <summary>
        /// Trigger an ack.
        /// </summary>
        /// <param name="ack"></param>
        public void TriggerAck(byte[] ack)
        {
            var id = ReadAck(ack);
            TriggerAck(id);
        }

        /// <summary>
        /// Write an ack.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public byte[] WriteAck(int id)
        {
            return Encoding.UTF8.GetBytes(id.ToString());
        }

        /// <summary>
        /// Read an ack.
        /// </summary>
        /// <param name="ack"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public int ReadAck(byte[] ack)
        {
            if (int.TryParse(Encoding.UTF8.GetString(ack), out var ackId))
            {
                return ackId;
            }

            throw new ArgumentException("Could not read Ack.");
        }

        private void CheckAcks()
        {
            if (_disposed)
            {
                return;
            }

            var currentTick = Environment.TickCount64;

            foreach (var pair in _acks)
            {
                var elapsed = currentTick - pair.Value.CreatedTick;
                if (elapsed > _ackThreshold)
                {
                    if (_acks.TryRemove(pair.Key, out var ack))
                    {
                        ack.Tcs.TrySetCanceled();
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;

                _timer.Dispose();

                foreach (var pair in _acks)
                {
                    if (_acks.TryRemove(pair.Key, out var ack))
                    {
                        ack.Tcs.TrySetCanceled();
                    }
                }
            }
        }

        private class AckInfo
        {
            public TaskCompletionSource Tcs { get; private set; }
            public long CreatedTick { get; private set; }

            public AckInfo()
            {
                CreatedTick = Environment.TickCount64;
                Tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }
}