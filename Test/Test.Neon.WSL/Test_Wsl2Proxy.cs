//-----------------------------------------------------------------------------
// FILE:	    Test_Wsl2Proxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by neonFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with neonFORGE, LLC.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Net;
using Neon.WSL;
using Neon.Xunit;

using Xunit;

namespace TestWSL
{
    [Trait(TestTrait.Category, TestArea.NeonWSL)]
    public class Test_Wsl2Proxy
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Test_Wsl2Proxy()
        {
            // Start each test case without an existing test distribution.

            TestHelper.RemoveTestDistro();
        }

        [Fact]
        public async Task ImportExport()
        {
            // Verify that we can import and export distributions.

            var imagePath = await TestHelper.GetTestImageAsync();

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    //---------------------------------------------------------
                    // Import the distribution and verify that we can execute a command.

                    Wsl2Proxy.Import(TestHelper.TestDistroName, imagePath, tempFolder.Path);
                    Assert.True(Wsl2Proxy.Exists(TestHelper.TestDistroName));

                    var distro = new Wsl2Proxy(TestHelper.TestDistroName);

                    Assert.Equal(TestHelper.TestDistroName, distro.Name);
                    Assert.Contains("Hello World!", distro.Execute("echo", "Hello World!").OutputText);

                    //---------------------------------------------------------
                    // Terminate the distribution and verify that we can export it.

                    var exportPath = Path.Combine(tempFolder.Path, "export.tar");

                    Wsl2Proxy.Terminate(TestHelper.TestDistroName);
                    Wsl2Proxy.Export(TestHelper.TestDistroName, exportPath);

                    //---------------------------------------------------------
                    // Remove the test distribution and verify that we can regenerate
                    // it from the image we just exported.

                    TestHelper.RemoveTestDistro();

                    Wsl2Proxy.Import(TestHelper.TestDistroName, exportPath, tempFolder.Path);
                    Assert.True(Wsl2Proxy.Exists(TestHelper.TestDistroName));

                    distro = new Wsl2Proxy(TestHelper.TestDistroName, user: KubeConst.SysAdminUser);

                    Assert.Contains("Hello World!", distro.Execute("echo", "Hello World!").OutputText);
                }
                finally
                {
                    TestHelper.RemoveTestDistro();
                }
            }
        }

        [Fact]
        public async Task NoSudoPassword()
        {
            // Verify that the distribution doesn't prompt for a SUDO password.

            var imagePath = await TestHelper.GetTestImageAsync();

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    Wsl2Proxy.Import(TestHelper.TestDistroName, imagePath, tempFolder.Path);
                    Assert.True(Wsl2Proxy.Exists(TestHelper.TestDistroName));

                    var distro = new Wsl2Proxy(TestHelper.TestDistroName, user: KubeConst.SysAdminUser);

                    Assert.Contains("Hello World!", distro.SudoExecute("echo", "Hello World!").OutputText);
                }
                finally
                {
                    TestHelper.RemoveTestDistro();
                }
            }
        }

        [Fact]
        public async Task Execute()
        {
            // Verify that we can execute SUDO and non-SUDO commands.

            var imagePath = await TestHelper.GetTestImageAsync();

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    Wsl2Proxy.Import(TestHelper.TestDistroName, imagePath, tempFolder.Path);

                    var distro = new Wsl2Proxy(TestHelper.TestDistroName, user: KubeConst.SysAdminUser);

                    Assert.Contains("Hello World!", distro.Execute("echo", "Hello World!").OutputText);
                    Assert.Contains("Hello World!", distro.SudoExecute("echo", "Hello World!").OutputText);
                }
                finally
                {
                    TestHelper.RemoveTestDistro();
                }
            }
        }

        [Fact]
        public async Task Execute_WithPathSpaces()
        {
            // Verify that we can execute SUDO and non-SUDO commands from a temporary
            // folder that includes spaces.  This happens when the user's Windows username
            // include spaces.

            var imagePath = await TestHelper.GetTestImageAsync();

            using (var folderWithSpaces = new TempFolder(folder: Path.Combine(Path.GetTempPath(), $"test {Guid.NewGuid().ToString("d")}")))
            {
                using (var tempFolder = new TempFolder())
                {
                    try
                    {
                        Wsl2Proxy.Import(TestHelper.TestDistroName, imagePath, tempFolder.Path);

                        var distro = new Wsl2Proxy(TestHelper.TestDistroName, user: KubeConst.SysAdminUser);

                        distro.TempFolder = folderWithSpaces.Path;

                        Assert.Contains("Hello World!", distro.Execute("echo", "Hello World!").OutputText);
                        Assert.Contains("Hello World!", distro.SudoExecute("echo", "Hello World!").OutputText);
                    }
                    finally
                    {
                        TestHelper.RemoveTestDistro();
                    }
                }
            }
        }

        [Fact]
        public async Task PathMapping()
        {
            // Verify that file system path mapping works in both directions.

            var imagePath = await TestHelper.GetTestImageAsync();

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    Wsl2Proxy.Import(TestHelper.TestDistroName, imagePath, tempFolder.Path);

                    var distro = new Wsl2Proxy(TestHelper.TestDistroName, user: KubeConst.SysAdminUser);

                    // Linux --> Windows

                    Assert.Equal($@"\\wsl$\{distro.Name}\", distro.ToWindowsPath("/"));
                    Assert.Equal($@"\\wsl$\{distro.Name}\bin\bash", distro.ToWindowsPath("/bin/bash"));

                    // Windows --> Linux

                    Assert.Equal("/mnt/c/", distro.ToLinuxPath(@"C:\"));
                    Assert.Equal("/mnt/c/Program Files/test.exe", distro.ToLinuxPath(@"c:\Program Files\test.exe"));
                }
                finally
                {
                    TestHelper.RemoveTestDistro();
                }
            }
        }

        [Fact]
        public async Task UploadFile()
        {
            // Verify that we can upload a text file to the distribution
            // and set its owner and permissions.

            var imagePath = await TestHelper.GetTestImageAsync();

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    Wsl2Proxy.Import(TestHelper.TestDistroName, imagePath, tempFolder.Path);

                    var distro = new Wsl2Proxy(TestHelper.TestDistroName, user: KubeConst.SysAdminUser);
                    var text   =
@"Line 1
Line 2
Line 3
Line 4
";
                    // Write a file using the defaults to convert CRLF-->LF with 
                    // no special permissions.

                    distro.UploadFile($"/home/{KubeConst.SysAdminUser}/test1.txt", text, toLinuxText: true);
                    Assert.Equal("Line 1\nLine 2\nLine 3\nLine 4\n", File.ReadAllText(distro.ToWindowsPath($"/home/{KubeConst.SysAdminUser}/test1.txt")));

                    var response = distro.SudoExecute("ls", "-l", $"/home/{KubeConst.SysAdminUser}/test1.txt");

                    response.EnsureSuccess();
                    Assert.StartsWith("-rw-r--r-- ", response.OutputText);

                    // Write another file using the leaving the line endings as CRLF
                    // and some permissions.  Also verify that the file is owned by
                    // the default distro user.

                    distro.UploadFile($"/home/{KubeConst.SysAdminUser}/test2.txt", text, permissions: "666", toLinuxText: false);
                    Assert.Equal("Line 1\r\nLine 2\r\nLine 3\r\nLine 4\r\n", File.ReadAllText(distro.ToWindowsPath($"/home/{KubeConst.SysAdminUser}/test2.txt")));

                    response = distro.SudoExecute("ls", "-l", $"/home/{KubeConst.SysAdminUser}/test2.txt");

                    response.EnsureSuccess();
                    Assert.StartsWith("-rw-rw-rw- ", response.OutputText);
                    Assert.Contains($"{KubeConst.SysAdminUser} {KubeConst.SysAdminUser}", response.OutputText);
                }
                finally
                {
                    TestHelper.RemoveTestDistro();
                }
            }
        }

        [Fact]
        public async Task StartAs_Root()
        {
            // Verify that we can start a distro as [root] without configuring
            // or starting systemd.

            var imagePath = await TestHelper.GetTestImageAsync();

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    Wsl2Proxy.Import(TestHelper.TestDistroName, imagePath, tempFolder.Path);

                    // Start as the default (root) user.

                    var distro = new Wsl2Proxy(TestHelper.TestDistroName);

                    Assert.Equal("root", distro.User);

                    // Expecting to be running under MSFT's [init] process 1.

                    var response = distro.Execute("ps", "-e");

                    Assert.Equal(0, response.ExitCode);
                    Assert.Contains("init", response.OutputText);

                    // Expecting to be logged in as [root]

                    response = distro.Execute("echo", "$LOGNAME");

                    Assert.Equal(0, response.ExitCode);
                    Assert.Equal("root", response.OutputText.Trim());
                }
                finally
                {
                    TestHelper.RemoveTestDistro();
                }
            }
        }

        [Fact]
        public async Task StartAs_Sysadmin()
        {
            // Verify that we can start a distro as [sysadmin] without configuring
            // or starting systemd.

            var imagePath = await TestHelper.GetTestImageAsync();

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    Wsl2Proxy.Import(TestHelper.TestDistroName, imagePath, tempFolder.Path);

                    // Start as the [sysadmin] user.

                    var distro = new Wsl2Proxy(TestHelper.TestDistroName, user: KubeConst.SysAdminUser);

                    Assert.Equal(KubeConst.SysAdminUser, distro.User);

                    // Expecting to be running under MSFT's [init] process 1.

                    var response = distro.Execute("ps", "-e");

                    Assert.Equal(0, response.ExitCode);
                    Assert.Contains("init", response.OutputText);

                    // Expecting to be logged in as [root]

                    response = distro.Execute("echo", "$LOGNAME");

                    Assert.Equal(0, response.ExitCode);
                    Assert.Equal(KubeConst.SysAdminUser, response.OutputText.Trim());
                }
                finally
                {
                    TestHelper.RemoveTestDistro();
                }
            }
        }
    }
}
