//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Marcus Bowyer, Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Service;

using k8s;
using k8s.Models;

using KubeOps.Operator;
using KubeOps.Operator.Builder;

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
            // Intercept and handle KubeOps [generator] commands executed by the 
            // KubeOps MSBUILD tasks.

            if (await OperatorHelper.HandleGeneratorCommand(args, AddResourceAssemblies))
            {
                return;
            }

            await new Service(KubeService.NeonClusterOperator).RunAsync();
        }

        /// <summary>
        /// Identifies assemblies that may include custom resource types by adding these
        /// assemblies to the <see cref="IOperatorBuilder"/> passed.
        /// </summary>
        /// <param name="operatorBuilder">The target operator builder.</param>
        internal static void AddResourceAssemblies(IOperatorBuilder operatorBuilder)
        {
            Covenant.Requires<ArgumentNullException>(operatorBuilder != null, nameof(operatorBuilder));

            operatorBuilder.AddResourceAssembly(typeof(Neon.Kube.Resources.V1ContainerRegistry).Assembly);
        }
    }
}
