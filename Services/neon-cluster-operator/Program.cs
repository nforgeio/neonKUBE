//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Marcus Bowyer, Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

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
using Neon.Kube.Service;
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
        public static IStaticDirectory Resources { get; private set; } = Assembly.GetExecutingAssembly().GetResourceFileSystem("NeonClusterOperator.Resources");

        /// <summary>
        /// The program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task Main(string[] args)
        {
            // Intercept KubeOps [generator] commands and execute them here.

            if (await OperatorHelper.HandleGeneratorCommand(args))
            {
                return;
            }

            await new Service(KubeService.NeonClusterOperator).RunAsync();
        }
    }
}
