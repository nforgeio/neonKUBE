//-----------------------------------------------------------------------------
// FILE:        Block.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Common
{
    /// <summary>
    /// Used to reference a block in a <see cref="BlockArray"/> or <see cref="Neon.IO.BlockStream"/>.
    /// </summary>
    public sealed class Block
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Explict cast from a <see cref="Byte" /> <see cref="ArraySegment{T}" /> to a <see cref="Block" />.
        /// </summary>
        /// <param name="segment">The array segment to be converted.</param>
        /// <returns>The equivalent <see cref="Block" />.</returns>
        public static explicit operator Block(ArraySegment<byte> segment)
        {
            return new Block(segment.Array, segment.Offset, segment.Count);
        }

        /// <summary>
        /// Explict cast from a <see cref="Block" /> to a <see cref="Byte" /> <see cref="ArraySegment{T}" />
        /// </summary>
        /// <param name="block">The <see cref="Block" /> to be converted.</param>
        /// <returns>The equivalent <see cref="ArraySegment{T}" />.</returns>
        public static explicit operator ArraySegment<byte>(Block block)
        {
            return new ArraySegment<byte>(block.buffer, block.offset, block.length);
        }

        /// <summary>
        /// Assembles the bytes referenced by the blocks into a contiguous buffer.
        /// </summary>
        /// <param name="blocks">The blocks.</param>
        /// <returns>A contiguous buffer.</returns>
        public static byte[] Assemble(params Block[] blocks)
        {
            byte[]  assembled;
            int     cb;
            int     pos;

            cb = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                cb += blocks[i].Length;
            }

            assembled = new byte[cb];

            pos = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                var block = blocks[i];

                Array.Copy(block.Buffer, block.Offset, assembled, pos, block.Length);
                pos += block.Length;
            }

            return assembled;
        }

        //---------------------------------------------------------------------
        // Instance members

        private byte[]      buffer;
        private int         offset;
        private int         length;

        /// <summary>
        /// Constructs a block.
        /// </summary>
        /// <param name="buffer">The byte buffer.</param>
        public Block(byte[] buffer)
        {
            this.buffer = buffer;
            this.offset = 0;
            this.length = buffer.Length;
        }

        /// <summary>
        /// Allocates a block to a newly allocated buffer of the
        /// specified size.
        /// </summary>
        /// <param name="size">The new block size in bytes.</param>
        public Block(int size)
        {
            this.buffer = new byte[size];
            this.offset = 0;
            this.length = size;
        }

        /// <summary>
        /// Constructs a block.
        /// </summary>
        /// <param name="buffer">The byte buffer.</param>
        /// <param name="offset">Offset of the first referenced byte.</param>
        /// <param name="length">Byte length of the reference.</param>
        public Block(byte[] buffer, int offset, int length)
        {
            if (offset < 0 || length < 0 || offset + length > buffer.Length)
            {
                throw new IndexOutOfRangeException();
            }

            this.buffer = buffer;
            this.offset = offset;
            this.length = length;
        }

        /// <summary>
        /// The referenced buffer.
        /// </summary>
        public byte[] Buffer
        {
            get { return buffer; }
            set { buffer = value; }
        }

        /// <summary>
        /// The offset of the starting position of the referenced bytes in the buffer.
        /// </summary>
        public int Offset
        {
            get { return offset; }
            set { offset = value; }
        }

        /// <summary>
        /// The number of referenced bytes.
        /// </summary>
        public int Length
        {
            get { return length; }
            set { length = value; }
        }

        /// <summary>
        /// Modifies the range of bytes referenced by the instance.
        /// </summary>
        /// <param name="offset">Index of the first referenced byte.</param>
        /// <param name="length">Number of bytes referenced.</param>
        public void SetRange(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset + length > buffer.Length)
            {
                throw new IndexOutOfRangeException();
            }

            this.offset = offset;
            this.length = length;
        }

        /// <summary>
        /// Accesses the byte at the specified index in the block.
        /// </summary>
        public byte this[int index]
        {
            get
            {
                if (index < 0 || index >= length)
                {
                    throw new IndexOutOfRangeException();
                }

                return buffer[offset + index];
            }

            set
            {
                if (index < 0 || index >= length)
                {
                    throw new IndexOutOfRangeException();
                }

                buffer[offset + index] = value;
            }
        }

        /// <summary>
        /// Copies bytes from the logical offset in the block
        /// to the target byte array.
        /// </summary>
        /// <param name="sourceOffset">Logical offset of the first byte to copy.</param>
        /// <param name="target">The output byte array.</param>
        /// <param name="targetOffset">Target offset where the first byte is to be written.</param>
        /// <param name="length"></param>
        public void CopyTo(int sourceOffset, byte[] target, int targetOffset, int length)
        {
            Array.Copy(buffer, offset + sourceOffset, target, targetOffset, length);
        }

        /// <summary>
        /// Copies bytes from the byte array passed into the block.
        /// </summary>
        /// <param name="source">The source byte array.</param>
        /// <param name="sourceOffset">Offset of the first byte to copy from the source array.</param>
        /// <param name="targetOffset">Logical offset of the first target byte in the block.</param>
        /// <param name="length">Number of bytes to copy.</param>
        public void CopyFrom(byte[] source, int sourceOffset, int targetOffset, int length)
        {
            Array.Copy(source, sourceOffset, buffer, offset + targetOffset, length);
        }
    }
}
