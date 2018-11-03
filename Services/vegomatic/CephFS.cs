//-----------------------------------------------------------------------------
// FILE:	    CephTest.cs
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
    /// Implements Ceph distributed file system tests.
    /// </summary>
    public class CephFS
    {
        private static INeonLogger log;

        /// <summary>
        /// Constructor.
        /// </summary>
        public CephFS()
        {
            log = LogManager.Default.GetLogger(typeof(Program));
        }

        /// <summary>
        /// Implements CephFS tests.
        /// </summary>
        /// <param name="commandLine">The test command line.</param>
        public async Task ExecAsync(CommandLine commandLine)
        {
            log.LogInfo("Hello World!");

            await Task.CompletedTask;
        }
    }
}
