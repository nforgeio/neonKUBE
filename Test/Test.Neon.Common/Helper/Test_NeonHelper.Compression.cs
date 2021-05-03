//-----------------------------------------------------------------------------
// FILE:	    Test_Helper.Compression.cs
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
using System.IO;
using System.Text;

using Newtonsoft.Json;

using Neon.Common;
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_NeonHelper
    {
        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Compression_Deflate()
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
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Compression_Gzip()
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

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Compression_GZip_File()
        {
            using (var tempFolder = new TempFolder())
            {
                const string testText = "Hello World! xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

                var uncompressedPath = Path.Combine(tempFolder.Path, "uncompressed.dat");
                var compressedPath   = Path.Combine(tempFolder.Path, "compressed.dat");

                File.WriteAllText(uncompressedPath, testText);
                NeonHelper.GzipFile(uncompressedPath, compressedPath);

                // Verify that the compressed file is actually smaller.

                using (var compressed = new FileStream(compressedPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    using (var uncompressed = new FileStream(uncompressedPath, FileMode.Open, FileAccess.ReadWrite))
                    {
                        Assert.True(compressed.Length < uncompressed.Length);
                    }
                }

                // Verify that uncompress works

                File.Delete(uncompressedPath);
                NeonHelper.GunzipFile(compressedPath, uncompressedPath);
                Assert.Equal(testText, File.ReadAllText(uncompressedPath));
            }
        }
    }
}
