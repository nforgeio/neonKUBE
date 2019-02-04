//-----------------------------------------------------------------------------
// FILE:        BlockArray.cs
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
using System.IO;

// $todo(jeff.lill): 
//
// The Block class needs to be redefined as a struct.  This will require
// some significant changes to the BlockArray implementation.

namespace Neon.Common
{
    /// <summary>
    /// Implements an array of <see cref="Block"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The purpose of <see cref="BlockArray"/> is to avoid performance robbing 
    /// buffer reallocations and copies and large obvject heap applications as can happen 
    /// with extensive use of the <see cref="MemoryStream"/> class.  Rather than doing I/O
    /// to a single large buffer, the <see cref="BlockArray"/> provides the underlying
    /// functionality for spreading I/O across multiple blocks.  This avoids any need
    /// to reallocate and copy a large buffer as the stream grows an also
    /// tends to allocate consistently sized memory blocks, making life
    /// easier for the memory allocator.
    /// </para>
    /// <para>
    /// This class is pretty flexible.  Blocks can be explicitly added and
    /// removed from the class or the <see cref="BlockArray.ExtendTo" />, 
    /// <see cref="TruncateTo" />, or <see cref="SetExactSize" />
    /// methods can be used have the class handle block management.
    /// </para>
    /// <para>
    /// The <see cref="BlockSize" /> and <see cref="BlockOffset" />
    /// properties are used by the internal block management methods when allocating 
    /// new blocks.  <see cref="BlockSize" /> defaults to 512 and specifies 
    /// the size of new blocks.  <see cref="BlockOffset" />
    /// defaults to 0.  New blocks will have their <see cref="Block.Offset" /> field set to
    /// <see cref="BlockOffset" />.
    /// </para>
    /// <para>
    /// BlockOffset provides for some tricky performance optimizations.
    /// A common situation in network protocols is the need to fragment
    /// serialized data across multiple data packets with fixed sized
    /// headers.  Setting BlockOffset to the size of the fixed header
    /// will reserve these bytes at the beginning of each block.  The
    /// data can be serialized into the array and then afterwards, the
    /// headers can be written manually into each block.  This technique
    /// can avoid lots of buffer copying.
    /// </para>
    /// <para>
    /// <note>
    /// You should call <see cref="BlockArray.Reload" /> after 
    /// directly modifying the <see cref="Block.Length" /> or <see cref="Block.Offset" />
    /// properties of any of the blocks in the array.  It is not necessary 
    /// to call this for changes to the Block.Buffer 
    /// array.
    /// </note>
    /// </para>
    /// </remarks>
    public sealed class BlockArray
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Implicit cast from and array of Blocks into a BlockArray.
        /// </summary>
        public static implicit operator BlockArray(Block[] blocks)
        {
            return new BlockArray(blocks);
        }

        //---------------------------------------------------------------------
        // Instance members

        private List<Block>     list;                   // Underlying block array
        private int             size;                   // Logical byte size of the array
        private int             blockSize   = 1024;     // Size of blocks added when extending the array
        private int             blockOffset = 0;        // Bytes to reserve at the beginning of new blocks

        // These fields cache information relating a logical index
        // into the block array to the physical location of the
        // referenced byte.  The idea here is improve the performance
        // of operations that sweep forward through the array.

        private int             lastIndex      = -1;    // Logical index of the last referenced item (or -1)
        private Block           lastBlock      = null;  // Corresponding block (or null)
        private int             lastBlockIndex = -1;    // Corresponding index of the block in the list (or -1)
        private int             lastBlockPos   = -1;    // Position of the indexed byte in the Block (or -1)

        /// <summary>
        /// Constructs an empty list.
        /// </summary>
        public BlockArray()
        {
            this.list = new List<Block>();
            this.size = 0;
        }

        /// <summary>
        /// Constructs an array with the specified capacity.
        /// </summary>
        /// <param name="capacity">The capacity in bytes.</param>
        public BlockArray(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentException();
            }

            this.list = new List<Block>();
            this.size = 0;

