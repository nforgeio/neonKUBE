//-----------------------------------------------------------------------------
// FILE:        Test_BlockArray.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Area, TestArea.NeonCommon)]
    public class Test_BlockArray
    {
        [Fact]
        public void Construction()
        {
            BlockArray blocks;

            blocks = new BlockArray();
            Assert.Equal(0, blocks.Size);

            blocks.ExtendTo(1);
            Assert.Equal(blocks.BlockSize, blocks.Size);

            blocks = new BlockArray(1);
            Assert.Equal(blocks.BlockSize, blocks.Size);

            blocks = new BlockArray(1, 10);
            Assert.Equal(10, blocks.Size);

            blocks = new BlockArray(10, 10);
            Assert.Equal(10, blocks.Size);

            blocks = new BlockArray(new Block(10), new Block(10), new Block(10));
            Assert.Equal(30, blocks.Size);
        }

        [Fact]
        public void Indexing()
        {
            BlockArray blocks;

            blocks = new BlockArray(512, 512);

            blocks[0] = 55;
            blocks[1] = 56;
            blocks[511] = 57;

            Assert.Equal(55, blocks[0]);
            Assert.Equal(56, blocks[1]);
            Assert.Equal(57, blocks[511]);

            for (int i = 0; i < 512; i++)
                blocks[i] = (byte)i;

            for (int i = 0; i < 512; i++)
                Assert.Equal((byte)i, blocks[i]);

            for (int i = 511; i <= 0; i--)
                Assert.Equal((byte)i, blocks[i]);

            blocks = new BlockArray();
            blocks.Append(new Block(10));
            blocks.Append(new Block(10));
            blocks.Append(new Block(10));
            blocks[0] = 0;
            blocks[10] = 10;
            blocks[20] = 20;
            Assert.Equal(0, blocks[0]);
            Assert.Equal(10, blocks[10]);
            Assert.Equal(20, blocks[20]);

            blocks = new BlockArray();
            blocks.Append(new Block(10));
            blocks.Append(new Block(10));
            blocks.Append(new Block(10));

            Assert.Equal(30, blocks.Size);
            for (int i = 0; i < 30; i++)
                blocks[i] = (byte)i;

            Assert.Equal(0, blocks[0]);

            for (int i = 0; i < 30; i++)
                Assert.Equal((byte)i, blocks[i]);

            for (int i = 29; i >= 0; i--)
                Assert.Equal((byte)i, blocks[i]);

            for (int i = 29; i >= 0; i--)
                blocks[i] = (byte)(i + 10);

            for (int i = 0; i < 30; i++)
                Assert.Equal((byte)(i + 10), blocks[i]);

            for (int i = 29; i >= 0; i--)
                Assert.Equal((byte)(i + 10), blocks[i]);

            blocks = new BlockArray(1000, 1);
            for (int i = 0; i < 1000; i++)
                blocks[i] = (byte)i;

            for (int i = 0; i < 1000; i++)
                Assert.Equal((byte)i, blocks[i]);

            for (int i = 999; i >= 0; i--)
                Assert.Equal((byte)i, blocks[i]);

            for (int i = 0; i < 1000; i++)
            {
                Assert.Equal((byte)i, blocks[i]);
                Assert.Equal((byte)(999 - i), blocks[999 - i]);
            }

            //-------------------------

            blocks = new BlockArray(new Block(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 5, 5));
            Assert.Equal(5, blocks[0]);
        }

        private void CopyToTest(int bufSize, int blockSize, int cbCopy)
        {
            BlockArray blocks;
            byte[] arr;
            byte[] cmp;

            blocks = new BlockArray(bufSize, blockSize);
            for (int i = 0; i < blocks.Size; i++)
                blocks[i] = (byte)i;

            blocks.Reset();
            for (int i = 0; i < blocks.Size - cbCopy; i++)
            {
                arr = new byte[cbCopy];

                cmp = new byte[cbCopy];
                for (int j = 0; j < cbCopy; j++)
                    cmp[j] = (byte)(i + j);

                blocks.CopyTo(i, arr, 0, cbCopy);
                Assert.Equal(cmp, arr);

                if (i < blocks.Size - 1)
                    Assert.Equal((byte)(i + 1), blocks[i + 1]);
            }

            blocks.Reset();
            for (int i = 0; i < blocks.Size - cbCopy; i++)
            {
                arr = new byte[cbCopy];

                cmp = new byte[cbCopy];
                for (int j = 0; j < cbCopy; j++)
                    cmp[j] = (byte)(i + j);

                blocks.CopyTo(i, arr, 0, cbCopy);
                Assert.Equal(cmp, arr);
            }

            blocks.Reset();
            for (int i = 0; i < blocks.Size; i++)
            {
                arr = new byte[i];
                blocks.CopyTo(0, arr, 0, i);

                cmp = new byte[i];
                for (int j = 0; j < i; j++)
                    cmp[j] = (byte)j;

                Assert.Equal(cmp, arr);

                if (i < blocks.Size - 1)
                    Assert.Equal((byte)(i + 1), blocks[i + 1]);
            }

            blocks.Reset();
            for (int i = 0; i < blocks.Size; i++)
            {
                arr = new byte[i];
                blocks.CopyTo(0, arr, 0, i);

                cmp = new byte[i];
                for (int j = 0; j < i; j++)
                    cmp[j] = (byte)j;

                Assert.Equal(cmp, arr);
            }

            blocks.Reset();
            arr = new byte[cbCopy + 10];
            cmp = new byte[cbCopy + 10];
            for (int i = 0; i < cbCopy; i++)
            {
                blocks[i] = (byte)i;
                cmp[i + 10] = (byte)i;
            }

            blocks.CopyTo(0, arr, 10, cbCopy);
            Assert.Equal(cmp, arr);
        }

        [Fact]
        public void CopyTo()
        {
            CopyToTest(20, 1, 0);
            CopyToTest(20, 1, 1);
            CopyToTest(20, 2, 1);
            CopyToTest(30, 3, 1);
            CopyToTest(30, 3, 2);
            CopyToTest(30, 3, 3);
            CopyToTest(30, 3, 4);
            CopyToTest(30, 3, 5);
            CopyToTest(30, 3, 6);
            CopyToTest(30, 3, 7);
            CopyToTest(30, 3, 9);
            CopyToTest(30, 3, 10);
            CopyToTest(512, 64, 32);
        }

        private void Zero(BlockArray blocks)
        {
            for (int i = 0; i < blocks.Size; i++)
                blocks[i] = 0;

            blocks.Reset();
        }

        private void CopyFromTest(int bufSize, int blockSize, int cbCopy)
        {
            BlockArray blocks;
            byte[] arr;
            byte[] cmp;
            int pos;

            blocks = new BlockArray(bufSize, blockSize);

            Zero(blocks);
            arr = new byte[cbCopy];
            for (int i = 0; i < cbCopy; i++)
                arr[i] = (byte)i;

            blocks.CopyFrom(arr, 0, 0, cbCopy);
            cmp = new byte[bufSize];
            for (int i = 0; i < cbCopy; i++)
                cmp[i] = (byte)i;

            arr = new byte[bufSize];
            blocks.CopyTo(0, arr, 0, bufSize);
            Assert.Equal(cmp, arr);

            Zero(blocks);
            arr = new byte[cbCopy];
            for (int i = 0; i < cbCopy; i++)
                arr[i] = (byte)i;

            if (cbCopy == 0)
                return;

            pos = 0;
            for (int i = 0; i < blockSize / cbCopy; i++)
            {

                blocks.CopyFrom(arr, 0, pos, cbCopy);
                pos += cbCopy;
            }

            pos = 0;
            cmp = new byte[bufSize];
            for (int i = 0; i < blockSize / cbCopy; i++)
                for (int j = 0; j < cbCopy; j++)
                    cmp[pos++] = (byte)j;

            arr = new byte[bufSize];
            blocks.CopyTo(0, arr, 0, bufSize);
            Assert.Equal(cmp, arr);

            Zero(blocks);
            arr = new byte[cbCopy + 10];
            cmp = new byte[cbCopy + 10];
            for (int i = 0; i < cbCopy; i++)
                cmp[i + 10] = (byte)i;

            blocks.CopyFrom(cmp, 10, 0, cbCopy);
            blocks.CopyTo(0, arr, 10, cbCopy);
            Assert.Equal(cmp, arr);
        }

        [Fact]
        public void CopyFrom()
        {
            CopyFromTest(30, 1, 0);
            CopyFromTest(30, 1, 1);
            CopyFromTest(30, 2, 1);
            CopyFromTest(30, 3, 1);
            CopyFromTest(30, 3, 2);
            CopyFromTest(30, 3, 3);
            CopyFromTest(30, 3, 4);
            CopyFromTest(30, 3, 5);
            CopyFromTest(30, 3, 6);
            CopyFromTest(30, 3, 7);
            CopyFromTest(30, 3, 8);
            CopyFromTest(30, 3, 9);
            CopyFromTest(30, 3, 10);
        }

        [Fact]
        public void ExtendTo()
        {
            BlockArray blocks;

            blocks = new BlockArray();
            blocks.ExtendTo(1);
            Assert.Equal(blocks.BlockSize, blocks.Size);
            Assert.Single(blocks.GetBlocks());

            blocks.ExtendTo(blocks.BlockSize / 2);
            Assert.Equal(blocks.BlockSize, blocks.Size);
            Assert.Single(blocks.GetBlocks());
            Assert.Equal(blocks.BlockSize, blocks.GetBlocks()[0].Length);

            blocks.ExtendTo(blocks.BlockSize + 1);
            Assert.Equal(blocks.BlockSize * 2, blocks.Size);
            Assert.Equal(2, blocks.GetBlocks().Length);
            Assert.Equal(blocks.BlockSize, blocks.GetBlocks()[0].Length);
            Assert.Equal(blocks.BlockSize, blocks.GetBlocks()[1].Length);
        }

        [Fact]
        public void TruncateTo()
        {
            BlockArray blocks;

            blocks = new BlockArray();
            blocks.TruncateTo(0);
            Assert.Equal(0, blocks.Size);
            Assert.Empty(blocks.GetBlocks());

            blocks.ExtendTo(1);
            blocks.TruncateTo(1);
            Assert.Equal(blocks.BlockSize, blocks.Size);
            Assert.Single(blocks.GetBlocks());

            blocks.TruncateTo(1000000);
            blocks.TruncateTo(1);
            Assert.Equal(blocks.BlockSize, blocks.Size);
            Assert.Single(blocks.GetBlocks());

            blocks.TruncateTo(0);
            Assert.Equal(0, blocks.Size);
            Assert.Empty(blocks.GetBlocks());

            blocks.ExtendTo(blocks.BlockSize * 4);
            blocks.GetBlocks()[0].Buffer[0] = 0;
            blocks.GetBlocks()[1].Buffer[0] = 1;
            blocks.GetBlocks()[2].Buffer[0] = 2;
            blocks.GetBlocks()[3].Buffer[0] = 3;

            blocks.TruncateTo(blocks.BlockSize * 3);
            Assert.Equal(blocks.BlockSize * 3, blocks.Size);
            Assert.Equal(3, blocks.GetBlocks().Length);
            Assert.Equal(0, blocks.GetBlocks()[0].Buffer[0]);
            Assert.Equal(1, blocks.GetBlocks()[1].Buffer[0]);
            Assert.Equal(2, blocks.GetBlocks()[2].Buffer[0]);

            blocks.TruncateTo(blocks.BlockSize * 2 + 1);
            Assert.Equal(blocks.BlockSize * 3, blocks.Size);
            Assert.Equal(3, blocks.GetBlocks().Length);
            Assert.Equal(0, blocks.GetBlocks()[0].Buffer[0]);
            Assert.Equal(1, blocks.GetBlocks()[1].Buffer[0]);
            Assert.Equal(2, blocks.GetBlocks()[2].Buffer[0]);

            blocks.TruncateTo(blocks.BlockSize);
            Assert.Equal(blocks.BlockSize, blocks.Size);
            Assert.Single(blocks.GetBlocks());
            Assert.Equal(0, blocks.GetBlocks()[0].Buffer[0]);
        }

        [Fact]
        public void SetExactSize()
        {
            BlockArray blocks;

            blocks = new BlockArray();
            blocks.SetExactSize(0);
            Assert.Equal(0, blocks.Size);
            Assert.Equal(0, blocks.Count);

            blocks.SetExactSize(1);
            Assert.Equal(1, blocks.Size);
            Assert.Equal(1, blocks.Count);
            Assert.Equal(1, blocks.GetBlock(0).Length);

            blocks.SetExactSize(blocks.BlockSize);
            Assert.Equal(blocks.BlockSize, blocks.Size);
            Assert.Equal(2, blocks.Count);
            Assert.Equal(1, blocks.GetBlock(0).Length);
            Assert.Equal(blocks.BlockSize - 1, blocks.GetBlock(1).Length);

            blocks.SetExactSize(blocks.BlockSize + 1);
            Assert.Equal(blocks.BlockSize + 1, blocks.Size);
            Assert.Equal(3, blocks.Count);
            Assert.Equal(1, blocks.GetBlock(0).Length);
            Assert.Equal(blocks.BlockSize - 1, blocks.GetBlock(1).Length);
            Assert.Equal(1, blocks.GetBlock(2).Length);

            blocks = new BlockArray();
            blocks.ExtendTo(blocks.BlockSize * 3);
            blocks.SetExactSize(blocks.Size - blocks.BlockSize / 2);
            Assert.Equal(3, blocks.Count);
            Assert.Equal(blocks.BlockSize, blocks.GetBlock(0).Length);
            Assert.Equal(blocks.BlockSize, blocks.GetBlock(1).Length);
            Assert.Equal(blocks.BlockSize - blocks.BlockSize / 2, blocks.GetBlock(2).Length);

            blocks.SetExactSize(blocks.BlockSize * 2);
            Assert.Equal(2, blocks.Count);
            Assert.Equal(blocks.BlockSize, blocks.GetBlock(0).Length);
            Assert.Equal(blocks.BlockSize, blocks.GetBlock(1).Length);

            blocks.SetExactSize(blocks.BlockSize + 1);
            Assert.Equal(2, blocks.Count);
            Assert.Equal(blocks.BlockSize, blocks.GetBlock(0).Length);
            Assert.Equal(1, blocks.GetBlock(1).Length);
        }

        [Fact]
        public void AddFromBlockArray()
        {
            BlockArray ba1;
            BlockArray ba2;

            ba1 = new BlockArray();
            ba1.Append(new byte[] { 0, 1, 2, 3, 4 });

            ba2 = new BlockArray();
            ba2.Append(new byte[] { 5, 6, 7, 8, 9 });
            ba2.Append(new byte[] { 10, 11, 12, 13, 14 });
            ba2.Append(new byte[] { 15, 16, 17, 18, 19 });
            ba2.Append(new byte[] { 20, 21, 22, 23, 24 });

            ba1.Append(ba2, 2, 2);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 }, ba1.ToByteArray());

            ba1.SetExactSize(5);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, ba1.ToByteArray());

            ba1.Append(ba2);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 }, ba1.ToByteArray());
        }

        [Fact]
        public void BlockOffset()
        {
            BlockArray ba;

            ba = new BlockArray(0, 10, 5);
            ba.ExtendTo(10);
            Assert.Equal(10, ba.Size);
            Assert.Equal(10, ba.GetBlock(0).Buffer.Length);
            Assert.Equal(5, ba.GetBlock(0).Length);
            Assert.Equal(5, ba.GetBlock(0).Offset);
            Assert.Equal(10, ba.GetBlock(1).Buffer.Length);
            Assert.Equal(5, ba.GetBlock(1).Length);
            Assert.Equal(5, ba.GetBlock(1).Offset);
        }

        [Fact]
        public void Reload()
        {
            BlockArray ba;
            Block b;
            byte v;

            ba = new BlockArray(0, 10, 5);
            ba.ExtendTo(10);
            Assert.Equal(10, ba.Size);

            v = ba[5];

            b = ba.GetBlock(0);
            b.Offset = 0;
            b.Length = b.Buffer.Length;
            for (int i = 0; i < b.Length; i++)
                b.Buffer[i] = (byte)i;

            b = ba.GetBlock(1);
            b.Offset = 0;
            b.Length = b.Buffer.Length;
            for (int i = 0; i < b.Length; i++)
                b.Buffer[i] = (byte)(i + 100);

            ba.Reload();

            Assert.Equal(20, ba.Size);
            Assert.Equal(5, ba[5]);
        }

        [Fact]
        public void Extract()
        {
            BlockArray ba;
            BlockArray ex;

            //-------------------------

            ba = new BlockArray();
            ex = ba.Extract(0, 0);
            Assert.Equal(0, ex.Size);
            Assert.Equal(0, ex.Count);

            //-------------------------

            ba = new BlockArray(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            ex = ba.Extract(0, 10);
            Assert.Equal(10, ex.Size);
            Assert.Equal(1, ex.Count);
            Assert.Equal(0, ex.GetBlock(0).Offset);
            Assert.Equal(10, ex.GetBlock(0).Length);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, ex.GetBlock(0).Buffer);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, ex.ToByteArray());

            ba.GetBlock(0).Offset = 5;
            ba.GetBlock(0).Length = 5;
            ba.Reload();
            Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, ba.ToByteArray());
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, ex.ToByteArray());

            //-------------------------

            ba = new BlockArray(new Block(new byte[] { 0, 0, 0, 0, 0, 0, 1, 2, 3, 4 }, 5, 5), new Block(new byte[] { 0, 0, 0, 0, 0, 5, 6, 7, 8, 9 }, 5, 5));
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, ba.ToByteArray());
            ex = ba.Extract(0, 10);
            Assert.Equal(2, ex.Count);
            Assert.Equal(5, ex.GetBlock(0).Offset);
            Assert.Equal(5, ex.GetBlock(0).Length);
            Assert.Equal(5, ex.GetBlock(1).Offset);
            Assert.Equal(5, ex.GetBlock(1).Length);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, ex.ToByteArray());

            ex = ba.Extract(2, 5);
            Assert.Equal(2, ex.Count);
            Assert.Equal(7, ex.GetBlock(0).Offset);
            Assert.Equal(3, ex.GetBlock(0).Length);
            Assert.Equal(5, ex.GetBlock(1).Offset);
            Assert.Equal(2, ex.GetBlock(1).Length);
            Assert.Equal(new byte[] { 2, 3, 4, 5, 6 }, ex.ToByteArray());

            ex = ba.Extract(5);
            Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, ex.ToByteArray());
        }

        [Fact]
        public void Clone()
        {
            BlockArray ba;
            BlockArray ex;

            ba = new BlockArray(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            ex = ba.Clone();
            Assert.Equal(10, ex.Size);
            Assert.Equal(1, ex.Count);
            Assert.Equal(0, ex.GetBlock(0).Offset);
            Assert.Equal(10, ex.GetBlock(0).Length);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, ex.GetBlock(0).Buffer);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, ex.ToByteArray());
        }
    }
}

