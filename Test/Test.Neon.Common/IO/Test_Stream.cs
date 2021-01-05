//-----------------------------------------------------------------------------
// FILE:	    Test_Stream.cs
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
    public class Test_Stream
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Write()
        {
            using (var ms = new MemoryStream())
            {
                ms.Write(Array.Empty<byte>());

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
                await ms.WriteAsync(Array.Empty<byte>());

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