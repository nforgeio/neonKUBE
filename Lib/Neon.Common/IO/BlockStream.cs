//-----------------------------------------------------------------------------
// FILE:        BlockStream.cs
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

// $todo(jeff.lill): Implement all [*Async()] method overrides.

namespace Neon.IO
{
    /// <summary>
    /// Implements an in-memory stream based on a collection of <see cref="Block"/> 
    /// buffers rather than a single byte buffer.  This is more efficient than
    /// <see cref="MemoryStream"/> for large streams and also avoids allocations
    /// in the large object heap.
    /// </summary>
    /// <remarks>
    /// <note>
    /// Buffer array streams cannot be greater than or equal to 2GiB in length.
    /// </note>
    /// </remarks>
    public sealed class BlockStream : Stream
    {
        private const string TooLongError = "Block streams cannot be greater than or equal to 2GiB.";

        private BlockArray      blocks;     // Underlying blocks
        private int             pos;        // Current stream position
        private int             length;     // Current logical string length

        /// <summary>
        /// Constructs a zero length stream with default block size.
        /// </summary>
        public BlockStream()
        {
            this.pos    = 0;
            this.length = 0;
            this.blocks = new BlockArray();
        }

        /// <summary>
        /// Constructs a stream of the specified size using the default
        /// block size.
        /// </summary>
        /// <param name="size">The stream size in bytes.</param>
        public BlockStream(int size)
        {
            this.pos    = 0;
            this.length = 0;
            this.blocks = new BlockArray(size);
        }

        /// <summary>
        /// Constructs a stream of the specified size using the 
        /// specified block size.
        /// </summary>
        /// <param name="size">The stream size in bytes.</param>
        /// <param name="blockSize">The block size in bytes.</param>
        public BlockStream(int size, int blockSize)
        {
            this.pos    = 0;
            this.length = 0;
            this.blocks = new BlockArray(size, blockSize);
        }

        /// <summary>
        /// Constructs a stream of the specified size using the 
        /// specified block size and offset.
        /// </summary>
        /// <param name="size">The stream size in bytes.</param>
        /// <param name="blockSize">The block size in bytes.</param>
        /// <param name="blockOffset">Bytes to be reserved at the beginning of each new block.</param>
        /// <remarks>
        /// See <see cref="BlockArray"/> for more information on
        /// the value and use of the blockOffset prarmeter.
        /// </remarks>
        public BlockStream(int size, int blockSize, int blockOffset)
        {
            this.pos    = 0;
            this.length = 0;
            this.blocks = new BlockArray(size, blockSize, blockOffset);
        }

        /// <summary>
        /// Constructs a stream from the blocks passed.
        /// </summary>
        /// <param name="blocks">The blocks.</param>
        /// <remarks>
        /// The stream size will be set to the size of the blocks.
        /// </remarks>
        public BlockStream(BlockArray blocks)
        {
            this.pos    = 0;
            this.length = blocks.Size;
            this.blocks = blocks;
        }

        /// <summary>
        /// Constructs a stream from the blocks passed.
        /// </summary>
        /// <param name="blocks">The blocks.</param>
        /// <remarks>
        /// The stream size will be set to the size of the blocks.
        /// </remarks>
        public BlockStream(params Block[] blocks)
        {
            this.pos    = 0;
            this.blocks = new BlockArray(blocks);
            this.length = this.blocks.Size;
        }

        /// <summary>
        /// Constructs a stream from a byte array.
        /// </summary>
        /// <param name="buffer">The byte array.</param>
        public BlockStream(byte[] buffer)
        {
            this.pos    = 0;
            this.blocks = new BlockArray(buffer);
            this.length = this.blocks.Size;
        }

        /// <summary>
        /// Returns <c>true</c> if the stream supports read operations.
        /// </summary>
        public override bool CanRead
        {
            get { return true; }
        }

        /// <summary>
        /// Returns <c>true</c> if the stream supports write operations.
        /// </summary>
        public override bool CanWrite
        {
            get { return true; }
        }

        /// <summary>
        /// Returns <c>true</c> if the stream supports seek operations.
        /// </summary>
        public override bool CanSeek
        {
            get { return true; }
        }

        /// <summary>
        /// Returns the current size of the stream in bytes.
        /// </summary>
        public override long Length
        {
            get { return length; }
        }

