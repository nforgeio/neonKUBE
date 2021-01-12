//-----------------------------------------------------------------------------
// FILE:	    Test_StaticFileSystem.cs
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
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
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
        public Test_StaticFileSystem()
        {
        }

        //---------------------------------------------------------------------
        // Tests that don't filter resource names.

        [Fact]
        public void All_Load()
        {
            // Verify that an unfiltered filesystem has the directories and files that we expect.

            var fs        = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var directory = fs;

            // Directory: /

            Assert.Single(directory.GetDirectories());
            Assert.Contains("TestCommon", directory.GetDirectories().Select(directory => directory.Name));
            Assert.Empty(directory.GetFiles().Select(file => file.Name));

            // Directory: /TestCommon/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "TestCommon")
                .Single();

            Assert.Contains("IORes", directory.GetDirectories().Select(directory => directory.Name));
            Assert.Empty(directory.GetFiles().Select(file => file.Name));

            // Directory: /TestCommon/IORes/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "TestCommon")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "IORes")
                .Single();

            Assert.Single(directory.GetDirectories());
            Assert.Contains("Resources", directory.GetDirectories().Select(directory => directory.Name));
            Assert.Empty(directory.GetFiles());

            // Directory: /TestCommon/IORes/Resources/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "TestCommon")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "IORes")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Resources")
                .Single();

            Assert.Equal(2, directory.GetDirectories().Count());
            Assert.Contains("Folder1", directory.GetDirectories().Select(directory => directory.Name));
            Assert.Contains("Folder2", directory.GetDirectories().Select(directory => directory.Name));

            Assert.Equal(2, directory.GetFiles().Count());
            Assert.Contains("TextFile1.txt", directory.GetFiles().Select(file => file.Name));
            Assert.Contains("TextFile2.txt", directory.GetFiles().Select(file => file.Name));

            // Directory: /TestCommon/IORes/Resources/Folder1/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "TestCommon")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "IORes")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Resources")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Folder1")
                .Single();

            Assert.Single(directory.GetDirectories());
            Assert.Contains("Folder3", directory.GetDirectories().Select(directory => directory.Name));

            Assert.Equal(2, directory.GetFiles().Count());
            Assert.Contains("TextFile3.txt", directory.GetFiles().Select(file => file.Name));
            Assert.Contains("TextFile4.txt", directory.GetFiles().Select(file => file.Name));

            // Directory: /TestCommon/IORes/Resources/Folder1/Folder3/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "TestCommon")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "IORes")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Resources")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Folder1")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Folder3")
                .Single();

            Assert.Empty(directory.GetDirectories());

            Assert.Single(directory.GetFiles());
            Assert.Contains("TextFile5.txt", directory.GetFiles().Select(file => file.Name));

            // Directory: /TestCommon/IORes/Resources/Folder2/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "TestCommon")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "IORes")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Resources")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Folder2")
                .Single();

            Assert.Single(directory.GetDirectories());
            Assert.Contains("Folder4", directory.GetDirectories().Select(directory => directory.Name));

            Assert.Equal(2, directory.GetFiles().Count());
            Assert.Contains("TextFile6.txt", directory.GetFiles().Select(file => file.Name));
            Assert.Contains("TextFile7.txt", directory.GetFiles().Select(file => file.Name));

            // Directory: /TestCommon/IORes/Resources/Folder2/Folder4/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "TestCommon")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "IORes")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Resources")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Folder2")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Folder4")
                .Single();

            Assert.Empty(directory.GetDirectories());

            Assert.Single(directory.GetFiles());
            Assert.Contains("TextFile8.txt", directory.GetFiles().Select(file => file.Name));
        }

        [Fact]
        public void All_List_Files()
        {
            var fs        = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var directory = fs;

            // Directory: /

            Assert.Empty(directory.GetFiles());

            // Directory: /TestCommon/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "TestCommon")
                .Single();

            // Directory: /TestCommon/IORes/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "TestCommon")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "IORes")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Resources")
                .Single();

            var files = directory.GetFiles();

            Assert.Equal(2, files.Count());
            Assert.Contains("TextFile1.txt", files.Select(file => file.Name));
            Assert.Contains("TextFile2.txt", files.Select(file => file.Name));

            // Specific file.

            files = directory.GetFiles("TextFile1.txt");

            Assert.Single(files);
            Assert.Contains("TextFile1.txt", files.Select(file => file.Name));

            // Pattern match.

            files = directory.GetFiles("*1.*");

            Assert.Single(files);
            Assert.Contains("TextFile1.txt", files.Select(file => file.Name));
        }

        [Fact]
        public void All_List_Files_Recursively()
        {
            var fs = Assembly.GetExecutingAssembly().GetResourceFileSystem();

            // List from root.

            var files = fs.GetFiles(options: SearchOption.AllDirectories);

            Assert.Equal(8, files.Count());
            Assert.Contains("/TestCommon/IORes/Resources/TextFile1.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/TextFile2.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/TextFile3.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/TextFile4.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/Folder3/TextFile5.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder2/TextFile6.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder2/TextFile7.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder2/Folder4/TextFile8.txt", files.Select(file => file.Path));

            // Pattern match

            files = fs.GetFiles(searchPattern: "TextFile3.txt", options: SearchOption.AllDirectories);

            Assert.Single(files);
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/TextFile3.txt", files.Select(file => file.Path));

            files = fs.GetFiles(searchPattern: "*.txt", options: SearchOption.AllDirectories);

            Assert.Equal(8, files.Count());
            Assert.Contains("/TestCommon/IORes/Resources/TextFile1.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/TextFile2.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/TextFile3.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/TextFile4.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/Folder3/TextFile5.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder2/TextFile6.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder2/TextFile7.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder2/Folder4/TextFile8.txt", files.Select(file => file.Path));

            // List from a subdirectory.

            var directory = fs.GetDirectory("/TestCommon/IORes/Resources/Folder1");
            
            files = directory.GetFiles(options: SearchOption.AllDirectories);

            Assert.Equal(3, files.Count());
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/TextFile3.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/TextFile4.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/Folder3/TextFile5.txt", files.Select(file => file.Path));

            // Extra test to ensure that an extra trailing "/" in a directory path is ignored.

            directory = fs.GetDirectory("/TestCommon/IORes/Resources/Folder1/");
            
            files = directory.GetFiles(options: SearchOption.AllDirectories);

            Assert.Equal(3, files.Count());
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/TextFile3.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/TextFile4.txt", files.Select(file => file.Path));
            Assert.Contains("/TestCommon/IORes/Resources/Folder1/Folder3/TextFile5.txt", files.Select(file => file.Path));
        }

        [Fact]
        public void All_List_Directories()
        {
            var fs        = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var directory = fs;

            // Directory: /

            Assert.Single(directory.GetDirectories("TestCommon"));
            Assert.Empty(directory.GetDirectories("NOT-FOUND"));

            // Directory: /TestCommon/

            directory = fs.GetDirectories("TestCommon").Single();

            Assert.Single(directory.GetDirectories("IORes"));
            Assert.Empty(directory.GetDirectories("NOT-FOUND"));

            // Directory: /TestCommon/IORes/

            directory = fs.GetDirectories("TestCommon").Single();
            directory = directory.GetDirectories("IORes").Single();

            Assert.Single(directory.GetDirectories("Resources"));
            Assert.Empty(directory.GetDirectories("NOT-FOUND"));

            // Directory: /TestCommon/IORes/Resources/

            directory = fs.GetDirectories("TestCommon").Single();
            directory = directory.GetDirectories("IORes").Single();
            directory = directory.GetDirectories("Resources").Single();

            Assert.Single(directory.GetDirectories("Folder1"));
            Assert.Single(directory.GetDirectories("Folder2"));
            Assert.Empty(directory.GetDirectories("NOT-FOUND"));

            // Directory: /TestCommon/IORes/Resources/Folder1/

            directory = fs.GetDirectories("TestCommon").Single();
            directory = directory.GetDirectories("IORes").Single();
            directory = directory.GetDirectories("Resources").Single();
            directory = directory.GetDirectories("Folder1").Single();

            Assert.Single(directory.GetDirectories("Folder3"));
            Assert.Empty(directory.GetDirectories("NOT-FOUND"));

            // Directory: /TestCommon/IORes/Resources/Folder2/

            directory = fs.GetDirectories("TestCommon").Single();
            directory = directory.GetDirectories("IORes").Single();
            directory = directory.GetDirectories("Resources").Single();
            directory = directory.GetDirectories("Folder2").Single();

            Assert.Single(directory.GetDirectories("Folder4"));
            Assert.Empty(directory.GetDirectories("NOT-FOUND"));
        }

        [Fact]
        public void All_List_Directories_Recursively()
        {
            var fs          = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var directories = fs.GetDirectories(searchPattern: null, options: SearchOption.AllDirectories);

            Assert.Equal(7, directories.Count());
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder1"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder1/Folder3"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder2"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder2/Folder4"));
        }

        [Fact]
        public void All_List_Directories_Recursively_WithFilter()
        {
            // Filter by: *

            var fs          = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var directories = fs.GetDirectories("*", SearchOption.AllDirectories);

            Assert.Equal(7, directories.Count());
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder1"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder1/Folder3"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder2"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder2/Folder4"));

            // Filter by: *.*
            
            directories = fs.GetDirectories("*.*", SearchOption.AllDirectories);

            Assert.Equal(7, directories.Count());
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder1"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder1/Folder3"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder2"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder2/Folder4"));

            // Filter by: F*

            directories = fs.GetDirectories("F*", SearchOption.AllDirectories);

            Assert.Equal(4, directories.Count());
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder1"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder1/Folder3"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder2"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder2/Folder4"));

            // Filter by: Folder?

            directories = fs.GetDirectories("Folder?", SearchOption.AllDirectories);

            Assert.Equal(4, directories.Count());
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder1"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder1/Folder3"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder2"));
            Assert.Single(directories.Where(directory => directory.Path == "/TestCommon/IORes/Resources/Folder2/Folder4"));
        }

        [Fact]
        public void All_GetFile()
        {
            var fs = Assembly.GetExecutingAssembly().GetResourceFileSystem();

            // From the root directory.

            Assert.Equal("/TestCommon/IORes/Resources/TextFile1.txt", fs.GetFile("/TestCommon/IORes/Resources/TextFile1.txt").Path);
            Assert.Equal("/TestCommon/IORes/Resources/TextFile2.txt", fs.GetFile("/TestCommon/IORes/Resources/TextFile2.txt").Path);

            Assert.Equal("/TestCommon/IORes/Resources/Folder1/TextFile3.txt", fs.GetFile("/TestCommon/IORes/Resources/Folder1/TextFile3.txt").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder1/TextFile4.txt", fs.GetFile("/TestCommon/IORes/Resources/Folder1/TextFile4.txt").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder1/Folder3/TextFile5.txt", fs.GetFile("/TestCommon/IORes/Resources/Folder1/Folder3/TextFile5.txt").Path);

            Assert.Equal("/TestCommon/IORes/Resources/Folder2/TextFile6.txt", fs.GetFile("/TestCommon/IORes/Resources/Folder2/TextFile6.txt").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder2/TextFile7.txt", fs.GetFile("/TestCommon/IORes/Resources/Folder2/TextFile7.txt").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder2/Folder4/TextFile8.txt", fs.GetFile("/TestCommon/IORes/Resources/Folder2/Folder4/TextFile8.txt").Path);

            Assert.Throws<FileNotFoundException>(() => fs.GetFile("/TestCommon/IORes/Resources/Folder2/NOT-FOUND.txt"));

            // From a subdirectory.

            var directory = fs.GetDirectory("/TestCommon/IORes/Resources/Folder2/Folder4");

            Assert.Equal("/TestCommon/IORes/Resources/TextFile1.txt", directory.GetFile("/TestCommon/IORes/Resources/TextFile1.txt").Path);
            Assert.Equal("/TestCommon/IORes/Resources/TextFile2.txt", directory.GetFile("/TestCommon/IORes/Resources/TextFile2.txt").Path);

            Assert.Equal("/TestCommon/IORes/Resources/Folder1/TextFile3.txt", directory.GetFile("/TestCommon/IORes/Resources/Folder1/TextFile3.txt").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder1/TextFile4.txt", directory.GetFile("/TestCommon/IORes/Resources/Folder1/TextFile4.txt").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder1/Folder3/TextFile5.txt", fs.GetFile("/TestCommon/IORes/Resources/Folder1/Folder3/TextFile5.txt").Path);

            Assert.Equal("/TestCommon/IORes/Resources/Folder2/TextFile6.txt", directory.GetFile("/TestCommon/IORes/Resources/Folder2/TextFile6.txt").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder2/TextFile7.txt", directory.GetFile("/TestCommon/IORes/Resources/Folder2/TextFile7.txt").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder2/Folder4/TextFile8.txt", directory.GetFile("/TestCommon/IORes/Resources/Folder2/Folder4/TextFile8.txt").Path);

            // Relative path.

            Assert.Equal("/TestCommon/IORes/Resources/Folder2/Folder4/TextFile8.txt", directory.GetFile("TextFile8.txt").Path);

            // Not found.

            Assert.Throws<FileNotFoundException>(() => directory.GetFile("/TestCommon/IORes/Resources/Folder2/NOT-FOUND.txt"));
        }

        [Fact]
        public void All_GetDirectory()
        {
            var fs = Assembly.GetExecutingAssembly().GetResourceFileSystem();

            // From the root directory.

            Assert.Equal("/TestCommon", fs.GetDirectory("/TestCommon").Path);
            Assert.Equal("/TestCommon/IORes", fs.GetDirectory("/TestCommon/IORes").Path);
            Assert.Equal("/TestCommon/IORes/Resources", fs.GetDirectory("/TestCommon/IORes/Resources").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder1", fs.GetDirectory("/TestCommon/IORes/Resources/Folder1").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder1/Folder3", fs.GetDirectory("/TestCommon/IORes/Resources/Folder1/Folder3").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder2", fs.GetDirectory("/TestCommon/IORes/Resources/Folder2").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder2/Folder4", fs.GetDirectory("/TestCommon/IORes/Resources/Folder2/Folder4").Path);

            Assert.Throws<FileNotFoundException>(() => fs.GetDirectory("/TestCommon/IORes/Resources/NOT-FOUND.txt"));

            // From a subdirectory.

            var directory = fs.GetDirectory("/TestCommon/IORes/Resources/Folder2/Folder4");

            Assert.Equal("/TestCommon", directory.GetDirectory("/TestCommon").Path);
            Assert.Equal("/TestCommon/IORes", directory.GetDirectory("/TestCommon/IORes").Path);
            Assert.Equal("/TestCommon/IORes/Resources", directory.GetDirectory("/TestCommon/IORes/Resources").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder1", directory.GetDirectory("/TestCommon/IORes/Resources/Folder1").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder1/Folder3", directory.GetDirectory("/TestCommon/IORes/Resources/Folder1/Folder3").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder2", directory.GetDirectory("/TestCommon/IORes/Resources/Folder2").Path);
            Assert.Equal("/TestCommon/IORes/Resources/Folder2/Folder4", directory.GetDirectory("/TestCommon/IORes/Resources/Folder2/Folder4").Path);

            // Relative path.

            directory = fs.GetDirectory("/TestCommon/IORes/Resources/Folder2");

            Assert.Equal("/TestCommon/IORes/Resources/Folder2/Folder4", directory.GetDirectory("Folder4").Path);

            // Not found.

            Assert.Throws<FileNotFoundException>(() => fs.GetDirectory("/TestCommon/IORes/Resources/NOT-FOUND.txt"));
        }

        //---------------------------------------------------------------------
        // Tests that use a prefix to extract only some of the resopurces.

        [Fact]
        public void Partial_Load()
        {
            // Verify that an filtered filesystem has the directories and files that we expect.

            var fs        = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");
            var directory = fs;

            // Directory: /

            Assert.Equal(2, fs.GetDirectories().Count());
            Assert.Contains("Folder1", directory.GetDirectories().Select(directory => directory.Name));
            Assert.Contains("Folder2", directory.GetDirectories().Select(directory => directory.Name));

            Assert.Equal(2, directory.GetFiles().Count());
            Assert.Contains("TextFile1.txt", directory.GetFiles().Select(file => file.Name));
            Assert.Contains("TextFile2.txt", directory.GetFiles().Select(file => file.Name));

            // Directory: /Folder1/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "Folder1")
                .Single();

            Assert.Single(directory.GetDirectories());
            Assert.Contains("Folder3", directory.GetDirectories().Select(directory => directory.Name));

            Assert.Equal(2, directory.GetFiles().Count());
            Assert.Contains("TextFile3.txt", directory.GetFiles().Select(file => file.Name));
            Assert.Contains("TextFile4.txt", directory.GetFiles().Select(file => file.Name));

            // Directory: /Folder3/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "Folder1")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Folder3")
                .Single();

            Assert.Empty(directory.GetDirectories());

            Assert.Single(directory.GetFiles());
            Assert.Contains("TextFile5.txt", directory.GetFiles().Select(file => file.Name));

            // Directory: /Folder2/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "Folder2")
                .Single();

            Assert.Single(directory.GetDirectories());
            Assert.Contains("Folder4", directory.GetDirectories().Select(directory => directory.Name));

            Assert.Equal(2, directory.GetFiles().Count());
            Assert.Contains("TextFile6.txt", directory.GetFiles().Select(file => file.Name));
            Assert.Contains("TextFile7.txt", directory.GetFiles().Select(file => file.Name));

            // Directory: /Folder4/

            directory = fs.GetDirectories()
                .Where(directory => directory.Name == "Folder2")
                .Single();

            directory = directory.GetDirectories()
                .Where(directory => directory.Name == "Folder4")
                .Single();

            Assert.Empty(directory.GetDirectories());

            Assert.Single(directory.GetFiles());
            Assert.Contains("TextFile8.txt", directory.GetFiles().Select(file => file.Name));
        }

        [Fact]
        public void Partial_List_Files()
        {
            var fs        = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");
            var directory = fs;

            var files = directory.GetFiles();

            Assert.Equal(2, files.Count());
            Assert.Contains("TextFile1.txt", files.Select(file => file.Name));
            Assert.Contains("TextFile2.txt", files.Select(file => file.Name));

            // Specific file.

            files = directory.GetFiles("TextFile1.txt");

            Assert.Single(files);
            Assert.Contains("TextFile1.txt", files.Select(file => file.Name));

            // Pattern match.

            files = directory.GetFiles("*1.*");

            Assert.Single(files);
            Assert.Contains("TextFile1.txt", files.Select(file => file.Name));
        }

        [Fact]
        public void Partial_List_Files_Recursively()
        {
            var fs = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");

            // List from root.

            var files = fs.GetFiles(options: SearchOption.AllDirectories);

            Assert.Equal(8, files.Count());
            Assert.Contains("/TextFile1.txt", files.Select(file => file.Path));
            Assert.Contains("/TextFile2.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/TextFile3.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/TextFile4.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/Folder3/TextFile5.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder2/TextFile6.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder2/TextFile7.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder2/Folder4/TextFile8.txt", files.Select(file => file.Path));

            // Pattern match

            files = fs.GetFiles(searchPattern: "TextFile3.txt", options: SearchOption.AllDirectories);

            Assert.Single(files);
            Assert.Contains("/Folder1/TextFile3.txt", files.Select(file => file.Path));

            files = fs.GetFiles(searchPattern: "*.txt", options: SearchOption.AllDirectories);

            Assert.Equal(8, files.Count());
            Assert.Contains("/TextFile1.txt", files.Select(file => file.Path));
            Assert.Contains("/TextFile2.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/TextFile3.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/TextFile4.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/Folder3/TextFile5.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder2/TextFile6.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder2/TextFile7.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder2/Folder4/TextFile8.txt", files.Select(file => file.Path));

            // List from a subdirectory.

            var directory = fs.GetDirectory("/Folder1");
            
            files = directory.GetFiles(options: SearchOption.AllDirectories);

            Assert.Equal(3, files.Count());
            Assert.Contains("/Folder1/TextFile3.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/TextFile4.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/Folder3/TextFile5.txt", files.Select(file => file.Path));

            // Extra test to ensure that an extra trailing "/" in a directory path is ignored.

            directory = fs.GetDirectory("/Folder1/");
            
            files = directory.GetFiles(options: SearchOption.AllDirectories);

            Assert.Equal(3, files.Count());
            Assert.Contains("/Folder1/TextFile3.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/TextFile4.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/Folder3/TextFile5.txt", files.Select(file => file.Path));
        }

        [Fact]
        public void Partial_List_Directories()
        {
            var fs        = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");
            var directory = fs;

            // Directory: /Folder1/

            directory = fs.GetDirectories("Folder1").Single();

            Assert.Single(directory.GetDirectories("Folder3"));
            Assert.Empty(directory.GetDirectories("NOT-FOUND"));

            // Directory: /Folder2/

            directory = fs.GetDirectories("Folder2").Single();

            Assert.Single(directory.GetDirectories("Folder4"));
            Assert.Empty(directory.GetDirectories("NOT-FOUND"));
        }

        [Fact]
        public void Partial_List_Directories_Recursively()
        {
            var fs = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");

            // List from root.

            var files = fs.GetFiles(options: SearchOption.AllDirectories);

            Assert.Equal(8, files.Count());
            Assert.Contains("/TextFile1.txt", files.Select(file => file.Path));
            Assert.Contains("/TextFile2.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/TextFile3.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/TextFile4.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/Folder3/TextFile5.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder2/TextFile6.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder2/TextFile7.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder2/Folder4/TextFile8.txt", files.Select(file => file.Path));

            // Pattern match

            files = fs.GetFiles(searchPattern: "TextFile3.txt", options: SearchOption.AllDirectories);

            Assert.Single(files);
            Assert.Contains("/Folder1/TextFile3.txt", files.Select(file => file.Path));

            files = fs.GetFiles(searchPattern: "*.txt", options: SearchOption.AllDirectories);

            Assert.Equal(8, files.Count());
            Assert.Contains("/TextFile1.txt", files.Select(file => file.Path));
            Assert.Contains("/TextFile2.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/TextFile3.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/TextFile4.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/Folder3/TextFile5.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder2/TextFile6.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder2/TextFile7.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder2/Folder4/TextFile8.txt", files.Select(file => file.Path));

            // List from a subdirectory.

            var directory = fs.GetDirectory("/Folder1");
            
            files = directory.GetFiles(options: SearchOption.AllDirectories);

            Assert.Equal(3, files.Count());
            Assert.Contains("/Folder1/TextFile3.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/TextFile4.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/Folder3/TextFile5.txt", files.Select(file => file.Path));

            // Extra test to ensure that an extra trailing "/" in a directory path is ignored.

            directory = fs.GetDirectory("/Folder1/");
            
            files = directory.GetFiles(options: SearchOption.AllDirectories);

            Assert.Equal(3, files.Count());
            Assert.Contains("/Folder1/TextFile3.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/TextFile4.txt", files.Select(file => file.Path));
            Assert.Contains("/Folder1/Folder3/TextFile5.txt", files.Select(file => file.Path));
        }

        [Fact]
        public void Partial_GetFile()
        {
            var fs = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");

            // From the root directory.

            Assert.Equal("/TextFile1.txt", fs.GetFile("/TextFile1.txt").Path);
            Assert.Equal("/TextFile2.txt", fs.GetFile("/TextFile2.txt").Path);

            Assert.Equal("/Folder1/TextFile3.txt", fs.GetFile("/Folder1/TextFile3.txt").Path);
            Assert.Equal("/Folder1/TextFile4.txt", fs.GetFile("/Folder1/TextFile4.txt").Path);
            Assert.Equal("/Folder1/Folder3/TextFile5.txt", fs.GetFile("/Folder1/Folder3/TextFile5.txt").Path);

            Assert.Equal("/Folder2/TextFile6.txt", fs.GetFile("/Folder2/TextFile6.txt").Path);
            Assert.Equal("/Folder2/TextFile7.txt", fs.GetFile("/Folder2/TextFile7.txt").Path);
            Assert.Equal("/Folder2/Folder4/TextFile8.txt", fs.GetFile("/Folder2/Folder4/TextFile8.txt").Path);

            Assert.Throws<FileNotFoundException>(() => fs.GetFile("/Folder2/NOT-FOUND.txt"));

            // From a subdirectory.

            var directory = fs.GetDirectory("/Folder2/Folder4");

            Assert.Equal("/TextFile1.txt", directory.GetFile("/TextFile1.txt").Path);
            Assert.Equal("/TextFile2.txt", directory.GetFile("/TextFile2.txt").Path);

            Assert.Equal("/Folder1/TextFile3.txt", directory.GetFile("/Folder1/TextFile3.txt").Path);
            Assert.Equal("/Folder1/TextFile4.txt", directory.GetFile("/Folder1/TextFile4.txt").Path);
            Assert.Equal("/Folder1/Folder3/TextFile5.txt", fs.GetFile("/Folder1/Folder3/TextFile5.txt").Path);

            Assert.Equal("/Folder2/TextFile6.txt", directory.GetFile("/Folder2/TextFile6.txt").Path);
            Assert.Equal("/Folder2/TextFile7.txt", directory.GetFile("/Folder2/TextFile7.txt").Path);
            Assert.Equal("/Folder2/Folder4/TextFile8.txt", directory.GetFile("/Folder2/Folder4/TextFile8.txt").Path);

            // Relative path.

            Assert.Equal("/Folder2/Folder4/TextFile8.txt", directory.GetFile("TextFile8.txt").Path);

            // Not found.

            Assert.Throws<FileNotFoundException>(() => directory.GetFile("/Folder2/NOT-FOUND.txt"));
        }

        [Fact]
        public void Partial_GetDirectory()
        {
            var fs = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");

            // From the root directory.

            Assert.Equal("", fs.GetDirectory("/").Path);
            Assert.Equal("/Folder1", fs.GetDirectory("/Folder1").Path);
            Assert.Equal("/Folder1/Folder3", fs.GetDirectory("/Folder1/Folder3").Path);
            Assert.Equal("/Folder2", fs.GetDirectory("/Folder2").Path);
            Assert.Equal("/Folder2/Folder4", fs.GetDirectory("/Folder2/Folder4").Path);

            Assert.Throws<FileNotFoundException>(() => fs.GetDirectory("//NOT-FOUND.txt"));

            // From a subdirectory.

            var directory = fs.GetDirectory("/Folder2/Folder4");

            Assert.Equal("/Folder1", directory.GetDirectory("/Folder1").Path);
            Assert.Equal("/Folder1/Folder3", directory.GetDirectory("/Folder1/Folder3").Path);
            Assert.Equal("/Folder2", directory.GetDirectory("/Folder2").Path);
            Assert.Equal("/Folder2/Folder4", directory.GetDirectory("/Folder2/Folder4").Path);

            // Relative path.

            directory = fs.GetDirectory("/Folder2");

            Assert.Equal("/Folder2/Folder4", directory.GetDirectory("Folder4").Path);

            // Not found.

            Assert.Throws<FileNotFoundException>(() => fs.GetDirectory("//NOT-FOUND.txt"));
        }

        [Fact]
        public void ZipToStream()
        {
            var fs = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");

            // Verify that we can zip all embedded resources to a stream.

            using (var stream = new MemoryStream())
            {
                fs.Zip(stream, searchOptions: SearchOption.AllDirectories);

                Assert.True(stream.Length > 0);

                using (var tempFolder = new TempFolder())
                {
                    var fastZip     = new FastZip();
                    var zipPath     = Path.Combine(tempFolder.Path, "test.zip");
                    var unzipFolder = Path.Combine(tempFolder.Path, "unzipped");

                    File.WriteAllBytes(zipPath, stream.ToArray());
                    fastZip.ExtractZip(zipPath, unzipFolder, null);

                    Assert.True(File.Exists(Path.Combine(unzipFolder, "TextFile1.txt")));
                    Assert.True(File.Exists(Path.Combine(unzipFolder, "TextFile2.txt")));

                    Assert.True(File.Exists(Path.Combine(unzipFolder, "Folder1", "TextFile3.txt")));
                    Assert.True(File.Exists(Path.Combine(unzipFolder, "Folder1", "TextFile4.txt")));
                    Assert.True(File.Exists(Path.Combine(unzipFolder, "Folder1", "Folder3", "TextFile5.txt")));

                    Assert.True(File.Exists(Path.Combine(unzipFolder, "Folder2", "TextFile6.txt")));
                    Assert.True(File.Exists(Path.Combine(unzipFolder, "Folder2", "TextFile7.txt")));
                    Assert.True(File.Exists(Path.Combine(unzipFolder, "Folder2", "Folder4", "TextFile8.txt")));
                }
            }

            // Verify that we can zip a single file from the root directory.

            using (var stream = new MemoryStream())
            {
                fs.Zip(stream, "TextFile1.txt", searchOptions: SearchOption.TopDirectoryOnly);

                Assert.True(stream.Length > 0);

                using (var tempFolder = new TempFolder())
                {
                    var fastZip     = new FastZip();
                    var zipPath     = Path.Combine(tempFolder.Path, "test.zip");
                    var unzipFolder = Path.Combine(tempFolder.Path, "unzipped");

                    File.WriteAllBytes(zipPath, stream.ToArray());
                    fastZip.ExtractZip(zipPath, unzipFolder, null);

                    Assert.True(File.Exists(Path.Combine(unzipFolder, "TextFile1.txt")));
                    Assert.False(File.Exists(Path.Combine(unzipFolder, "TextFile2.txt")));

                    Assert.False(File.Exists(Path.Combine(unzipFolder, "Folder1", "TextFile3.txt")));
                    Assert.False(File.Exists(Path.Combine(unzipFolder, "Folder1", "TextFile4.txt")));
                    Assert.False(File.Exists(Path.Combine(unzipFolder, "Folder1", "Folder3", "TextFile5.txt")));

                    Assert.False(File.Exists(Path.Combine(unzipFolder, "Folder2", "TextFile6.txt")));
                    Assert.False(File.Exists(Path.Combine(unzipFolder, "Folder2", "TextFile7.txt")));
                    Assert.False(File.Exists(Path.Combine(unzipFolder, "Folder2", "Folder4", "TextFile8.txt")));
                }
            }
        }

        [Fact]
        public void ZipToFile()
        {
            var fs = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");

            // Verify that we can zip all embedded resources to a file.

            using (var tempFolder = new TempFolder())
            {
                var fastZip     = new FastZip();
                var zipPath     = Path.Combine(tempFolder.Path, "test.zip");
                var unzipFolder = Path.Combine(tempFolder.Path, "unzipped");

                fs.Zip(zipPath, searchOptions: SearchOption.AllDirectories);
                fastZip.ExtractZip(zipPath, unzipFolder, null);

                Assert.True(File.Exists(Path.Combine(unzipFolder, "TextFile1.txt")));
                Assert.True(File.Exists(Path.Combine(unzipFolder, "TextFile2.txt")));

                Assert.True(File.Exists(Path.Combine(unzipFolder, "Folder1", "TextFile3.txt")));
                Assert.True(File.Exists(Path.Combine(unzipFolder, "Folder1", "TextFile4.txt")));
                Assert.True(File.Exists(Path.Combine(unzipFolder, "Folder1", "Folder3", "TextFile5.txt")));

                Assert.True(File.Exists(Path.Combine(unzipFolder, "Folder2", "TextFile6.txt")));
                Assert.True(File.Exists(Path.Combine(unzipFolder, "Folder2", "TextFile7.txt")));
                Assert.True(File.Exists(Path.Combine(unzipFolder, "Folder2", "Folder4", "TextFile8.txt")));
            }

            // Verify that we can zip a single file from the root directory.

            using (var tempFolder = new TempFolder())
            {
                var fastZip     = new FastZip();
                var zipPath     = Path.Combine(tempFolder.Path, "test.zip");
                var unzipFolder = Path.Combine(tempFolder.Path, "unzipped");

                fs.Zip(zipPath, searchPattern: "TextFile1.txt", searchOptions: SearchOption.TopDirectoryOnly);
                fastZip.ExtractZip(zipPath, unzipFolder, null);

                Assert.True(File.Exists(Path.Combine(unzipFolder, "TextFile1.txt")));
                Assert.False(File.Exists(Path.Combine(unzipFolder, "TextFile2.txt")));

                Assert.False(File.Exists(Path.Combine(unzipFolder, "Folder1", "TextFile3.txt")));
                Assert.False(File.Exists(Path.Combine(unzipFolder, "Folder1", "TextFile4.txt")));
                Assert.False(File.Exists(Path.Combine(unzipFolder, "Folder1", "Folder3", "TextFile5.txt")));

                Assert.False(File.Exists(Path.Combine(unzipFolder, "Folder2", "TextFile6.txt")));
                Assert.False(File.Exists(Path.Combine(unzipFolder, "Folder2", "TextFile7.txt")));
                Assert.False(File.Exists(Path.Combine(unzipFolder, "Folder2", "Folder4", "TextFile8.txt")));
            }
        }

        [Fact]
        public void ZipToFile_WithLinuxLineEndings()
        {
            var fs = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");

            // Verify that we can zip all text files, converting any Windows style CRLF
            // line endings to Linux LF.

            using (var tempFolder = new TempFolder())
            {
                var fastZip     = new FastZip();
                var zipPath     = Path.Combine(tempFolder.Path, "test.zip");
                var unzipFolder = Path.Combine(tempFolder.Path, "unzipped");

                fs.Zip(zipPath, searchOptions: SearchOption.AllDirectories, zipOptions: StaticZipOptions.LinuxLineEndings);
                fastZip.ExtractZip(zipPath, unzipFolder, null);

                Assert.True(!File.ReadAllText(Path.Combine(unzipFolder, "TextFile1.txt")).Contains("\r\n"));
                Assert.True(!File.ReadAllText(Path.Combine(unzipFolder, "TextFile2.txt")).Contains("\r\n"));

                Assert.True(!File.ReadAllText(Path.Combine(unzipFolder, "Folder1", "TextFile3.txt")).Contains("\r\n"));
                Assert.True(!File.ReadAllText(Path.Combine(unzipFolder, "Folder1", "TextFile4.txt")).Contains("\r\n"));
                Assert.True(!File.ReadAllText(Path.Combine(unzipFolder, "Folder1", "Folder3", "TextFile5.txt")).Contains("\r\n"));

                Assert.True(!File.ReadAllText(Path.Combine(unzipFolder, "Folder2", "TextFile6.txt")).Contains("\r\n"));
                Assert.True(!File.ReadAllText(Path.Combine(unzipFolder, "Folder2", "TextFile7.txt")).Contains("\r\n"));
                Assert.True(!File.ReadAllText(Path.Combine(unzipFolder, "Folder2", "Folder4", "TextFile8.txt")).Contains("\r\n"));
            }
        }

        //---------------------------------------------------------------------
        // Resource file read tests for non-filtered resources.

        [Fact]
        public void All_ReadAllText()
        {
            var fs   = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var file = fs.GetFile("/TestCommon/IORes/Resources/TextFile1.txt");

            Assert.Equal(
@"TextFile1.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                file.ReadAllText());
        }

        [Fact]
        public async Task All_ReadAllTextAsync()
        {
            var fs   = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var file = fs.GetFile("/TestCommon/IORes/Resources/TextFile2.txt");

            Assert.Equal(
@"TextFile2.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                await file.ReadAllTextAsync());
        }

        [Fact]
        public void All_ReadAllBytes()
        {
            var fs   = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var file = fs.GetFile("/TestCommon/IORes/Resources/Folder1/TextFile3.txt");

            Assert.Equal(
@"TextFile3.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                Encoding.UTF8.GetString(file.ReadAllBytes()));
        }

        [Fact]
        public async Task All_ReadAllBytesAsync()
        {
            var fs   = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var file = fs.GetFile("/TestCommon/IORes/Resources/Folder1/TextFile4.txt");

            Assert.Equal(
@"TextFile4.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                Encoding.UTF8.GetString(await file.ReadAllBytesAsync()));
        }

        [Fact]
        public void All_OpenReader()
        {
            var fs   = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var file = fs.GetFile("/TestCommon/IORes/Resources/Folder1/Folder3/TextFile5.txt");

            using (var reader = file.OpenReader())
            {
                Assert.Equal(
@"TextFile5.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                reader.ReadToEnd());                
            }
        }

        [Fact]
        public async Task All_OpenReaderAsync()
        {
            var fs     = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var file   = fs.GetFile("/TestCommon/IORes/Resources/Folder2/Folder4/TextFile8.txt");
            var reader = await file.OpenReaderAsync();

            using (reader)
            {
                Assert.Equal(
@"TextFile8.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                await reader.ReadToEndAsync());
            }
        }

        [Fact]
        public void All_OpenStream()
        {
            var fs   = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var file = fs.GetFile("/TestCommon/IORes/Resources/Folder1/Folder3/TextFile5.txt");

            using (var stream = file.OpenStream())
            {
                Assert.Equal(
@"TextFile5.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                Encoding.UTF8.GetString(stream.ReadToEnd()));
            }
        }

        [Fact]
        public async Task All_OpenStreamAsync()
        {
            var fs     = Assembly.GetExecutingAssembly().GetResourceFileSystem();
            var file   = fs.GetFile("/TestCommon/IORes/Resources/Folder2/Folder4/TextFile8.txt");
            var stream = await file.OpenStreamAsync();

            using (stream)
            {
                Assert.Equal(
@"TextFile8.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                Encoding.UTF8.GetString(await stream.ReadToEndAsync()));
            }
        }

        //---------------------------------------------------------------------
        // Resource file read tests for filtered resources.

        [Fact]
        public void Partial_ReadAllText()
        {
            var fs   = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");
            var file = fs.GetFile("/TextFile1.txt");

            Assert.Equal(
@"TextFile1.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                file.ReadAllText());
        }

        [Fact]
        public async Task Partial_ReadAllTextAsync()
        {
            var fs   = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");
            var file = fs.GetFile("/TextFile2.txt");

            Assert.Equal(
@"TextFile2.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                await file.ReadAllTextAsync());
        }

        [Fact]
        public void Partial_ReadAllBytes()
        {
            var fs   = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");
            var file = fs.GetFile("/Folder1/TextFile3.txt");

            Assert.Equal(
@"TextFile3.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                Encoding.UTF8.GetString(file.ReadAllBytes()));
        }

        [Fact]
        public async Task Partial_ReadAllBytesAsync()
        {
            var fs   = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");
            var file = fs.GetFile("/Folder1/TextFile4.txt");

            Assert.Equal(
@"TextFile4.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                Encoding.UTF8.GetString(await file.ReadAllBytesAsync()));
        }

        [Fact]
        public void Partial_OpenReader()
        {
            var fs   = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");
            var file = fs.GetFile("/Folder1/Folder3/TextFile5.txt");

            using (var reader = file.OpenReader())
            {
                Assert.Equal(
@"TextFile5.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                reader.ReadToEnd());                
            }
        }

        [Fact]
        public async Task Partial_OpenReaderAsync()
        {
            var fs     = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");
            var file   = fs.GetFile("/Folder2/Folder4/TextFile8.txt");
            var reader = await file.OpenReaderAsync();

            using (reader)
            {
                Assert.Equal(
@"TextFile8.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                await reader.ReadToEndAsync());
            }
        }

        [Fact]
        public void Partial_OpenStream()
        {
            var fs   = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");
            var file = fs.GetFile("/Folder1/Folder3/TextFile5.txt");

            using (var stream = file.OpenStream())
            {
                Assert.Equal(
@"TextFile5.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                Encoding.UTF8.GetString(stream.ReadToEnd()));
            }
        }

        [Fact]
        public async Task Partial_OpenStreamAsync()
        {
            var fs     = Assembly.GetExecutingAssembly().GetResourceFileSystem("TestCommon.IORes.Resources");
            var file   = fs.GetFile("/Folder2/Folder4/TextFile8.txt");
            var stream = await file.OpenStreamAsync();

            using (stream)
            {
                Assert.Equal(
@"TextFile8.txt:
Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Line 7
Line 8
Line 9
",
                Encoding.UTF8.GetString(await stream.ReadToEndAsync()));
            }
        }
    }
}
