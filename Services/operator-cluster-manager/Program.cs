//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Threading.Tasks;
using Neon.Common;

using Neon.Kube;

using k8s;
using k8s.Models;

using System.Collections.Generic;

namespace ClusterManager
{
    /// <summary>
    /// The Loopie cluster initialization operator.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The program entrypoint.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public static void Main(string[] args)
        {
            new ClusterManager(NeonServiceMap.Production, NeonServices.ClusterManager).RunAsync().Wait();
        }
    }
}
