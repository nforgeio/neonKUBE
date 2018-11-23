//-----------------------------------------------------------------------------
// FILE:	    IssueMntc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Hive;
using Neon.Net;

namespace NeonVegomatic
{
    /// <summary>
    /// Attempts to replicate behavior to reproduce the <b>mnt/c</b> Docker issue.
    /// </summary>
    public class IssueMntc
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public IssueMntc()
        {
        }

        /// <summary>
        /// Implements the command.
        /// </summary>
        /// <param name="commandLine">The test command line.</param>
        public async Task ExecAsync(CommandLine commandLine)
        {
            // Exercise the mounted folder a bit.

            for (int i = 0; i < 10; i++)
            {
                var folder = $"/test/{i}";

                // List the contents of each of the mounted test folders.

                Directory.GetFiles(folder);

                // Write a new file.

                await File.WriteAllTextAsync(Path.Combine(folder, Guid.NewGuid().ToString("D")), "test");
            }

            // Keep running for another 10 seconds to test so we can ensure
            // that multiple instances are running in parallel.

            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}
