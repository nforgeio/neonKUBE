//-----------------------------------------------------------------------------
// FILE:	    Test_Wsl2Proxy.cs
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
using Neon.Kube.Xunit;
using Neon.Net;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    public class Test_Wsl2Proxy
    {
        /// <summary>
        /// Reasonable base WSL2 image for testing.  This will need to be updated if/when the
        /// location for this changes.
        /// </summary>
        private const string BaseImageUri = "https://neonkube.s3-us-west-2.amazonaws.com/images/wsl2/base/ubuntu-20.04.20210129.tar";

        /// <summary>
        /// Used to identify the WSL2 distribution we'll be using for testing.
        /// </summary>
        private const string TestDistro = "neonkube-test-distro";

        private string TestCacheFolder = Path.Combine(Environment.GetEnvironmentVariable("NF_CACHE"), "Test-Wsl2");

        /// <summary>
        /// Constructor.
        /// </summary>
        public Test_Wsl2Proxy()
        {
            // Start each test case without an existing test distribution.

            RemoveTestDistro();
        }

        /// <summary>
        /// Downloads and caches and decompresses the Wsl2 base image for testing.
        /// </summary>
        /// <returns>Returns the path to the decompressed Wsl2 base TAR file.</returns>
        private async Task<string> GetTestImageAsync()
        {
            var imagePath = Path.Combine(TestCacheFolder, "image.tar");

            Directory.CreateDirectory(TestCacheFolder);

            if (!File.Exists(imagePath))
            {
                try
                {
                    using (var output = new FileStream(imagePath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        using (var httpClient = new HttpClient())
                        {
                            var stream = await httpClient.GetStreamSafeAsync(BaseImageUri);
                            var buffer = new byte[64 * 1024];

                            while (true)
                            {
                                var cb = stream.Read(buffer);

                                if (cb == 0)
                                {
                                    break;
                                }

                                output.Write(buffer, 0, cb);
                            }
                        }
                    }
                }
                catch
                {
                    // Remove a partially downloaded file.

                    NeonHelper.DeleteFile(imagePath);
                    throw;
                }
            }

            return imagePath;
        }

        /// <summary>
        /// Ensures that the <see cref="TestDistro"/> does not exist from a past test run.
        /// </summary>
        private void RemoveTestDistro()
        {
            if (Wsl2Proxy.Exists(TestDistro))
            {
                Wsl2Proxy.Unregister(TestDistro);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task ImportExport()
        {
            // Verify that we can import and export distributions.

            var imagePath = await GetTestImageAsync();

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    //---------------------------------------------------------
                    // Import the distribution and verify that we can execute a command.

                    Wsl2Proxy.Import(TestDistro, imagePath, tempFolder.Path);
                    Assert.True(Wsl2Proxy.Exists(TestDistro));

                    var distro = new Wsl2Proxy(TestDistro);

                    Assert.Equal(TestDistro, distro.Name);
                    Assert.True(distro.IsRunning);
                    Assert.True(distro.IsPrepared);
                    Assert.Contains("Hello World!", distro.Execute("echo", "Hello World!").OutputText);

                    //---------------------------------------------------------
                    // Terminate the distribution and verify that we can export it.

                    var exportPath = Path.Combine(tempFolder.Path, "export.tar");

                    Wsl2Proxy.Terminate(TestDistro);
                    Wsl2Proxy.Export(TestDistro, exportPath);

                    //---------------------------------------------------------
                    // Remove the test distribution and verify that we can regenerate
                    // it from the image we just exported.

                    RemoveTestDistro();

                    Wsl2Proxy.Import(TestDistro, exportPath, tempFolder.Path);
                    Assert.True(Wsl2Proxy.Exists(TestDistro));

                    distro = new Wsl2Proxy(TestDistro);

                    Assert.True(distro.IsRunning);
                    Assert.True(distro.IsPrepared);
                    Assert.Contains("Hello World!", distro.Execute("echo", "Hello World!").OutputText);
                }
                finally
                {
                    RemoveTestDistro();
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task Address()
        {
            // Verify that we can obtain a distribution's IP address.

            var imagePath = await GetTestImageAsync();

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    Wsl2Proxy.Import(TestDistro, imagePath, tempFolder.Path);
                    Assert.True(Wsl2Proxy.Exists(TestDistro));

                    var distro  = new Wsl2Proxy(TestDistro);
                    var address = IPAddress.Parse(distro.Address);

                    Assert.Equal(AddressFamily.InterNetwork, address.AddressFamily);

                    // Verify that we can ping the distribution via its address.

                    using (var pinger = new Pinger())
                    {
                        var reply = pinger.SendPingAsync(address, 2000).Result;

                        Assert.Equal(IPStatus.Success, reply.Status);
                    }
                }
                finally
                {
                    RemoveTestDistro();
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task NoSudoPassword()
        {
            // Verify that the distribution doesn't prompt for a SUDO password.

            var imagePath = await GetTestImageAsync();

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    Wsl2Proxy.Import(TestDistro, imagePath, tempFolder.Path);
                    Assert.True(Wsl2Proxy.Exists(TestDistro));

                    var distro = new Wsl2Proxy(TestDistro);

                    Assert.Contains("Hello World!", distro.SudoExecute("echo", "Hello World!").OutputText);
                }
                finally
                {
                    RemoveTestDistro();
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task Execute()
        {
            // Verify that we can execute SUDO and non-SUDO commands.

            var imagePath = await GetTestImageAsync();

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    Wsl2Proxy.Import(TestDistro, imagePath, tempFolder.Path);

                    var distro = new Wsl2Proxy(TestDistro);

                    Assert.Contains("Hello World!", distro.Execute("echo", "Hello World!").OutputText);
                    Assert.Contains("Hello World!", distro.SudoExecute("echo", "Hello World!").OutputText);
                }
                finally
                {
                    RemoveTestDistro();
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task PathMapping()
        {
            // Verify that file system path mapping works in both directions.

            var imagePath = await GetTestImageAsync();

            using (var tempFolder = new TempFolder())
            {
                try
                {
                    Wsl2Proxy.Import(TestDistro, imagePath, tempFolder.Path);

                    var distro = new Wsl2Proxy(TestDistro);

                    // Linux --> Windows

                    Assert.Equal($@"\\wsl$\{distro.Name}\", distro.ToWindowsPath("/"));
                    Assert.Equal($@"\\wsl$\{distro.Name}\bin\bash", distro.ToWindowsPath("/bin/bash"));

                    // Windows --> Linux

                    Assert.Equal("/mnt/c/", distro.ToLinuxPath(@"C:\"));
                    Assert.Equal("/mnt/c/Program Files/test.exe", distro.ToLinuxPath(@"c:\Program Files\test.exe"));
                }
                finally
                {
                    RemoveTestDistro();
                }
            }
        }
    }
}