        /// <summary>
        /// The current stream position.
        /// </summary>
        /// <remarks>
        /// <note>
        /// It is valid to set a stream position beyond the current
        /// end of the stream.  The stream will be extended to this position.
        /// The contents of the extended portion will be undefined.
        /// </note>
        /// </remarks>
        public override long Position
        {
            get { return pos; }

            set
            {
                if (value < 0 || value > int.MaxValue)
                {
                    throw new IOException(TooLongError);
                }

                pos = (int)value;

                if (pos > length)
                {
                    blocks.ExtendTo(pos);
                    length = pos;
                }
            }
        }

        /// <summary>
        /// Sets the length of the stream.
        /// </summary>
        /// <param name="value">The new length in bytes.</param>
        /// <remarks>
        /// The stream will be truncated if the new length is less than
        /// the current length.  The stream will be extended if the new
        /// length is greater than the current length.  In this case,
        /// the contents of the extended portion will be undefined.
        /// </remarks>
        public override void SetLength(long value)
        {
            int cb;

            if (value < 0 || value > int.MaxValue)
            {
                throw new IOException(TooLongError);
            }

            cb = (int)value;

            if (cb < length)
            {
                length = cb;
                blocks.TruncateTo(length);

                if (pos > length)
                {
                    pos = length;
                }
            }
            else if (cb > length)
            {
                length = cb;
                blocks.ExtendTo(length);
            }
        }

        /// <summary>
        /// Sets the length of the stream.
        /// </summary>
        /// <param name="value">The new length in bytes.</param>
        /// <param name="modifyBlocks"><c>true</c> to modify the underlying block array if necessary.</param>
        /// <remarks>
        /// The modifyBlocks parameter specifies whether the underlying
        /// block array will be truncated or extended to the length
        /// specified.
        /// </remarks>
        public void SetLength(long value, bool modifyBlocks)
        {
            int cb;

            if (value < 0 || value > int.MaxValue)
            {
                throw new IOException(TooLongError);
            }

            if (modifyBlocks)
            {
                SetLength(value);
                return;
            }

            cb = (int)value;

            if (cb > blocks.Size)
            {
                throw new IOException("Underlying block array is not large enough.");
            }

            length = cb;

            if (pos > length)
            {
                pos = length;
            }
        }

        /// <summary>
        /// Reads a byte from the current stream position, advancing
        /// the position by 1.
        /// </summary>
        /// <returns>The byte read or <b>-1</b> if the end of the stream has been reached.</returns>
        public override int ReadByte()
        {
            if (pos >= length)
            {
                return -1;
            }

            return blocks[pos++];
        }

        /// <summary>
        /// Reads bytes from the current stream position, advancing the
        /// position past the data read.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">Offset where the read data is to be copied.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <returns>The number of bytes actually read.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int cbRemain;
            int cbRead;

            cbRemain = length - pos;
            cbRead   = cbRemain >= count ? count : cbRemain;

            if (cbRead == 0)
            {
                return 0;
            }

            blocks.CopyTo(pos, buffer, offset, cbRead);
            pos += cbRead;

            return cbRead;
        }

