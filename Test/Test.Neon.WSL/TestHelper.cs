//-----------------------------------------------------------------------------
// FILE:	    TestHelper.cs
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
    /// <summary>
    /// Test helpers.
    /// </summary>
    public static class TestHelper
    {
        /// <summary>
        /// Reasonable base WSL2 image for testing.  This will need to be updated if/when the
        /// location for this changes.
        /// </summary>
        public const string BaseImageUri = $"{KubeDownloads.NeonPublicBucketUri}/vm-images/wsl2/base/ubuntu-20.04.20210206.wsl2.amd64.tar.gz";

        /// <summary>
        /// Used to identify the WSL2 distribution we'll be using for testing.
        /// </summary>
        public const string TestDistroName = "neonkube-test-distro";

        /// <summary>
        /// Returns the path to the test cache folder.
        /// </summary>
        public static readonly string TestCacheFolder = Path.Combine(Environment.GetEnvironmentVariable("NC_CACHE"), "test-wsl2");

        /// <summary>
        /// Downloads and caches and decompresses the Wsl2 base image for testing.
        /// </summary>
        /// <returns>Returns the path to the decompressed Wsl2 base TAR file.</returns>
        public static async Task<string> GetTestImageAsync()
        {
            var imagePath = Path.Combine(TestCacheFolder, "image.tar");

            Directory.CreateDirectory(TestCacheFolder);

            if (!File.Exists(imagePath))
            {
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        await httpClient.GetToFileSafeAsync(BaseImageUri, imagePath);
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
        /// Ensures that the <see cref="TestDistroName"/> does not exist from a past test run.
        /// </summary>
        public static void RemoveTestDistro()
        {
            if (Wsl2Proxy.Exists(TestDistroName))
            {
                Wsl2Proxy.Unregister(TestDistroName);
            }
        }
    }
}
