//-----------------------------------------------------------------------------
// FILE:	    Test_SubStream.cs
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
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    public class Test_SubStream
    {
        [Fact]
        public void Read()
        {
            // Verify that we can support an empty parent stream.

            using (var parent = new MemoryStream())
            {
                using (var substream = new SubStream(parent, 0, 0))
                {
                    var buffer = new byte[10];

                    Assert.Equal(0, substream.Position);
                    Assert.Equal(0, substream.Read(buffer, 0, 10));
                    Assert.Equal(0, substream.Position);
                }
            }

            // Verify that we can substream the entire parent stream.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 0, 10))
                {
                    var buffer = new byte[10];

                    Assert.Equal(0, substream.Position);
                    Assert.Equal(10, substream.Read(buffer, 0, 10));
                    Assert.Equal(10, substream.Position);
                    Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, buffer);
                }
            }

            // Verify that we can substream just a part of a parent stream.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 1, 9))
                {
                    var buffer = new byte[9];

                    Assert.Equal(0, substream.Position);
                    Assert.Equal(9, substream.Read(buffer, 0, 9));
                    Assert.Equal(9, substream.Position);
                    Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, buffer);
                }
            }

            // Verify that reads clip at the logical EOF.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 5, 5))
                {
                    var buffer = new byte[5];

                    Assert.Equal(0, substream.Position);
                    Assert.Equal(5, substream.Read(buffer, 0, 5));
                    Assert.Equal(5, substream.Position);
                    Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, buffer);
                }
            }
        }

        [Fact]
        public void ReadByte()
        {
            // Verify that we can support an empty parent stream.

            using (var parent = new MemoryStream())
            {
                using (var substream = new SubStream(parent, 0, 0))
                {
                    Assert.Equal(0, substream.Position);
                    Assert.Equal(-1, substream.ReadByte());
                    Assert.Equal(0, substream.Position);
                }
            }

            // Verify that we can read all of the bytes from the parent when substream spans everything.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 0, 10))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        Assert.Equal(i, substream.Position);
                        Assert.Equal(i, substream.ReadByte());
                        Assert.Equal(i + 1, substream.Position);
                    }

                    // The next read should return -1.

                    Assert.Equal(10, substream.Position);
                    Assert.Equal(-1, substream.ReadByte());
                    Assert.Equal(10, substream.Position);
                }
            }
        }

        [Fact]
        public void Write()
        {
            // Verify that write fails for empty parent streams.

            using (var parent = new MemoryStream())
            {
                using (var substream = new SubStream(parent, 0, 0))
                {
                    Assert.Throws<IOException>(() => substream.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
                    Assert.Equal(0, substream.Position);
                }
            }

            // Verify that we can write to a substream that spans the entire parent.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[10]);

                using (var substream = new SubStream(parent, 0, 10))
                {
                    substream.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                    Assert.Equal(10, substream.Position);
                }
            }

            // Verify that we can write to a substream that spans the only part parent.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[10]);

                using (var substream = new SubStream(parent, 2, 5))
                {
                    var buffer = new byte[5];

                    substream.Write(new byte[] { 1, 2, 3, 4, 5 });
                    Assert.Equal(5, substream.Position);

                    buffer = new byte[10];

                    parent.Position = 0;
                    parent.Read(buffer, 0, 10);
                    Assert.Equal(new byte[] { 0, 0, 1, 2, 3, 4, 5, 0, 0, 0 }, buffer);
                }
            }

            // Verify that we can't write past the end of the substream data
            // when the substream spans the entire parent.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[10]);

                using (var substream = new SubStream(parent, 0, 10))
                {
                    var buffer = new byte[11];

                    Assert.Throws<IOException>(() => substream.Write(buffer));
                }
            }

            // Verify that we can't write past the end of the substream data
            // when the substream is fully contained within the parent.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[20]);

                using (var substream = new SubStream(parent, 5, 10))
                {
                    var buffer = new byte[11];

                    Assert.Throws<IOException>(() => substream.Write(buffer));
                }
            }
        }

        [Fact]
        public void WriteByte()
        {
            // Verify that we can write bytes to a substream that spans the entire parent.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[10]);

                using (var substream = new SubStream(parent, 0, 10))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        substream.WriteByte((byte)i);
                    }
                }

                parent.Position = 0;
                
                var buffer = parent.ReadBytes(10);

                Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, buffer);
            }

            // Verify that we can write bytes to a substream that is fully contained within the parent.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[10]);

                using (var substream = new SubStream(parent, 2, 5))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        substream.WriteByte((byte)i);
                    }
                }

                parent.Position = 0;

                var buffer = parent.ReadBytes(10);

                Assert.Equal(new byte[] { 0, 0, 0, 1, 2, 3, 4, 0, 0, 0 }, buffer);
            }

            // Verify that we can't write bytes past the end of the substream when
            // the substream spans the entire parent.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[10]);

                using (var substream = new SubStream(parent, 0, 10))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        substream.WriteByte((byte)i);
                    }

                    Assert.Throws<IOException>(() => substream.WriteByte((byte)11));
                }
            }

            // Verify that we can't write bytes past the end of the substream when
            // the substream that is fully contained within the parent.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[10]);

                using (var substream = new SubStream(parent, 2, 5))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        substream.WriteByte((byte)i);
                    }

                    Assert.Throws<IOException>(() => substream.WriteByte((byte)6));
                }
            }
        }

        [Fact]
        public void Seek_Position()
        {
            // Verify that we can seek via the [Position] property.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 5, 5))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        substream.Position = i;
                        Assert.Equal(i + 5, substream.ReadByte());
                    }
                }
            }

            // Verify that we can't seek before the beginning of the substream.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 5, 5))
                {
                    Assert.Throws<IOException>(
                        () =>
                        {
                            substream.Position = -1;
                        });
                }
            }

            // Verify that we can't seek past the end of the substream.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 5, 5))
                {
                    Assert.Throws<IOException>(
                        () =>
                        {
                            substream.Position = 11;
                        });
                }
            }
        }

        [Fact]
        public void Seek_FromBegin()
        {
            // Verify that we can seek via the [Seek/origin] method.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 5, 5))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Assert.Equal(i, substream.Seek(i, SeekOrigin.Begin));
                        Assert.Equal(i + 5, substream.ReadByte());
                    }
                }
            }

            // Verify that we can't seek before the beginning of the substream.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 5, 5))
                {
                    Assert.Throws<IOException>(
                        () =>
                        {
                            substream.Seek(-1, SeekOrigin.Begin);
                        });
                }
            }

            // Verify that we can't seek past the end of the substream.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 5, 5))
                {
                    Assert.Throws<IOException>(
                        () =>
                        {
                            substream.Seek(6, SeekOrigin.Begin);
                        });
                }
            }
        }

        [Fact]
        public void Seek_FromCurrent()
        {
            // Verify that we can seek via the [Seek/current] method.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 0, 10))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        substream.Position = 5;
                        Assert.Equal(i + 5, substream.Seek(i, SeekOrigin.Current));
                        Assert.Equal(i + 5, substream.ReadByte());
                    }
                }
            }

            // Verify that we can't seek before the beginning of the substream.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 5, 5))
                {
                    Assert.Throws<IOException>(
                        () =>
                        {
                            substream.Position = 0;
                            substream.Seek(-1, SeekOrigin.Current);
                        });
                }
            }

            // Verify that we can't seek past the end of the substream.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 5, 5))
                {
                    Assert.Throws<IOException>(
                        () =>
                        {
                            substream.Seek(6, SeekOrigin.Current);
                        });
                }
            }
        }

        [Fact]
        public void Seek_FromEnd()
        {
            // Verify that we can seek via the [Seek/end] method.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 0, 10))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        Assert.Equal(9 - i, substream.Seek(-(i + 1), SeekOrigin.End));
                        Assert.Equal(9 - i, substream.ReadByte());
                    }
                }
            }

            // Verify that we can't seek before the beginning of the substream.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 5, 5))
                {
                    Assert.Throws<IOException>(
                        () =>
                        {
                            substream.Seek(-6, SeekOrigin.End);
                        });
                }
            }

            // Verify that we can't seek past the end of the substream.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 5, 5))
                {
                    Assert.Throws<IOException>(
                        () =>
                        {
                            substream.Seek(1, SeekOrigin.End);
                        });
                }
            }
        }

        [Fact]
        public void Length()
        {
            // Verify substring length when it spans the entire parent.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 0, 10))
                {
                    Assert.Equal(10, substream.Length);
                }
            }

            // Verify substring length when it is fully cointained within the parent.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                using (var substream = new SubStream(parent, 5, 5))
                {
                    Assert.Equal(5, substream.Length);
                }
            }
        }

        [Fact]
        public void Dispose()
        {
            // Verify that substring dispose restores the parent stream position.

            using (var parent = new MemoryStream())
            {
                parent.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                parent.Position = 5;

                using (var substream = new SubStream(parent, 2, 5))
                {
                    Assert.Equal(2, substream.ReadByte());
                }

                Assert.Equal(5, parent.Position);
            }
        }
    }
}
