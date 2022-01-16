//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
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
        /// The Linux path where the host node's file system is mounted;
        /// </summary>
        public const string HostMount = "/mnt/host";

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
            // Intercept and handle KubeOps [generator] commands executed by the 
            // KubeOps MSBUILD tasks.

            if (await OperatorHelper.HandleGeneratorCommand(args, AddResourceAssemblies))
            {
                return;
            }

            Service = new Service(KubeService.NeonNodeAgent);

            await Service.RunAsync();
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

        /// <summary>
        /// <para>
        /// Executes a command on the host node, setting the file system root to <see cref="HostMount"/>
        /// where the host's file system is mounted.
        /// </para>
        /// <note>
        /// WARNING! This relies on the pod's environment variables like PATH matching the host
        /// environment, which is currently the case because the Microsoft .NET container images
        /// are based on Ubuntu, as are neonKUBE cluster nodes.
        /// </note>
        /// </summary>
        /// <param name="command">The fully qualified path to the command to be executed (relative to the host file system).</param>
        /// <param name="args">Optional command arguments.</param>
        /// <returns>The <see cref="ExecuteResponse"/>.</returns>
        public static ExecuteResponse HostExecuteCapture(string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

            var actualArgs = new List<object>();

            actualArgs.Add(HostMount);
            actualArgs.Add(command);

            foreach (var arg in args)
            {
                actualArgs.Add(arg);
            }

            return NeonHelper.ExecuteCapture("chroot", actualArgs.ToArray());
        }
    }
}
