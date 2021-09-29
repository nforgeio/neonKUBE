//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Service;

using k8s;
using k8s.Models;

namespace NeonClusterApi
{
    /// <summary>
    /// The Neon cluster initialization operator.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Returns the static direrctory holding the service embedded resources.
        /// </summary>
        public static IStaticDirectory Resources { get; private set; }

        /// <summary>
        /// The program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task Main(string[] args)
        {
            Resources = Assembly.GetExecutingAssembly().GetResourceFileSystem("NeonClusterApi.Resources");

            await new Service(NeonServices.NeonClusterApiService, serviceMap: NeonServiceMap.Production).RunAsync();
        }
    }
}
