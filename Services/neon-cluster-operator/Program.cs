//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Marcus Bowyer, Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Service;

using k8s;
using k8s.Models;
using KubeOps.Operator;

namespace NeonClusterOperator
{
    /// <summary>
    /// The <b>neon-cluster-operator</b> entrypoint.
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
            Resources = Assembly.GetExecutingAssembly().GetResourceFileSystem("NeonClusterOperator.Resources");

            // Intercept KubeOps [generator] commands and execute them here.  
            // These commands will be invoked by the KubeOps MSBUILD targets
            // immediately after the assembly is complied and are responsible
            // for generating the CRDs and Kubernetes installation manifests.

            if (args.FirstOrDefault() == "generator")
            {
                // $debug(jefflill): Temporarily disabled.

                return;

                //await Host.CreateDefaultBuilder(args)
                //    .ConfigureWebHostDefaults(builder => { builder.UseStartup<Startup>(); })
                //    .Build()
                //    .RunOperatorAsync(args);

                //return;
            }

            await new Service(KubeService.NeonClusterOperator).RunAsync();
        }
    }
}
