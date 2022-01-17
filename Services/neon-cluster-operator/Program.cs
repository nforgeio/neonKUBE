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
        /// Returns the program's service implementation.
        /// </summary>
        public static Service Service { get; private set; }

        /// <summary>
        /// The program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task Main(string[] args)
        {
            try
            {
                // Intercept and handle KubeOps [generator] commands executed by the 
                // KubeOps MSBUILD tasks.

                if (await OperatorHelper.HandleGeneratorCommand(args, AddResourceAssemblies))
                {
                    return;
                }

                Service = new Service(KubeService.NeonClusterOperator);

                Environment.Exit(await Service.RunAsync());
            }
            catch (Exception e)
            {
                // We really shouldn't see exceptions here but let's log something
                // just in case.  Note that logging may not be initialized yet so
                // we'll just output a string.

                Console.Error.WriteLine(NeonHelper.ExceptionError(e));
                Environment.Exit(-1);
            }
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
            operatorBuilder.AddResourceAssembly(typeof(Neon.Kube.Resources.Stub).Assembly);
        }
    }
}
