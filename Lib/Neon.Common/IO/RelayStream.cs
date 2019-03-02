//-----------------------------------------------------------------------------
// FILE:        RelayStream.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

// $todo(jeff.lill): Implement all [*Async()] method overrides.

namespace Neon.IO
{
    /// <summary>
    /// Implements a stream that passes operations to another stream.  This 
    /// is mainly useful for controlling whether <see cref="Stream.Dispose()"/>
    /// and <see cref="Dispose(bool)"/> actually disposes the underlying
    /// stream or not when the stream is referenced by another class that 
    /// always disposes the stream.
    /// </summary>
    public class RelayStream : Stream
    {
        private Stream  stream;
        private bool    leaveOpen;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="stream">The underlying stream being associated.</param>
        /// <param name="leaveOpen">Optionally leave the underlying stream open when this instance is disposed.</param>
        public RelayStream(Stream stream, bool leaveOpen = false)
        {
            Covenant.Requires<ArgumentNullException>(stream != null);

            this.stream    = stream;
            this.leaveOpen = leaveOpen;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!leaveOpen)
            {
                base.Dispose(disposing);
            }
        }

        /// <inheritdoc/>
        public override bool CanRead => stream.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => stream.CanSeek;

        /// <inheritdoc/>
        public override bool CanWrite => stream.CanWrite;

        /// <inheritdoc/>
        public override long Length => stream.Length;

        /// <inheritdoc/>
        public override long Position
        {
            get { return stream.Position; }
            set { stream.Position = value; }
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            stream.Flush();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return stream.Read(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
        }
    }
}
