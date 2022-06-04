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
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Tasks;

using KubeOps.Operator;
using KubeOps.Operator.Builder;

using k8s;
using k8s.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Useful utilities for the <b>KubeOps</b> operator SDK.
    /// </summary>
    public static class OperatorHelper
    {
        private static Assembly     operatorAssembly;

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

                            // KubeOPs also spams the logs with reconnection attempts.

                            if (logEvent.Module == "KubeOps.Operator.Kubernetes.ResourceWatcher")
                            {
                                if (logEvent.Message.StartsWith("Trying to reconnect with exponential backoff"))
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
        /// <typeparam name="TStartup">Specifies the operator's ASP.NET startup type.</typeparam>
        /// <param name="args">The command line arguments.</param>
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
        /// method, passing your controller's <b>Startup</b> type as the generic type parameter.
        /// Your main method should exit immediately when this method returns <c>true</c>.
        /// Otherwise, your <b>Main()</b> method should continue with normal application startup.
        /// </para>
        /// <para>
        /// Your operator <b>Main()</b> entrypoint should look something like:
        /// </para>
        /// <code language="C#">
        /// public static async Task Main(string[] args)
        /// {
        ///     if (await OperatorHelper.HandleGeneratorCommand&lt;Startup&gt;(args))
        ///     {
        ///         return;
        ///     }
        ///         
        ///     // Continue with normal operator startup here.
        /// }
        /// </code>
        /// </remarks>
        public static async Task<bool> HandleGeneratorCommand<TStartup>(string[] args)
            where TStartup: class, new()
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

                await Host.CreateDefaultBuilder(args)
                    .ConfigureWebHostDefaults(builder => { builder.UseStartup<TStartup>(); })
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

        /// <summary>
        /// Creates a new <see cref="JsonPatchDocument"/> that can be used to specify modifications
        /// to a <typeparamref name="T"/> custom object.
        /// </summary>
        /// <typeparam name="T">Specifies the custom object type.</typeparam>
        /// <returns>The <see cref="JsonPatchDocument"/>.</returns>
        public static JsonPatchDocument<T> CreatePatch<T>()
            where T : class
        {
            return new JsonPatchDocument<T>()
            {
                ContractResolver = new DefaultContractResolver()
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };
        }

        /// <summary>
        /// Converts a <see cref="JsonPatchDocument"/> into a <see cref="V1Patch"/> that
        /// can be submitted to the Kubernetes API.
        /// </summary>
        /// <typeparam name="T">Identifies the type being patched.</typeparam>
        /// <param name="patchDoc">The configured patch document.</param>
        /// <returns>The <see cref="V1Patch"/> instance.</returns>
        public static V1Patch ToV1Patch<T>(JsonPatchDocument<T> patchDoc)
            where T : class
        {
            Covenant.Requires<ArgumentNullException>(patchDoc != null, nameof(patchDoc));

            var patchString = JsonConvert.SerializeObject(patchDoc, Formatting.None, NeonHelper.JsonRelaxedSerializerSettings.Value);

            //###############################
            // $debug(jefflill): DELETE THIS!

            Console.WriteLine($"PATCH BEFORE = {patchString}");

            //patchString = patchString.Replace("Status", "status");
            //patchString = patchString.Replace("pending", "Pending");
            //patchString = patchString.Replace("State", "state");

            Console.WriteLine($"PATCH AFTER  = {patchString}");

            //###############################

            return new V1Patch(patchString, V1Patch.PatchType.JsonPatch);
        }
    }
}
