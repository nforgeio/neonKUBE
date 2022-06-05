//-----------------------------------------------------------------------------
// FILE:	    WatchStream.cs
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
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Rest.Serialization;

using Neon.Common;
using Neon.Tasks;

using k8s;

namespace Neon.Kube
{
    /// <summary>
    /// This handles streams from the Kubernetes API server, used for watching resources.
    /// </summary>
    /// <typeparam name="T">The type of resource being watched.</typeparam>
    public class WatchStream<T> : IAsyncEnumerable<WatchEvent<T>>, IDisposable where T : new()
    {
        private readonly StreamReader _reader;
        private readonly IDisposable _response;
        private readonly List<Memory<char>> _previousData = new List<Memory<char>>();
        private Memory<char> _currentBuffer = Memory<char>.Empty;
        private Memory<char> _currentData = Memory<char>.Empty;
        private static JsonSerializerOptions jsonSerializerOptions;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="response"></param>
        public WatchStream(Stream stream, IDisposable response)
        {
            _reader = new StreamReader(stream, leaveOpen: false);
            _response = response;

            jsonSerializerOptions = new JsonSerializerOptions();

            jsonSerializerOptions.Converters.Add(new V1ResourceConverter());
            jsonSerializerOptions.Converters.Add(new JsonStringEnumMemberConverter());
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                _reader.Dispose();
            }
            finally
            {
                _response.Dispose();
            }
        }

        /// <inheritdoc/>
        public IAsyncEnumerator<WatchEvent<T>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new AsyncEnumerator(this, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<(WatchEventType eventType, T resource, bool connected)> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            if (TryGetLine(out var line))
            {
                var data = JsonSerializer.Deserialize<Watcher<T>.WatchEvent>(line, jsonSerializerOptions);
                return (data.Type, data.Object, true);
            }

            while (true)
            {
                if (_currentBuffer.IsEmpty)
                {
                    _currentBuffer = new Memory<char>(new char[4096]);
                }
                if (!_currentData.IsEmpty)
                {
                    _previousData.Add(_currentData);
                    _currentData = Memory<char>.Empty;
                }

                var length = await _reader.ReadAsync(_currentBuffer, cancellationToken);
                if (length == 0)
                {
                    // stream closed
                    return (default, default, false);
                }

                _currentData = _currentBuffer.Slice(0, length);
                _currentBuffer = _currentBuffer.Slice(length);

                if (TryGetLine(out line))
                {
                    var data = JsonSerializer.Deserialize<Watcher<T>.WatchEvent>(line, jsonSerializerOptions);
                    return (data.Type, data.Object, true);
                }
            }
        }

        private bool TryGetLine(out string line)
        {
            var delimiterIndex = _currentData.Span.IndexOf('\n');
            if (delimiterIndex == -1)
            {
                line = null;
                return false;
            }

            if (_previousData.Count != 0)
            {
                var sb = new StringBuilder();
                foreach (var buffer in _previousData)
                {
                    sb.Append(buffer);
                }
                _previousData.Clear();

                sb.Append(_currentData.Slice(0, delimiterIndex));
                _currentData = _currentData.Slice(delimiterIndex + 1);
                line = sb.ToString();
                return true;
            }

            line = new string(_currentData.Slice(0, delimiterIndex).Span);
            _currentData = _currentData.Slice(delimiterIndex + 1);
            return true;
        }

        internal class AsyncEnumerator : IAsyncEnumerator<WatchEvent<T>>
        {
            private WatchStream<T> _self;
            private CancellationToken _cancellationToken;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="self"></param>
            /// <param name="cancellationToken"></param>
            public AsyncEnumerator(WatchStream<T> self, CancellationToken cancellationToken)
            {
                _self = self;
                _cancellationToken = cancellationToken;
            }

            public WatchEvent<T> Current { get; set; }

            /// <inheritdoc/>
            public ValueTask DisposeAsync()
            {
                _self.Dispose();
                return ValueTask.CompletedTask;
            }

            /// <inheritdoc/>
            public async ValueTask<bool> MoveNextAsync()
            {
                await SyncContext.Clear;
                var (eventType, resource, connected) = await _self.ReadNextAsync(_cancellationToken);
                Current = new WatchEvent<T>(eventType, resource);
                return connected;
            }
        }
    }
}
