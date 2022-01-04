//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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

namespace NeonNodeAgent
{
    /// <summary>
    /// The <b>neon-node-agent</b> entrypoint.
    /// </summary>
    public static class Program
    {
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

            await new Service(KubeService.NeonNodeAgent).RunAsync();
        }

        /// <summary>
        /// Identifies assemblies that may include custom resource types as well adding the
        /// program assembly so the controller endpoints can also be discovered.  This method
        /// adds these assemblies to the <see cref="IOperatorBuilder"/> passed.
        /// </summary>
        /// <param name="operatorBuilder">The target operator builder.</param>
        internal static void AddResourceAssemblies(IOperatorBuilder operatorBuilder)
        {
            Covenant.Requires<ArgumentNullException>(operatorBuilder != null, nameof(operatorBuilder));

            operatorBuilder.AddResourceAssembly(Assembly.GetExecutingAssembly());
            operatorBuilder.AddResourceAssembly(typeof(Neon.Kube.Resources.V1ContainerRegistry).Assembly);
        }
    }
}
