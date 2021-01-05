//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Service;

using k8s;
using k8s.Models;

namespace NeonClusterManager
{
    /// <summary>
    /// The Neon cluster initialization operator.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The program entrypoint.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async static Task Main(string[] args)
        {
            await new Service(NeonServices.ClusterManager, serviceMap: NeonServiceMap.Production).RunAsync();
        }
    }
}
