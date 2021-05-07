//-----------------------------------------------------------------------------
// FILE:	    Test_TempFile.cs
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
    public class Test_TempFile
    {
        private string tempPath;

        public Test_TempFile()
        {
            if (NeonHelper.IsWindows)
            {
                tempPath = Path.GetFullPath("~\\temp");
            }
            else
            {
                tempPath = Path.GetFullPath("~/temp");
            }
        }

        [Fact]
        public void Basic()
        {
            var tempFile = new TempFile();

            Assert.False(File.Exists(tempFile.Path));
            Assert.StartsWith(Path.GetTempPath(), tempFile.Path);
            Assert.EndsWith( ".tmp", tempFile.Path);

            File.WriteAllText(tempFile.Path, "Hello World!");

            Assert.True(File.Exists(tempFile.Path));

            tempFile.Dispose();

            Assert.False(File.Exists(tempFile.Path));

            // Make sure that there's no exception if we dispose more than once.

            tempFile.Dispose();
        }

        [Fact]
        public void Unique()
        {
            var tempFile1 = new TempFile();
            var tempFile2 = new TempFile();

            Assert.NotEqual(tempFile1.Path, tempFile2.Path);
        }

        [Fact]
        public void DisposeWhenNone()
        {
            var tempFile = new TempFile();

            // Make sure that there's no exception when there's no file.

            tempFile.Dispose();
        }

        [Fact]
        public void DisposeWhenLocked()
        {
            var tempFile = new TempFile();

            // Make sure that there's no exception when the file 
            // is open.

            using (var stream = new FileStream(tempFile.Path, FileMode.Create, FileAccess.ReadWrite))
            {
                stream.Write(Encoding.UTF8.GetBytes("Hello World!"));

                // Make sure that there's no exception when the file 
                // is open.

                tempFile.Dispose();
            }

            // We can delete it now that the stream is closed.

            File.Delete(tempFile.Path);
        }

        [Fact]
        public void CustomSuffix()
        {
            var tempFile = new TempFile(suffix: ".txt");

            Assert.StartsWith(Path.GetTempPath(), tempFile.Path);
            Assert.EndsWith(".txt", tempFile.Path);
        }

        [Fact]
        public void CustomFolder()
        {
            var tempFile = new TempFile(folder: tempPath);

            Assert.StartsWith(tempPath, tempFile.Path);
            Assert.EndsWith(".tmp", tempFile.Path);
        }

        [Fact]
        public void CustomFolderAndSuffix()
        {
            var tempFile = new TempFile(suffix: ".txt", folder: tempPath);

            Assert.StartsWith(tempPath, tempFile.Path);
            Assert.EndsWith(".txt", tempFile.Path);
        }

        [Fact]
        public void CustomRoot()
        {
            TempFile.Root = tempPath;

            try
            {
                var tempFile = new TempFile();

                Assert.StartsWith(tempPath, tempFile.Path);
                Assert.EndsWith(".tmp", tempFile.Path);
            }
            finally
            {
                TempFile.Root = null;
            }
        }
    }
}