        /// <summary>
        /// Asynchronously reads from the stream.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">Offset where the read data is to be copied.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of bytes actually read.</returns>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.FromResult(Read(buffer, offset, count));
        }

        /// <summary>
        /// Writes a byte to the current stream position, advancing the
        /// position by 1.
        /// </summary>
        /// <param name="value">The byte to write.</param>
        public override void WriteByte(byte value)
        {
            try
            {
                unchecked
                {
                    if (pos + 1 < 0)  // Checking for wraparound
                    {
                        throw new IOException(TooLongError);
                    }
                }

                blocks.ExtendTo(pos + 1);
                blocks[pos++] = value;

                if (pos > length)
                {
                    length++;
                }
            }
            catch
            {
                throw new IOException();
            }
        }

        /// <summary>
        /// Writes bytes to the stream at the current position, advancing
        /// the position past the data written.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="offset">Offset of the first byte to write.</param>
        /// <param name="count">Number of bytes to read.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            try
            {
                unchecked
                {
                    if (pos + count < 0)  // Checking for wraparound
                    {
                        throw new IOException(TooLongError);
                    }
                }

                blocks.ExtendTo(pos + count);
                blocks.CopyFrom(buffer, offset, pos, count);

                pos += count;

                if (pos > length)
                {
                    length = pos;
                }
            }
            catch
            {
                throw new IOException();
            }
        }

        /// <summary>
        /// Asynchronously writes bytes to the stream at the current position, advancing
        /// the position past the data written.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="offset">Offset of the first byte to write.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.Delay(0);
        }

        /// <summary>
        /// Flushes any stream buffers.
        /// </summary>
        /// <remarks>
        /// This is a NOP for this implementation.
        /// </remarks>
        public override void Flush()
        {
        }

        /// <summary>
        /// Moves the current stream position relative to the specified origin.
        /// </summary>
        /// <param name="offset">The positional offset.</param>
        /// <param name="origin">Specifies the seek origin.</param>
        /// <returns>The stream position after the seek.</returns>
        /// <remarks>
        /// It is valid to seek past the current stream length.  In this
        /// case, the stream will be extended with the contents of the
        /// extended portion being undefined.
        /// </remarks>
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:

                    break;

                case SeekOrigin.Current:

                    offset += pos;
                    break;

                case SeekOrigin.End:

                    offset += length;
                    break;
            }

            if (offset < 0 || offset > int.MaxValue)
            {
                throw new IOException(TooLongError);
            }

            pos = (int)offset;

            if (pos > length)
            {
                blocks.ExtendTo(pos);
                length = pos;
            }

            return pos;
        }

        /// <summary>
        /// Appends a block to the end of the underlying BlockArray.
        /// </summary>
        /// <param name="block">The block to append.</param>
        /// <remarks>
        /// <para>
        /// The underyling block array's SetExactSize() method will be
        /// called before appending the block.  The stream position will
        /// be set to the end of the stream before the method returns.
        /// </para>
        /// <para>
        /// This method is a performance improvement over writing the
        /// a buffer to the stream via one of the write methods.
        /// </para>
        /// </remarks>
        public void Append(Block block)
        {
            blocks.SetExactSize(length);
            blocks.Append(block);

            length += block.Length;
            pos     = length;
        }

        /// <summary>
        /// Appends a block array to the end of the underlying BlockArray.
        /// </summary>
        /// <param name="blocks">The array to append.</param>
        /// <remarks>
        /// The underyling block array's SetExactSize() method will be
        /// called before appending the block.  The stream position will
        /// be set to the end of the stream before the method returns.
        /// 
        /// This method is a performance improvement over writing the
        /// a buffer to the stream via one of the write methods.
        /// </remarks>
        public void Append(BlockArray blocks)
        {
            this.blocks.SetExactSize(length);

            for (int i = 0; i < blocks.Count; i++)
            {
                this.blocks.Append(blocks.GetBlock(i));
            }

            length += blocks.Size;
            pos     = length;
        }

        /// <summary>
        /// Returns the underlying block array without modification.
        /// </summary>
        public BlockArray RawBlockArray
        {
            get { return blocks; }
        }

        /// <summary>
        /// Returns the underlying buffer array.
        /// </summary>
        /// <param name="truncate">
        /// <c>true</c> if the method will truncate the underlying BlockArray
        /// to the actual length of the stream before returning the array.
        /// </param>
        public BlockArray ToBlocks(bool truncate)
        {
            blocks.SetExactSize(length);
            return blocks;
        }

        /// <summary>
        /// Assembles a contiguous a byte array from the underlying
        /// buffer array.
        /// </summary>
        /// <returns>The assembled byte array.</returns>
        public byte[] ToArray()
        {
            byte[] arr;

            arr = new byte[length];
            blocks.CopyTo(0, arr, 0, length);

            return arr;
        }

        /// <summary>
        /// Returns requested bytes from the underlying block array as
        /// as a new block array.
        /// </summary>
        /// <param name="cb">The nunber of bytes to read.</param>
        /// <returns>
        /// A new block array referencing the requested bytes in the
        /// same underlying buffers as managed by then stream.
        /// </returns>
        /// <remarks>
        /// This provides a high performance way for code that knows
        /// how to handle block arrays to extract a portion of a stream.
        /// 
        /// The array returned will be truncated to the length of the
        /// underlying stream.  The stream position will be advanced
        /// past the requested bytes.
        /// </remarks>
        public BlockArray ReadBlocks(int cb)
        {
            BlockArray extracted;

            if (pos + cb > length)
            {
                cb = length - pos;
            }

            extracted = blocks.Extract(pos, cb);
            pos      += cb;

            return extracted;
        }
    }
}