            ExtendTo(capacity);
        }

        /// <summary>
        /// Constructs an array with the specified capacity and block size.
        /// </summary>
        /// <param name="capacity">The capacity in bytes.</param>
        /// <param name="blockSize">Size of blocks added when extending the array.</param>
        public BlockArray(int capacity, int blockSize)
        {
            if (capacity < 0 || blockSize <= 0)
            {
                throw new ArgumentException();
            }

            this.list      = new List<Block>();
            this.size      = 0;
            this.blockSize = blockSize;

            ExtendTo(capacity);
        }

        /// <summary>
        /// Constructs an array with the specified capacity, block size, and block offset.
        /// </summary>
        /// <param name="capacity">The capacity in bytes.</param>
        /// <param name="blockSize">Size of blocks added when extending the array.</param>
        /// <param name="blockOffset">Bytes to be reserved at the beginning of each new block.</param>
        public BlockArray(int capacity, int blockSize, int blockOffset)
        {
            if (capacity < 0 || blockSize <= 0 || blockSize <= blockOffset)
            {
                throw new ArgumentException();
            }

            this.list        = new List<Block>();
            this.size        = 0;
            this.blockSize   = blockSize;
            this.blockOffset = blockOffset;

            ExtendTo(capacity);
        }

        /// <summary>
        /// Constructs an array from the blocks passed.
        /// </summary>
        /// <param name="blocks">The blocks.</param>
        public BlockArray(params Block[] blocks)
        {
            this.list = new List<Block>(blocks.Length);
            this.size = 0;

            for (int i = 0; i < blocks.Length; i++)
            {
                this.Append(blocks[i]);
            }
        }

        /// <summary>
        /// Constructs a block array from a buffer.
        /// </summary>
        /// <param name="buffer">The buffer to append.</param>
        public BlockArray(byte[] buffer)
        {
            this.list = new List<Block>();
            this.size = 0;

            this.Append(buffer);
        }

        /// <summary>
        /// Clears the cached position related fields.
        /// </summary>
        private void ClearPos()
        {
            lastIndex      = -1;
            lastBlock      = null;
            lastBlockIndex = -1;
            lastBlockPos   = -1;
        }

        /// <summary>
        /// Reloads cached information about the blocks in the array.
        /// </summary>
        /// <remarks>
        /// This should be called after making changes to the Length
        /// property of any blocks in the array.
        /// </remarks>
        public void Reload()
        {
            size = 0;

            for (int i = 0; i < list.Count; i++)
            {
                size += (list[i]).Length;
            }

            ClearPos();
        }

        /// <summary>
        /// Calculates the block and position of the specified logically indexed
        /// byte in the block.
        /// </summary>
        /// <param name="index">The logical index.</param>
        /// <remarks>
        /// The method updates the lastIndex, lastBlock, lastBlockIndex, 
        /// and lastBlockPos with the calculated values.
        /// </remarks>
        private void CalcPos(int index)
        {
            // I'm optimizing for forward sweeping reads/writes since this
            // will be common for BlockStream as well as block read/write operations.

            if (index < 0 || index >= size)
            {
                throw new IndexOutOfRangeException();
            }

            if (lastIndex == -1)
            {
                lastIndex      = 0;
                lastBlockIndex = 0;
                lastBlockPos   = 0;
                lastBlock      = list[0];
            }

            if (index == lastIndex)
            {
                return;
            }
            else if (index == lastIndex + 1)
            {
                if (lastBlockPos + 1 < lastBlock.Length)
                {
                    lastBlockPos++;
                }
                else
                {
                    while (true)
                    {
                        lastBlock = list[++lastBlockIndex];

                        if (lastBlock.Length > 0)
                        {
                            break;
                        }
                    }

                    lastBlockPos = 0;
                }
            }
            else
            {
                int cb;

                cb             = 0;
                lastBlockIndex = 0;

                while (true)
                {
                    lastBlock = list[lastBlockIndex];

                    if (cb + lastBlock.Length > index)
                    {
                        lastBlockPos = index - cb;
                        break;
                    }

                    cb += lastBlock.Length;
                    lastBlockIndex++;
                }
            }

            lastIndex = index;
        }

        /// <summary>
        /// Used internally by unit tests to reset any internal positional
        /// optimization information maintained by the class.
        /// </summary>
        public void Reset()
        {
            ClearPos();
        }

        /// <summary>
        /// Appends a block formed by a buffer to the array.
        /// </summary>
        /// <param name="buffer">The buffer to add.</param>
        public void Append(byte[] buffer)
        {
            list.Add(new Block(buffer));
            size += buffer.Length;
        }

        /// <summary>
        /// Appends a block to end of the array.
        /// </summary>
        /// <param name="block">The new block.</param>
        public void Append(Block block)
        {
            list.Add(block);
            size += block.Length;
        }

        /// <summary>
        /// Appends all blocks from a block array to this array.
        /// </summary>
        /// <param name="blocks">The source array.</param>
        public void Append(BlockArray blocks)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                Append(blocks.GetBlock(i));
            }
        }

        /// <summary>
        /// Appends blocks from a block array to this array.
        /// </summary>
        /// <param name="blocks">The source array.</param>
        /// <param name="index">Index of the first block to append.</param>
        /// <param name="count">Number of blocks to append.</param>
        public void Append(BlockArray blocks, int index, int count)
        {
            for (int i = index; i < count + index; i++)
            {
                Append(blocks.GetBlock(i));
            }
        }

        /// <summary>
        /// The default offset to use when adding new blocks to the array.
        /// </summary>
        public int BlockOffset
        {
            get { return blockOffset; }

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException();
                }

                blockOffset = value;
            }
        }

        /// <summary>
        /// The size of new blocks added when extending the array.
        /// </summary>
        public int BlockSize
        {
            get { return blockSize; }

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException();
                }

                blockSize = value;
            }
        }

        /// <summary>
        /// Returns the number of blocks in the list.
        /// </summary>
        public int Count
        {
            get { return list.Count; }
        }

        /// <summary>
        /// Returns the total size of all the blocks in bytes.
        /// </summary>
        public int Size
        {
            get { return size; }
        }

        /// <summary>
        /// Adds blocks to the array as necessary to ensure that the total size
        /// of these blocks is at least equal to the value passed.
        /// </summary>
        /// <param name="capacity">The minimum requested capacity in bytes.</param>
        public void ExtendTo(int capacity)
        {
            if (size == capacity)
            {
                return;
            }

            while (size < capacity)
            {
                Append(new Block(new byte[blockSize], blockOffset, blockSize - blockOffset));
            }
        }

        /// <summary>
        /// Removes blocks from the and of the array array such that only 
        /// those blocks necessary to achieve the specified capacity remain.
        /// </summary>
        /// <param name="capacity">The desired capacity in bytes.</param>
        /// <remarks>
        /// The method does nothing if the requested capacity is larger
        /// than the current size of the blocks.
        /// </remarks>
        public void TruncateTo(int capacity)
        {
            if (capacity == size)
            {
                return;
            }

            ClearPos();

            if (capacity == 0)
            {
                list.Clear();
                size = 0;
                return;
            }

            int cBlocks = list.Count;

            size = 0;

            for (int i = 0; i < list.Count; i++)
            {
                size += (list[i]).Length;

                if (size >= capacity)
                {
                    cBlocks = i + 1;
                    break;
                }
            }

            if (list.Count > cBlocks)
            {
                list.RemoveRange(cBlocks, list.Count - cBlocks);
            }
        }

        /// <summary>
        /// Adjusts the blocks in the array such that their sizes
        /// total exactly to the value passed.
        /// </summary>
        /// <param name="capacity">The desired size.</param>
        /// <remarks>
        /// The method removes or appends blocks onto the end of the
        /// array to reach the desired size.  The method will also
        /// adjust the length of the final block if necessary.
        /// </remarks>
        public void SetExactSize(int capacity)
        {
            if (capacity == size)
            {
                return;
            }

            ClearPos();

            if (capacity == 0)
            {
                list.Clear();
                size = 0;
                return;
            }

            if (size < capacity)
            {
                ExtendTo(capacity);
            }
            else
            {
                TruncateTo(capacity);
            }

            if (size > capacity)
            {
                Block last;

                last         = list[list.Count - 1];
                last.Length -= size - capacity;
                size         = capacity;
            }
        }

        /// <summary>
        /// Accesses the indexed byte in the logical array formed by
        /// concatentating all of the blocks.
        /// </summary>
        public byte this[int index]
        {
            get
            {
                CalcPos(index);
                return lastBlock[lastBlockPos];
            }

            set
            {
                CalcPos(index);
                lastBlock[lastBlockPos] = value;
            }
        }

        /// <summary>
        /// Copies bytes from the logical offset in the blocks to the target byte array.
        /// </summary>
        /// <param name="sourceOffset">Logical offset of the first byte to copy.</param>
        /// <param name="target">The output byte array.</param>
        /// <param name="targetOffset">Target offset where the first byte is to be written.</param>
        /// <param name="length"></param>
        public void CopyTo(int sourceOffset, byte[] target, int targetOffset, int length)
        {
            int     cbRemain, cbAvail, cbCopy;
            int     outPos;

            if (length <= 0)
            {
                return;
            }

            CalcPos(sourceOffset);
            if (sourceOffset + length > this.size)
            {
                throw new IndexOutOfRangeException();
            }

            cbRemain = length;
            outPos   = targetOffset;

            while (true)
            {
                cbAvail = lastBlock.Length - lastBlockPos;

                if (cbAvail >= cbRemain)
                {
                    // There's enough data remaining in the current buffer
                    // to complete the copy.

                    cbCopy = cbRemain;
                    lastBlock.CopyTo(lastBlockPos, target, outPos, cbCopy);

                    lastBlockPos += cbCopy;

                    if (lastBlockPos == lastBlock.Length)
                    {
                        lastBlockIndex++;

                        if (lastBlockIndex >= list.Count)
                        {
                            // We've reached the end of the blocks

                            ClearPos();
                            break;
                        }

                        lastBlockPos = 0;
                        lastBlock = list[lastBlockIndex];
                    }

                    lastIndex += length;
                    break;
                }
                else
                {
                    // There's only enough data in the current block to 
                    // do a partial copy.

                    cbCopy = cbAvail;
                    lastBlock.CopyTo(lastBlockPos, target, outPos, cbCopy);

                    lastBlockIndex++;
                    Covenant.Assert(lastBlockIndex < list.Count);

                    lastBlockPos = 0;
                    lastBlock    = list[lastBlockIndex];
                    outPos      += cbCopy;
                }

                cbRemain -= cbCopy;
            }
        }

        /// <summary>
        /// Copies bytes from the byte array passed into the blocks.
        /// </summary>
        /// <param name="source">The source byte array.</param>
        /// <param name="sourceOffset">Offset of the first byte to copy from the source array.</param>
        /// <param name="targetOffset">Logical offset of the first target byte in the buffer references.</param>
        /// <param name="length">Number of bytes to copy.</param>
        public void CopyFrom(byte[] source, int sourceOffset, int targetOffset, int length)
        {
            int     cbRemain, cbAvail, cbCopy;
            int     inPos;

            if (length <= 0)
            {
                return;
            }

            CalcPos(targetOffset);

            if (targetOffset + length > this.size)
            {
                throw new IndexOutOfRangeException();
            }

            cbRemain = length;
            inPos    = sourceOffset;

            while (true)
            {
                cbAvail = lastBlock.Length - lastBlockPos;

                if (cbAvail >= cbRemain)
                {
                    // There's enough data remaining in the current block
                    // to complete the copy.

                    cbCopy = cbRemain;
                    lastBlock.CopyFrom(source, inPos, lastBlockPos, cbCopy);

                    lastBlockPos += cbCopy;

                    if (lastBlockPos == lastBlock.Length)
                    {
                        lastBlockIndex++;

                        if (lastBlockIndex >= list.Count)
                        {
                            // We've reached the end of the blocks

                            ClearPos();
                            break;
                        }

                        lastBlockPos = 0;
                        lastBlock    = list[lastBlockIndex];
                    }

                    lastIndex += length;
                    break;
                }
                else
                {
                    // There's only enough data in the current block to 
                    // do a partial copy.

                    cbCopy = cbAvail;
                    lastBlock.CopyFrom(source, inPos, lastBlockPos, cbCopy);

                    lastBlockIndex++;
                    Covenant.Assert(lastBlockIndex < list.Count);

                    lastBlockPos = 0;
                    lastBlock    = list[lastBlockIndex];
                    inPos       += cbCopy;
                }

                cbRemain -= cbCopy;
            }
        }

        /// <summary>
        /// Assembles the blocks referenced by the array into a contiguous
        /// byte array.
        /// </summary>
        /// <returns>A contiguous byte array.</returns>
        public byte[] ToByteArray()
        {
            byte[]  assembled = new byte[size];
            int     pos;

            pos = 0;

            for (int i = 0; i < list.Count; i++)
            {
                var block = list[i];

                block.CopyTo(0, assembled, pos, block.Length);
                pos += block.Length;
            }

            return assembled;
        }

        /// <summary>
        /// Returns an array to the underlying blocks.
        /// </summary>
        public Block[] GetBlocks()
        {
            Block[] blocks;

            blocks = new Block[list.Count];
            list.CopyTo(0, blocks, 0, list.Count);

            return blocks;
        }

        /// <summary>
        /// Returns the indexed block in the list.
        /// </summary>
        /// <param name="index">The index (0..Count-1).</param>
        /// <returns>The block.</returns>
        public Block GetBlock(int index)
        {
            return list[index];
        }

        /// <summary>
        /// Extracts a range of bytes from the array into newly
        /// created block array.
        /// </summary>
        /// <param name="index">Logical index of the first byte.</param>
        /// <param name="length">Number of bytes to extract.</param>
        /// <returns>A new block array referencing the bytes.</returns>
        /// <remarks>
        /// <note>
        /// Although this method does create a new BlockArray
        /// and Block objects, it does not copy the underlying buffers.
        /// Instead, it adjusts the new Block objects to reference the
        /// requested portions of the original underlying buffers.
        /// </note>
        /// </remarks>
        public BlockArray Extract(int index, int length)
        {
            BlockArray  extracted = new BlockArray();
            int         cbRemain;
            int         blockIndex;
            int         pos;
            int         cb;
            Block       srcBlock;
            Block       dstBlock;

            if (length <= 0)
            {
                return extracted;
            }

            if (index + length > size)
            {
                throw new IndexOutOfRangeException();
            }

            if (index == 0 && length == size)
            {
                // Return a clone of this instance.

                for (int i = 0; i < list.Count; i++)
                {
                    srcBlock = list[i];
                    extracted.Append(new Block(srcBlock.Buffer, srcBlock.Offset, srcBlock.Length));
                }

                return extracted;
            }

            CalcPos(index);

            cbRemain   = length;
            blockIndex = lastBlockIndex;
            srcBlock   = list[blockIndex];
            pos        = lastBlockPos + srcBlock.Offset;

            while (true)
            {
                cb = srcBlock.Length + srcBlock.Offset - pos;

                if (cb > cbRemain)
                {
                    cb = cbRemain;
                }

                dstBlock = new Block(srcBlock.Buffer, pos, cb);
                extracted.Append(dstBlock);

                cbRemain -= cb;

                if (cbRemain <= 0)
                {
                    break;
                }

                srcBlock = list[++blockIndex];
                pos      = srcBlock.Offset;
            }

            return extracted;
        }

        /// <summary>
        /// Extracts a range of bytes from the array from the specified
        /// index to the end of the array into newly created block array.
        /// </summary>
        /// <param name="index">Logical index of the first byte.</param>
        /// <returns>A new block array referencing the bytes.</returns>
        /// <remarks>
        /// <note>
        /// Although this method does create a new BlockArray
        /// and Block objects, it does not copy the underlying buffers.
        /// Instead, it adjusts the new Block objects to reference the
        /// requested portions of the original underlying buffers.
        /// </note>
        /// </remarks>
        public BlockArray Extract(int index)
        {
            return Extract(index, size - index);
        }

        /// <summary>
        /// Returns a shallow copy of the block array.
        /// </summary>
        /// <returns>The cloned array.</returns>
        /// <remarks>
        /// A new set of Block objects will be returned but they
        /// will point to the same underlying buffers.
        /// </remarks>
        public BlockArray Clone()
        {
            return Extract(0, size);
        }
    }
}
