//-----------------------------------------------------------------------------
// FILE:	    Test_Stream.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
    public class Test_Stream
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Write()
        {
            using (var ms = new MemoryStream())
            {
                ms.Write(new byte[0]);

                Assert.Equal<long>(0, ms.Length);

                ms.Write(new byte[] { 0, 1, 2, 3, 4 });

                Assert.Equal<long>(5, ms.Length);

                ms.Position = 0;

                var bytes = new byte[5];

                ms.Read(bytes, 0, 5);

                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, bytes);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task WriteAsync()
        {
            using (var ms = new MemoryStream())
            {
                await ms.WriteAsync(new byte[0]);

                Assert.Equal<long>(0, ms.Length);

                await ms.WriteAsync(new byte[] { 0, 1, 2, 3, 4 });

                Assert.Equal<long>(5, ms.Length);

                ms.Position = 0;

                var bytes = new byte[5];

                ms.Read(bytes, 0, 5);

                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, bytes);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ReadToEnd()
        {
            using (var ms = new MemoryStream())
            {
                var data = new byte[128 * 1024 - 221];

                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (byte)i;
                }

                ms.Write(data);
                ms.Position = 0;

                Assert.Equal(data, ms.ReadToEnd());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task ReadToEndAsync()
        {
            using (var ms = new MemoryStream())
            {
                var data = new byte[128 * 1024 - 221];

                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (byte)i;
                }

                ms.Write(data);
                ms.Position = 0;

                Assert.Equal(data, await ms.ReadToEndAsync());
            }
        }
    }
}