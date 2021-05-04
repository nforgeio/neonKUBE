//-----------------------------------------------------------------------------
// FILE:	    Test_BlockStream.cs
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
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Area, TestArea.NeonCommon)]
    public class Test_BlockStream
    {
        [Fact]
        public void Basic()
        {
            BlockStream s;
            byte b;
            int cb;
            byte[] buf;
            byte[] cmp;

            //-------------------------

            cb = 1024;

            s = new BlockStream();
            Assert.Equal(0, s.Length);
            Assert.Equal(0, s.Position);
            for (int i = 0; i < cb; i++)
            {
                s.WriteByte((byte)i);
                Assert.Equal((long)(i + 1), s.Position);
                Assert.Equal((long)(i + 1), s.Length);
            }

            s.Position = 0;
            for (int i = 0; i < cb; i++)
            {
                b = (byte)s.ReadByte();
                Assert.Equal((byte)i, b);
                Assert.Equal((long)(i + 1), s.Position);
                Assert.Equal(cb, s.Length);
            }

            cmp = new byte[cb];
            for (int i = 0; i < cb; i++)
                cmp[i] = (byte)i;

            buf = new byte[cb];
            s.Position = 0;
            s.Read(buf, 0, cb);
            Assert.Equal(cmp, buf);
            Assert.Equal((long)cb, s.Position);
            Assert.Equal((long)cb, s.Length);

            //-------------------------

            cb = 256;

            s = new BlockStream(cb);
            Assert.Equal(0, s.Length);
            Assert.Equal(0, s.Position);
            for (int i = 0; i < cb; i++)
            {
                s.WriteByte((byte)i);
                Assert.Equal((long)(i + 1), s.Position);
                Assert.Equal((long)(i + 1), s.Length);
            }

            s.Position = 0;
            for (int i = 0; i < cb; i++)
            {
                b = (byte)s.ReadByte();
                Assert.Equal((byte)i, b);
                Assert.Equal((long)(i + 1), s.Position);
                Assert.Equal(cb, s.Length);
            }

            cmp = new byte[cb];
            for (int i = 0; i < cb; i++)
                cmp[i] = (byte)i;

            buf = new byte[cb];
            s.Position = 0;
            s.Read(buf, 0, cb);
            Assert.Equal(cmp, buf);
            Assert.Equal((long)cb, s.Position);
            Assert.Equal((long)cb, s.Length);

            //-------------------------

            cb = 3 * 256 + 1;

            s = new BlockStream(cb, 256);
            Assert.Equal(0, s.Length);
            Assert.Equal(0, s.Position);
            for (int i = 0; i < cb; i++)
            {
                s.WriteByte((byte)i);
                Assert.Equal((long)(i + 1), s.Position);
                Assert.Equal((long)(i + 1), s.Length);
            }

            s.Position = 0;
            for (int i = 0; i < cb; i++)
            {
                b = (byte)s.ReadByte();
                Assert.Equal((byte)i, b);
                Assert.Equal((long)(i + 1), s.Position);
                Assert.Equal(cb, s.Length);
            }

            cmp = new byte[cb];
            for (int i = 0; i < cb; i++)
                cmp[i] = (byte)i;

            buf = new byte[cb];
            s.Position = 0;
            s.Read(buf, 0, cb);
            Assert.Equal(cmp, buf);
            Assert.Equal((long)cb, s.Position);
            Assert.Equal((long)cb, s.Length);

            //-------------------------

            cb = 300;

            s = new BlockStream(new Block(cb / 3), new Block(cb / 3), new Block(cb / 3));
            Assert.Equal(cb, s.Length);
            Assert.Equal(0, s.Position);
            for (int i = 0; i < cb; i++)
            {
                s.WriteByte((byte)i);
                Assert.Equal((long)(i + 1), s.Position);
            }

            s.Position = 0;
            for (int i = 0; i < cb; i++)
            {
                b = (byte)s.ReadByte();
                Assert.Equal((byte)i, b);
                Assert.Equal((long)(i + 1), s.Position);
            }

            cmp = new byte[cb];
            for (int i = 0; i < cb; i++)
                cmp[i] = (byte)i;

            buf = new byte[cb];
            s.Position = 0;
            s.Read(buf, 0, cb);
            Assert.Equal(cmp, buf);
            Assert.Equal((long)cb, s.Position);

            //-------------------------

            cb = 300;

            s = new BlockStream(new BlockArray(new Block[] { new Block(cb / 3), new Block(cb / 3), new Block(cb / 3) }));
            Assert.Equal(cb, s.Length);
            Assert.Equal(0, s.Position);
            for (int i = 0; i < cb; i++)
            {
                s.WriteByte((byte)i);
                Assert.Equal((long)(i + 1), s.Position);
            }

            s.Position = 0;
            for (int i = 0; i < cb; i++)
            {
                b = (byte)s.ReadByte();
                Assert.Equal((byte)i, b);
                Assert.Equal((long)(i + 1), s.Position);
            }

            cmp = new byte[cb];
            for (int i = 0; i < cb; i++)
                cmp[i] = (byte)i;

            buf = new byte[cb];
            s.Position = 0;
            s.Read(buf, 0, cb);
            Assert.Equal(cmp, buf);
            Assert.Equal((long)cb, s.Position);
        }

        [Fact]
        public void ReadWriteByte()
        {
            BlockStream s;
            int cb;

            s = new BlockStream();
            cb = 1024 * 1024;

            Assert.Equal(-1, s.ReadByte());
            Assert.Equal(0, s.Length);
            Assert.Equal(0, s.Position);

            for (int i = 0; i < cb; i++)
                s.WriteByte((byte)i);

            s.Position = 0;

            for (int i = 0; i < cb; i++)
                Assert.Equal((byte)i, s.ReadByte());

            Assert.Equal(cb, s.Length);
            Assert.Equal(cb, s.Position);
            Assert.Equal(-1, s.ReadByte());
            Assert.Equal(cb, s.Length);
            Assert.Equal(cb, s.Position);
        }

        private void Zero(byte[] buf)
        {
            for (int i = 0; i < buf.Length; i++)
                buf[i] = 0;
        }

        [Fact]
        public void ReadWriteBuffer()
        {
            BlockStream s;
            byte[] r;
            byte[] w;
            byte[] c;

            s = new BlockStream();

            r = new byte[10];
            w = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            Assert.Equal(0, s.Read(r, 0, 10));
            Assert.Equal(0, s.Length);
            Assert.Equal(0, s.Position);

            s.Write(w, 0, 10);
            Assert.Equal(10, s.Length);
            Assert.Equal(10, s.Position);

            s.Position = 0;
            Zero(r);
            s.Read(r, 0, 10);
            Assert.Equal(10, s.Length);
            Assert.Equal(10, s.Position);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, r);

            s.Position = 5;
            Zero(r);
            Assert.Equal(5, s.Read(r, 0, 10));
            Assert.Equal(new byte[] { 5, 6, 7, 8, 9, 0, 0, 0, 0, 0 }, r);

            s.Position = 5;
            Zero(r);
            Assert.Equal(5, s.Read(r, 5, 5));
            Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 5, 6, 7, 8, 9 }, r);

            s = new BlockStream();
            s.Write(w, 5, 5);
            Assert.Equal(5, s.Length);
            Assert.Equal(5, s.Position);

            s.Position = 0;
            Zero(r);
            Assert.Equal(5, s.Read(r, 5, 5));
            Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 5, 6, 7, 8, 9 }, r);

            s = new BlockStream();
            w = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            for (int i = 0; i < 100000; i++)
            {
                w[0] = (byte)(i << 24);
                w[1] = (byte)(i << 16);
                w[2] = (byte)(i << 8);
                w[3] = (byte)(i);

                s.Write(w, 0, 10);
            }

            Assert.Equal(10 * 100000, s.Position);
            Assert.Equal(10 * 100000, s.Length);

            s.Position = 0;
            c = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            for (int i = 0; i < 100000; i++)
            {
                Assert.Equal(10, s.Read(r, 0, 10));

                c[0] = (byte)(i << 24);
                c[1] = (byte)(i << 16);
                c[2] = (byte)(i << 8);
                c[3] = (byte)(i);

                Assert.Equal(c, r);
            }
        }

        [Fact]
        public void SetLength()
        {
            BlockStream s;
            byte[] r = new byte[10];
            byte[] w = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] c = new byte[10];

            s = new BlockStream();
            s.SetLength(100000);
            Assert.Equal(100000, s.Length);
            Assert.Equal(0, s.Position);

            s.Position = 50000;
            s.Write(w, 0, 10);
            Assert.Equal(50010, s.Position);

            s.SetLength(50005);
            Assert.Equal(50005, s.Length);
            Assert.Equal(50005, s.Position);

            s.Position = 50000;
            Zero(r);
            Assert.Equal(5, s.Read(r, 0, 10));
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 0, 0, 0, 0, 0 }, r);

            s.SetLength(0);
            Assert.Equal(0, s.Length);
            Assert.Equal(0, s.Position);
        }

        [Fact]
        public void SetLength_Modify()
        {
            BlockStream s;
            byte[] r = new byte[10];
            byte[] w = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] c = new byte[10];

            s = new BlockStream();
            s.SetLength(100000, true);
            Assert.Equal(100000, s.Length);
            Assert.Equal(0, s.Position);

            s.Position = 50000;
            s.Write(w, 0, 10);
            Assert.Equal(50010, s.Position);

            s.SetLength(50005, true);
            Assert.Equal(50005, s.Length);
            Assert.Equal(50005, s.Position);

            s.Position = 50000;
            Zero(r);
            Assert.Equal(5, s.Read(r, 0, 10));
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 0, 0, 0, 0, 0 }, r);

            s.SetLength(0, true);
            Assert.Equal(0, s.Length);
            Assert.Equal(0, s.Position);
        }

        [Fact]
        public void SetLength_NoModify()
        {
            BlockStream s;
            BlockArray ba;
            byte[] buf;
            int cb;

            ba = new BlockArray(new Block(new byte[] { 0, 1, 2, 3, 4 }), new Block(new byte[] { 5, 6, 7, 8, 9 }));
            s = new BlockStream(ba);

            Assert.Equal(10, s.Length);
            s.Position = 10;
            s.SetLength(5, false);
            Assert.Equal(5, s.Length);
            Assert.Equal(5, s.Position);
            Assert.Equal(2, ba.Count);

            s.Position = 0;
            buf = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            cb = s.Read(buf, 0, 10);
            Assert.Equal(5, cb);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 0, 0, 0, 0, 0 }, buf);

            s.Position = 0;
            s.SetLength(10, false);
            buf = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            cb = s.Read(buf, 0, 10);
            Assert.Equal(10, cb);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, buf);
        }

        [Fact]
        public void Seek()
        {
            BlockStream s = new BlockStream();
            byte[] r = new byte[10];
            byte[] w = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] c = new byte[] { 0, 0, 0, 0, 4, 5, 6, 7, 8, 9 };

            for (int i = 0; i < 100000; i++)
            {

                w[0] = (byte)(i << 24);
                w[1] = (byte)(i << 16);
                w[2] = (byte)(i << 8);
                w[3] = (byte)(i);

                s.Write(w, 0, 10);
            }

            for (int i = 0; i < 100000; i++)
            {
                int pos = i;

                c[0] = (byte)(pos << 24);
                c[1] = (byte)(pos << 16);
                c[2] = (byte)(pos << 8);
                c[3] = (byte)(pos);

                s.Seek(pos * 10, SeekOrigin.Begin);
                Assert.Equal((long)(pos * 10), s.Position);
                Assert.Equal(10, s.Read(r, 0, 10));
                Assert.Equal(c, r);
            }

            for (int i = 0; i < 100000; i++)
            {
                int pos = i;

                c[0] = (byte)(pos << 24);
                c[1] = (byte)(pos << 16);
                c[2] = (byte)(pos << 8);
                c[3] = (byte)(pos);

                s.Position = 50000 * 10;
                s.Seek(pos * 10 - 50000 * 10, SeekOrigin.Current);
                Assert.Equal((long)(pos * 10), s.Position);
                Assert.Equal(10, s.Read(r, 0, 10));
                Assert.Equal(c, r);
            }

            for (int i = 0; i < 100000; i++)
            {
                int pos = i;

                c[0] = (byte)(pos << 24);
                c[1] = (byte)(pos << 16);
                c[2] = (byte)(pos << 8);
                c[3] = (byte)(pos);

                s.Seek(-(100000 * 10 - pos * 10), SeekOrigin.End);
                Assert.Equal((long)(pos * 10), s.Position);
                Assert.Equal(10, s.Read(r, 0, 10));
                Assert.Equal(c, r);
            }
        }

        [Fact]
        public void ToByteArray()
        {
            BlockStream s = new BlockStream();

            s.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 0, 10);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, s.ToArray());
        }

        [Fact]
        public void Append()
        {
            BlockStream s;
            byte[] b1 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] b2 = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 };
            byte[] c;

            //-------------------------

            s = new BlockStream();
            s.Append(new Block(b1));
            Assert.Equal(10, s.Length);
            Assert.Equal(10, s.Position);

            c = new byte[10];
            s.Position = 0;
            Assert.Equal(10, s.Read(c, 0, 10));
            Assert.Equal(b1, c);

            //-------------------------

            s = new BlockStream();
            s.Write(b1, 0, 10);
            s.Append(new Block(b2));
            Assert.Equal(20, s.Length);
            Assert.Equal(20, s.Position);

            c = new byte[20];
            s.Position = 0;
            Assert.Equal(20, s.Read(c, 0, 20));
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 }, c);

            //-------------------------

            s = new BlockStream();
            s.Append(new Block[] { new Block(b1), new Block(b2) });
            Assert.Equal(20, s.Length);
            Assert.Equal(20, s.Position);

            c = new byte[20];
            s.Position = 0;
            Assert.Equal(20, s.Read(c, 0, 20));
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 }, c);
        }

        [Fact]
        public void BlockOffset()
        {
            BlockStream s;
            BlockArray ba;

            s = new BlockStream(0, 10, 5);
            for (int i = 0; i < 20; i++)
                s.WriteByte((byte)i);

            ba = s.ToBlocks(false);
            Assert.Equal(4, ba.Count);
            for (int i = 0; i < ba.Count; i++)
            {

                Block b = ba.GetBlock(i);

                Assert.Equal(5, b.Length);
                Assert.Equal(5, b.Offset);
                for (int j = 0; j < 5; j++)
                    Assert.Equal(i * 5 + j, (int)b.Buffer[b.Offset + j]);
            }
        }

        [Fact]
        public void ReadBlocks()
        {
            BlockStream s = new BlockStream(new Block(new byte[] { 0, 1, 2, 3, 4 }), new Block(new byte[] { 5, 6, 7, 8, 9 }));
            BlockArray ba;

            Assert.Equal(0, s.Position);

            ba = s.ReadBlocks(10);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, ba.ToByteArray());
            Assert.Equal(10, ba.Size);
            Assert.Equal(10, s.Position);

            s.Position = 5;
            ba = s.ReadBlocks(10);
            Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, ba.ToByteArray());
            Assert.Equal(5, ba.Size);
            Assert.Equal(10, s.Position);

            s.Position = 3;
            ba = s.ReadBlocks(5);
            Assert.Equal(new byte[] { 3, 4, 5, 6, 7 }, ba.ToByteArray());
            Assert.Equal(8, s.Position);

            s.Position = 0;
        }

        [Fact]
        public void ReadWriteBlocks()
        {
            var bs      = new BlockStream();
            var maxSize = 16 * 1024;

            for (int i = 0; i < maxSize; i++)
            {
                var data = new byte[i];

                for (int j = 0; j < i; j++)
                {
                    data[j] = (byte)j;
                }

                bs.Write(data, 0, data.Length);
            }

            bs.Seek(0, SeekOrigin.Begin);

            var buffer = new byte[maxSize];

            for (int i = 0; i < maxSize; i++)
            {
                for (var j = 0; j < i; j++)
                {
                    buffer[i] = (byte)(j+1);
                }

                bs.Read(buffer, 0, i);

                for (int j = 0; j < i; j++)
                {
                    Assert.Equal((byte)j, buffer[j]);
                }
            }
        }

        [Fact]
        public void Exceptions()
        {
            Assert.Throws<ArgumentException>(() => new BlockStream(-10));
            Assert.Throws<ArgumentException>(() => new BlockStream(0, -10));
            Assert.Throws<ArgumentException>(() => new BlockStream(0, 0));

            Assert.Throws<IOException>(
                () =>
                {
                    var s = new BlockStream();

                    s.SetLength(-1);
                });

            Assert.Throws<IOException>(
                () =>
                {
                    var s = new BlockStream();

                    s.SetLength((long)int.MaxValue + 1);
                });

            Assert.Throws<IOException>(
                () =>
                {
                    var s = new BlockStream();

                    s.SetLength(5000);
                    s.Position = -1;
                });

            Assert.Throws<IOException>(
                () =>
                {
                    var s = new BlockStream();

                    s.SetLength(5000);
                    s.Position = (long)int.MaxValue + 1;
                });

            Assert.Throws<IOException>(
                () =>
                {
                    var s = new BlockStream();
                    s.SetLength(5000);
                    s.Seek(-1, SeekOrigin.Begin);
                });

            Assert.Throws<IOException>(
                () =>
                {
                    var s = new BlockStream();

                    s.SetLength(5000);
                    s.Position = -1;
                });

            Assert.Throws<IOException>(
                () =>
                {
                    var s = new BlockStream();

                    s.SetLength(5000);
                    s.Seek(5000, SeekOrigin.Begin);
                    s.Seek(-5001, SeekOrigin.Current);
                });

            Assert.Throws<IOException>(
                () =>
                {
                    var s = new BlockStream();

                    s.SetLength(5000);
                    s.Seek(-5001, SeekOrigin.End);
                });

            Assert.Throws<IOException>(
                () =>
                {
                    var s = new BlockStream();

                    s.SetLength(5000);
                    s.Seek((long)int.MaxValue + 1, SeekOrigin.Begin);
                });

            Assert.Throws<IOException>(
                () =>
                {
                    var s = new BlockStream();

                    s.SetLength(5000);
                    s.Seek(5000, SeekOrigin.Begin);
                    s.Seek((long)int.MaxValue - 5000 + 1, SeekOrigin.Current);
                });

            Assert.Throws<IOException>(
                () =>
                {
                    var s = new BlockStream();

                    s.SetLength(5000);
                    s.Seek((long)int.MaxValue - 5000 + 1, SeekOrigin.End);
                });
        }
    }
}
