//-----------------------------------------------------------------------------
// FILE:	    MemoryStreamPool.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Temporal;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Manages a pool of <see cref="MemoryStream"/> instances used for serializing
    /// and deserializing proxy messages.
    /// </summary>
    internal static class MemoryStreamPool
    {
        // Implementation Note:
        // --------------------
        // We're going to use a stack to store the pooled streams with the theory
        // that we'll tend to reuse the same streams more often with the hope that
        // the memory these reference will may tend to be already loaded into the
        // CPU caches.

        private static Stack<MemoryStream> poolStack = new Stack<MemoryStream>();

        /// <summary>
        /// Allocates a stream from the pool.
        /// </summary>
        /// <returns>The allocated stream.</returns>
        public static MemoryStream Alloc()
        {
            MemoryStream stream;

            lock (poolStack)
            {
                if (poolStack.Count == 0)
                {
                    stream = new MemoryStream();
                }
                else
                {
                    stream = poolStack.Pop();
                }
            }

            return stream;
        }

        /// <summary>
        /// Frees the stream by adding it back to the pool.
        /// </summary>
        /// <param name="stream">The stream being freed.</param>
        public static void Free(MemoryStream stream)
        {
            Covenant.Requires<ArgumentNullException>(stream != null, nameof(stream));

            // We're going to limit the capacity of cached streams to 1MiB to
            // prevent the accumulation of cached streams with very large buffers.

            stream.SetLength(0);

            if (stream.Capacity > 1 * 1024 * 1024)
            {
                stream.Capacity = 1 * 1024 * 1024;
            }

            lock (poolStack)
            {
                poolStack.Push(stream);
            }
        }
    }
}
