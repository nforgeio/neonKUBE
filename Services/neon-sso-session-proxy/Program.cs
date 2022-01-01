//-----------------------------------------------------------------------------
// FILE:	      Program.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.Service;

namespace NeonSsoSessionProxy
{
    /// <summary>
    /// Holds the global program state.
    /// </summary>
    public static partial class Program
    {
        /// <summary>
        /// The program entrypoint.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public static async Task Main(string[] args)
        {
            var service  = new NeonSsoSessionProxyService(KubeService.NeonSsoSessionProxy);
            var exitCode = await service.RunAsync();

            Environment.Exit(exitCode);
        }
    }
}
