//-----------------------------------------------------------------------------
// FILE:	    Test_Helper.Compression.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Text;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Xunit;

using Xunit;
using System.IO;

namespace TestCommon
{
    public partial class Test_NeonHelper
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Deflate_Compression()
        {
            var sb = new StringBuilder();

            for (int i = 0; i < 100; i++)
            {
                sb.AppendLine("The quick brown fox jumped over the lazy dog.");
            }

            var uncompressedString = sb.ToString();
            var uncompressedBytes  = Encoding.UTF8.GetBytes(uncompressedString);
            var compressedBytes    = NeonHelper.DeflateString(uncompressedString);

            // Verify that compression actually worked.

            Assert.True(uncompressedBytes.Length > compressedBytes.Length);

            // Verify that decompression works.

            Assert.Equal(uncompressedBytes, NeonHelper.InflateBytes(compressedBytes));
            Assert.Equal(uncompressedBytes, NeonHelper.InflateBytes(NeonHelper.DeflateBytes(uncompressedBytes)));
            Assert.Equal(uncompressedString, NeonHelper.InflateString(compressedBytes));

            // Verify stream compression.

            using (var msUncompressed = new MemoryStream(uncompressedBytes))
            {
                using (var msCompressed = new MemoryStream())
                {
                    msUncompressed.DeflateTo(msCompressed);
                    Assert.Equal(compressedBytes, msCompressed.ToArray());
                }
            }

            // Verify stream decompression.

            using (var msCompressed = new MemoryStream(compressedBytes))
            {
                Assert.False(NeonHelper.IsGzipped(msCompressed));

                using (var msUncompressed = new MemoryStream())
                {
                    msCompressed.InflateTo(msUncompressed);
                    Assert.Equal(uncompressedBytes, msUncompressed.ToArray());
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Gzip_Compression()
        {
            var sb = new StringBuilder();

            for (int i = 0; i < 100; i++)
            {
                sb.AppendLine("The quick brown fox jumped over the lazy dog.");
            }

            var uncompressedString = sb.ToString();
            var uncompressedBytes  = Encoding.UTF8.GetBytes(uncompressedString);
            var compressedBytes    = NeonHelper.GzipString(uncompressedString);

            // Verify that compression actually worked.

            Assert.True(uncompressedBytes.Length > compressedBytes.Length);

            // Verify that decompression works.

            Assert.Equal(uncompressedBytes, NeonHelper.GunzipBytes(compressedBytes));
            Assert.Equal(uncompressedBytes, NeonHelper.GunzipBytes(NeonHelper.GzipBytes(uncompressedBytes)));
            Assert.Equal(uncompressedString, NeonHelper.GunzipString(compressedBytes));

            // Verify stream compression.

            using (var msUncompressed = new MemoryStream(uncompressedBytes))
            {
                using (var msCompressed = new MemoryStream())
                {
                    msUncompressed.GzipTo(msCompressed);
                    Assert.Equal(compressedBytes, msCompressed.ToArray());
                }
            }

            // Verify stream decompression.

            using (var msCompressed = new MemoryStream(compressedBytes))
            {
                Assert.True(NeonHelper.IsGzipped(msCompressed));

                using (var msUncompressed = new MemoryStream())
                {
                    msCompressed.GunzipTo(msUncompressed);
                    Assert.Equal(uncompressedBytes, msUncompressed.ToArray());
                }
            }
        }
    }
}
