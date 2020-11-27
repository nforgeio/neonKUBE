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
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    // IMPLEMENTATION NOTE:
    // --------------------
    // We're going to combine testing of the [StaticDirectoryBase] and [StaticFileBase]
    // together with the [Assembly.GetResourceFileSystem()] extension method and related
    // internal classes.
    //
    // This will kill two birds with one stone and is an honest test anyway.  The resource
    // file system will be rooted at [Test.Neon.Common/Resources] and the virtual file
    // structure should look like this:
    // 
    //      /
    //          TextFile1.txt
    //          TextFile2.txt
    //
    //          Folder1/
    //              TextFile3.txt
    //              TextFile4.txt
    //
    //              Folder3/
    //                  TextFile5.txt
    //
    //          Folder2/
    //              TextFile6.txt
    //              TextFile7.txt
    //
    //              Folder4/
    //                  TextFile8.txt
    //
    // The text files will each have 10 lines of UTF-8 text like:
    //
    //      TextFile#.txt:
    //      Line 1
    //      Line 2
    //      Line 3
    //      Line 4
    //      Line 5
    //      Line 6
    //      Line 7
    //      Line 8
    //      Line 9
    //
    // When "#" will match the number in the file's name.

    // $todo(jefflill): I'm only testing UTF-8 encoding at this time.

    public class Test_StaticFileSystem
    {
        private IStaticDirectory    fsAll;          // This file system is rooted at: /
        private IStaticDirectory    fsResources;    // This file system is rooted at: /Resources

        public Test_StaticFileSystem()
        {
            fsAll       = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            fsResources = Assembly.GetExecutingAssembly().GetResourceFileSystem("/Resources");
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
