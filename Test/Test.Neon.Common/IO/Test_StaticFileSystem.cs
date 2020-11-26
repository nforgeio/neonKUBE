//-----------------------------------------------------------------------------
// FILE:	    Test_StaticFileSystem.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_StaticFileSystem
    {
        //---------------------------------------------------------------------
        // Private types

        private class File : StaticFileBase
        {
            public File(string name)
                : base(name)
            {
            }

            public override TextReader OpenReader(Encoding encoding = null)
            {
                throw new NotImplementedException();
            }

            public override Task<TextReader> OpenReaderAsync(Encoding encoding = null)
            {
                throw new NotImplementedException();
            }

            public override Stream OpenStream()
            {
                throw new NotImplementedException();
            }

            public override Task<Stream> OpenStreamAsync()
            {
                throw new NotImplementedException();
            }

            public override byte[] ReadAllBytes()
            {
                throw new NotImplementedException();
            }

            public override Task<byte[]> ReadAllBytesAsync()
            {
                throw new NotImplementedException();
            }

            public override string ReadAllText(Encoding encoding = null)
            {
                throw new NotImplementedException();
            }

            public override Task<string> ReadAllTextAsync(Encoding encoding = null)
            {
                throw new NotImplementedException();
            }
        }

        public class Directory : StaticDirectoryBase
        {
            public Directory(StaticDirectoryBase root, StaticDirectoryBase parent, string name)
                : base(root, parent, name)
            {
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        public Test_StaticFileSystem()
        {
            // Initialize a few static file systems for testing.


        }

        [Fact]
        public void List_Files()
        {
        }

        [Fact]
        public void List_Files_Recursively()
        {
        }

        [Fact]
        public void List_Directories()
        {
        }

        [Fact]
        public void List_Directories_Recursively()
        {
        }
    }
}
