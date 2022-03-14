//-----------------------------------------------------------------------------
// FILE:	    OperatorHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Tasks;

using KubeOps.Operator;
using KubeOps.Operator.Builder;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Useful utilities for the <b>KubeOps</b> operator SDK.
    /// </summary>
    public static class OperatorHelper
    {
        //-------------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Configures the operator's service controllers.
        /// </summary>
        private class Startup
        {
            /// <summary>
            /// Configures depdendency injection.
            /// </summary>
            /// <param name="services">The service collection.</param>
            public void ConfigureServices(IServiceCollection services)
            {
                Covenant.Assert(operatorAssembly != null);

                var operatorBuilder = services.AddKubernetesOperator();

                operatorBuilder.AddResourceAssembly(OperatorHelper.operatorAssembly);

                if (builderCallback != null)
                {
                    builderCallback(operatorBuilder);
                }
            }

            /// <summary>
            /// Configures the operator service controllers.
            /// </summary>
            /// <param name="app">Specifies the application builder.</param>
            public void Configure(IApplicationBuilder app)
            {
                app.UseKubernetesOperator();
            }
        }

        //-------------------------------------------------------------------------
        // Implementation

        private static Assembly                     operatorAssembly;
        private static Action<IOperatorBuilder>     builderCallback;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static OperatorHelper()
        {
            LogFilter =
                logEvent =>
                {
                    switch (logEvent.LogLevel)
                    {
                        case LogLevel.Info:

                            // KubeOps spams the logs with unnecessary INFO events when events are raised to
                            // the controller.  We're going to filter these and do our own logging using this
                            // filter.  The filter returns TRUE for events to be logged and FALSE for events
                            // to be ignored.

                            if (logEvent.Module == "KubeOps.Operator.Controller.ManagedResourceController")
                            {
                                if (logEvent.Message.Contains("successfully reconciled"))
                                {
                                    return false;
                                }
                            }
                            break;

                        case LogLevel.Error:

                            // Kubernetes client is not handling watches correctly when there are no objects
                            // to be watched.  I read that the API server is returning a blank body in this
                            // case but the Kubernetes client is expecting valid JSON, like an empty array.

                            if (logEvent.Module == "KubeOps.Operator.Kubernetes.ResourceWatcher")
                            {
                                if (logEvent.Message.Contains("The input does not contain any JSON tokens"))
                                {
                                    return false;
                                }
                            }
                            break;
                    }

                    return true;
                };
        }

        /// <summary>
        /// Returns a log filter that can be used to filter out some of the log spam from KubeOps
        /// and the Kubernetes client.
        /// </summary>
        public static Func<LogEvent, bool> LogFilter { get; private set; }

        /// <summary>
        /// Handles <b>generator</b> commands invoked on an operator application
        /// during build by the built-in KubrOps build targets.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <param name="builderCallback">
        /// Optionally specifies the callback used to identify additional the assemblies 
        /// where custom resources are defined.  Note that the assembly that called
        /// <see cref="HandleGeneratorCommand(string[], Action{IOperatorBuilder})"/>
        /// is included by default.
        /// </param>
        /// <returns>
        /// <c>true</c> when the command was handled or <c>false</c> for other commands 
        /// that should be handled by the operator application itself.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The KubeOps operator SDK includes MSBUILD tasks that generate CRDs as well
        /// as deployment related manifest files.  These work by executing the operator
        /// application after it's built, passing one or more <b>generator</b> commands
        /// on the command line.
        /// </para>
        /// <para>
        /// You should call this early within your operator's <b>Main(string[] args)</b>
        /// method, passing the command line arguments as well as a callback where you
        /// will identify additional assemblies that may include custom resource types.
        /// This method handles any <b>generator</b> commands and returns <c>true</c> for 
        /// these.  Your main method should return immediately in this case.  Otherwise,
        /// your <b>Main()</b> method should continue with normal application startup.
        /// </para>
        /// <note>
        /// This method identifies the calling assembly as potentially including custom
        /// resources.
        /// </note>
        /// <para>
        /// Your operator <b>Main()</b> entrypoint should look something like:
        /// </para>
        /// <code language="C#">
        /// public static async Task Main(string[] args)
        /// {
        ///     if (await OperatorHelper(args, 
        ///             operatorBuilder =>
        ///             {
        ///                 // This is where you'll identify any additional assemblies
        ///                 // defining custom resource types.
        /// 
        ///                 operatorBuilder.AddResourceAssembly(typeof(V1MyCustomResource).Assembly)
        ///             }))
        ///         {
        ///             return;
        ///         }
        ///         
        ///     // Continue with normal operator startup here.
        /// }
        /// </code>
        /// </remarks>
        public static async Task<bool> HandleGeneratorCommand(string[] args, Action<IOperatorBuilder> builderCallback = null)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));;

            try
            {
                if (args.FirstOrDefault() != "generator")
                {
                    return false;
                }

                OperatorHelper.operatorAssembly = Assembly.GetCallingAssembly();
                OperatorHelper.builderCallback  = builderCallback;

                await Host.CreateDefaultBuilder(args)
                    .ConfigureWebHostDefaults(builder => { builder.UseStartup<Startup>(); })
                    .Build()
                    .RunOperatorAsync(args);

                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
                Environment.Exit(1);
                return true;
            }
        }
    }
}
