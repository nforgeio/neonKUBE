//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
    /// The <b>neon-node-agent</b> entry point.
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
            // Intercept and handle KubeOps [generator] commands executed by the 
            // KubeOps MSBUILD tasks.

            try
            {
                if (await OperatorHelper.HandleGeneratorCommand<Startup>(args))
                {
                    return;
                }

                Service = new Service(KubeService.NeonNodeAgent);

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
        /// adds these assemblies to the <see cref="KubeOps.Operator.Builder.IOperatorBuilder"/> passed.
        /// </summary>
        /// <param name="operatorBuilder">The target operator builder.</param>
        internal static void AddResourceAssemblies(KubeOps.Operator.Builder.IOperatorBuilder operatorBuilder)
        {
            Covenant.Requires<ArgumentNullException>(operatorBuilder != null, nameof(operatorBuilder));

            operatorBuilder.AddResourceAssembly(Assembly.GetExecutingAssembly());
            operatorBuilder.AddResourceAssembly(typeof(Neon.Kube.ResourceDefinitions.Stub).Assembly);
        }
    }
}
