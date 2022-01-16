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

using KubeOps.Operator;
using KubeOps.Operator.Builder;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Useful utilities for the <b>KubeOps</b> operatort SDK.
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
                                if (logEvent.Message.Contains("Event type \"Reconcile\"") ||
                                    logEvent.Message.Contains("Event type \"Modified\"") ||
                                    logEvent.Message.Contains("Event type \"Deleted\""))
                                {
                                    return false;
                                }
                            }

                            // KubeOps logs INFO events for every event raised on the controller.  I prefer
                            // doing our own logging for this.

                            if (logEvent.Module == "KubeOps.Operator.Controller.ManagedResourceController")
                            {
                                return false;
                            }
                            break;

                        case LogLevel.Error:

                            // Kubernetes client is not handling watches correctly when there are no objects
                            // to be watched.  I read that the API server is returning a blank body in this
                            // case but the Kubernetes client is expecting valid JSON, like an empty array.

                            if (logEvent.Module == "KubeOps.Operator.Kubernetes.ResourceWatcher" && logEvent.Module.Contains("The input does not contain any JSON tokens"))
                            {
                                return false;
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
        /// One complexity is that the stock KubeOps implementation requires that a 
        /// KUBECONFIG file with a current context exists during build, even when we're 
        /// just generating these CRDs etc.  This method handles this by temporarily
        /// configuring a dummy KUBECONFIG file while handling <b>generator</b>
        /// commands.
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
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));;

            try
            {
                if (args.FirstOrDefault() != "generator")
                {
                    return false;
                }

                OperatorHelper.operatorAssembly = Assembly.GetCallingAssembly();
                OperatorHelper.builderCallback  = builderCallback;

                // KubeOps requires an active Kubernetes config context, even for these build/generator
                // operations.  This is due to the KubeOps code injecting a [KubernetesClient] using
                // the default constructor which needs a context to work.
                //
                // The workaround is to create a temporary kubeconfig file with a current context that
                // doesn't reference an actual cluster and point a local KUBECONFIG enviroment variable
                // at it while we're executing the [generator] command.

                using (var tempFile = new TempFile())
                {
                    const string config =
@"
apiVersion: v1
clusters:
- cluster:
    certificate-authority-data: |
      LS0tLS1CRUdJTiBDRVJUSUZJQ0FURS0tLS0tCk1JSUM1ekNDQWMrZ0F3SUJBZ0lCQURBTkJna3Foa2lH
      OXcwQkFRc0ZBREFWTVJNd0VRWURWUVFERXdwcmRXSmwKY201bGRHVnpNQjRYRFRJeE1UQXlPREF3TURZ
      MU1Gb1hEVE14TVRBeU5qQXdNRFkxTUZvd0ZURVRNQkVHQTFVRQpBeE1LYTNWaVpYSnVaWFJsY3pDQ0FT
      SXdEUVlKS29aSWh2Y05BUUVCQlFBRGdnRVBBRENDQVFvQ2dnRUJBTnh4CmRuVUhucWlsekRJekhRY1kw
      YXUvVTBjMWk3VXJHSnlORmxlSVBKTGdSMW9EYlQ3dGpONitsRFBOMGF4YkxzRTkKNHM3Ynh4YWl1MW56
      U2ZldFFwcnJKaEFhQ0xlL1dsbFNza05CT2V0NmFzSzIydm5iRkkxNUhOVEFuSE5Eckw0KwoxT3A1QlUr
      TURPWWgvb0hqTFc2K1F4QVp6VTlaYjVNaE9lTElTa0t3Vys3YWNVQ3lHd1lGVVc5cXVadjJ3VUcvCi9h
      MlhVRGFGeldNMXFxUmtHRHo1d21JZklOSXVTTnBpM3B2bkdhOGNSa3VhOEF5WlpvTEtzOFEzQ1RHNGJS
      dTEKaEVDaTJEZHRuLy9heWJyWjNFQlJNRjhPd0t3UjRtV0tEL2VkeHJCL1g1SzBuZUFqUHJ4S1YzYW9P
      ZGVlcVduawp5RzJNNW1oc3VVY0ppa2w2S004Q0F3RUFBYU5DTUVBd0RnWURWUjBQQVFIL0JBUURBZ0tr
      TUE4R0ExVWRFd0VCCi93UUZNQU1CQWY4d0hRWURWUjBPQkJZRUZBa3Y4LzRrOGdoUVpjQUhYZUpvVEhk
      eUU5dFFNQTBHQ1NxR1NJYjMKRFFFQkN3VUFBNElCQVFBVHI5T1RRUVJIaDNUaVVRcDZtNlczZU9kdjVl
      eEZyNjNwVldjSVBXSDlnWVFVc0FTQwpHaThiZlVHYW5HYWJZMFdkZHdwb1Q4cWJaQ090SjAvZENkdzA5
      Ukd5NVc3NEFUMlZNVjhKSXB5VGROb1ZRVmx4ClhpQWwybG1nNGJFUzJjbnFlN3ZsUXlvQ2pES0Uvc3Zj
      czQrNStkQzdTbyt5OGQ1ZEJSMjJaVnJvMDRNRDJPVE0KNlNlUW1ySUtaMDhrRjRaUXlOOGZFSGowcFhZ
      azY4ZmJXbE5GYW53dktrVnAzaDVuV1RUOCtIV05xWUcyZG5DUwpRQ2ZzUmIrU2lIcEt1REp1QjEyYSsz
      ajRSTWg1aTNRcFF2WDNTc0RIYk8zdlVRZWV3a0h5ZjA4NmRybFY1cnZqCkNRdldGMTByd3RqSXIzdU15
      UERUb284eHl4bGtoeFZVeWlCSAotLS0tLUVORCBDRVJUSUZJQ0FURS0tLS0tCg==
    server: https://no.where:6443
  name: fake-cluster
contexts:
- context:
    cluster: fake-cluster
    user: root@fake-cluster
  name: root@fake-cluster
current-context: root@fake-cluster
kind: Config
preferences: {}
users:
- name: root@fake-cluster
  user:
    client-certificate-data: |
      LS0tLS1CRUdJTiBDRVJUSUZJQ0FURS0tLS0tCk1JSURJVENDQWdtZ0F3SUJBZ0lJY21mclN5QzNIREl3
      RFFZSktvWklodmNOQVFFTEJRQXdGVEVUTUJFR0ExVUUKQXhNS2EzVmlaWEp1WlhSbGN6QWVGdzB5TWpB
      eE1ERXdPRFU0TVRaYUZ3MHlNekF4TURFd09EVTRNVGRhTURReApGekFWQmdOVkJBb1REbk41YzNSbGJU
      cHRZWE4wWlhKek1Sa3dGd1lEVlFRREV4QnJkV0psY201bGRHVnpMV0ZrCmJXbHVNSUlCSWpBTkJna3Fo
      a2lHOXcwQkFRRUZBQU9DQVE4QU1JSUJDZ0tDQVFFQTRrTnVzQVFrYThFR1dGZHUKajFORGRGNXlWYjBE
      VENTemhnTE94T1EreE1RbHAvcU96eTNpcWpxSlpNVThGbG13d0toM1Fjbmd1K01vVERlRgpCa1kxTHFN
      SkxPRU1JY3hnRkxPdkhua1lLNUVxZlJPMmM5ai9RTFRIc1FFTCtnSU1GUVVWZlNZcW0rWVdWdEdmCjdu
      S1FvbHYxbm4yeWM2U3k1Q1ZyMFQxNUtOVEZOWmkzQWM3QTIvMDd1aDhaS1dYak1FdmNMZmlhS2V2ZjJ3
      d2UKcUxiQW56Y3ZtRktjeTI1SldxTDY3dTlpZUJsK2VudHBBeVM4b3JZVUErdjJSdDZkZVN6cGVsd3B2
      QlVuS01RQQpra0xXLy9MUUlwNE9idE53dGRFY1F2VWxSVzBBRDNRTFlybE1PeVJoRTMvaGU5UGcrYXdY
      czhXd0xRUVFCNUFGCldFWEplUUlEQVFBQm8xWXdWREFPQmdOVkhROEJBZjhFQkFNQ0JhQXdFd1lEVlIw
      bEJBd3dDZ1lJS3dZQkJRVUgKQXdJd0RBWURWUjBUQVFIL0JBSXdBREFmQmdOVkhTTUVHREFXZ0JRT00y
      S01LZHdrTG14elgxbUJPeWFpbHVVMwpHVEFOQmdrcWhraUc5dzBCQVFzRkFBT0NBUUVBbFp2Y0RzOTZT
      Z21qRk1WVTFaUWU0ZUEzMHFVNHR2YmVjUG9pCiszOUVML0ZSRGVkOHV0Mmh0TS9YR3F2MngwN0hkNVVI
      cm1mc0ZKVUdsanpPUjlDbmdib3BMRStKRWFXeUZEWUcKaUQ5NkVEd3BIaERuQk1WVXE3bnpKSFVFS3gr
      SkRWeG0vYTllNk5zOUlJenhoend1QVhpQi9BRWxHcXM4bHFWMApxSzVRU1lQeUQremQrZ3pKY0srWnh1
      Z05OeTdybTY2aW5VZ2h2dWFEQWNNeW0zSWRhWDIraGZDbWIxK05sWWlwCnpWaTduU1JZZnFzOWQ2RU0x
      cFlNcXdzVlBpSXlhb3dUc1E0ZjJ6dUNDSFFGbEFScjZncDVDQ3I3eWNIdWNDWjIKUWtYWHN4VTA2QlB4
      bGVsRFFaMjF5anByNGswUm1rR0paT0VqazlCNlFhMVpCS203WFE9PQotLS0tLUVORCBDRVJUSUZJQ0FU
      RS0tLS0tCg==
    client-key-data: |
      LS0tLS1CRUdJTiBSU0EgUFJJVkFURSBLRVktLS0tLQpNSUlFcFFJQkFBS0NBUUVBNGtOdXNBUWthOEVH
      V0ZkdWoxTkRkRjV5VmIwRFRDU3poZ0xPeE9RK3hNUWxwL3FPCnp5M2lxanFKWk1VOEZsbXd3S2gzUWNu
      Z3UrTW9URGVGQmtZMUxxTUpMT0VNSWN4Z0ZMT3ZIbmtZSzVFcWZSTzIKYzlqL1FMVEhzUUVMK2dJTUZR
      VVZmU1lxbStZV1Z0R2Y3bktRb2x2MW5uMnljNlN5NUNWcjBUMTVLTlRGTlppMwpBYzdBMi8wN3VoOFpL
      V1hqTUV2Y0xmaWFLZXZmMnd3ZXFMYkFuemN2bUZLY3kyNUpXcUw2N3U5aWVCbCtlbnRwCkF5UzhvcllV
      QSt2MlJ0NmRlU3pwZWx3cHZCVW5LTVFBa2tMVy8vTFFJcDRPYnROd3RkRWNRdlVsUlcwQUQzUUwKWXJs
      TU95UmhFMy9oZTlQZythd1hzOFd3TFFRUUI1QUZXRVhKZVFJREFRQUJBb0lCQVFDazJlYWFmZG9mWEJx
      SQpZT05mcjVXVkFuOGhNcjVsU3RRMXpuUGlCajRwVkpQdkNHSG1WeE12WGNqZXo4bFFxM1paV0NUVG5R
      ZU5QUnNPCk5PRkp5ZnRUaUZ2V0EvMjMzbFVlb0MvMTd0cUtXNUR1WWw5cmxtMmJNbHZQL2VoQTloN2hi
      YnZUVyt4dGU3MUkKOGlBcE5mVmxKY1VWL1pUNEpzWmo3VlBadG9WQkZpM20vTjI3amxVL1NBV3B2Vm1G
      OEZjNlNjb2Q1bEJZRit1Uwp6WThuS3JRd0dJYUpuNVZ1UllHRDVLQUFaRW1qNWVaYktabFR5VnRJemZK
      aW5oS3Iva1ZwdDB4OEZCTVYxaUphClJKRFJ4NHZTQW5WUkxmS3pxSk43ZVI4TFh3RU5Gek5KN0tVei9h
      OGZ3L2ZJMDdkM1hwZnlDZExQeGJXWWcxMEoKMkFFSjZ1TDFBb0dCQU9vbmdqV0pHS29uNGhQRUR4UTl6
      OFY2YVljVWpzZ0Y2YkphRWtwc0hMeERCLzhXUmcyagowL2x5TUo3Mm5PT3VHdjRiMlIrL0dWbmM3blp1
      MTV2R3NXUDh4Q3JLQ2pyL1JXdlVQZTBtT2hvTVZXdXFLT0tMClhLMEQzdjRZZEFzNHQrTkJtLzdwT1lZ
      YnMwWjEzMjljSkhld21iRXUwUWt6eWNvb3BEbDZSNU43QW9HQkFQZGYKZFdvSTQ0eDlVUm84dzB2dmRx
      QzZiaktZWE9kdHNEbkVTWVByWml1eWtYaC82aUl3NWxVR3BZU2I3YUJWaVhZZQpUdmZJR2grMDAydVhC
      SkxPTUxPd0RvaU5rSTF2UGZNanppK1JQWFZsVS9TL05kUFhRK3Y4UzBPUEZyUUFOdEZ0CmpIckQ2UUE2
      RjdvY3VIMXVNUC9OYjBFN3MxUE5Ld25QeG1WbFR4cWJBb0dCQUpNbnYwSXI1YzlSLzFmU3VITk4KSVYy
      SFAvaS9wN2YzVjFaYUd2S2duVEtIb2Vmak5LVnYxMUVHUFo0NWVJSHlNazZPYTlieXYxamxhd3dOUHYx
      TQpVc0YyNGtYTjhiNEFIYjNWaGhHYkc1cXhNNkhWTDVxb1lOYnUvdDZMdWFvdnZBbGJlMUVwZTVoWG9r
      UmU0Y3ZYCmlhZWEyZ3dyVXYzSWlVRytadThrZFFVdkFvR0JBUFVTL2FBdmJrQ2hadGczbXNTQVdXYWpU
      TW1UYVhkZWxGaW0KdnE2VGFJV2lRN3k5L1pnaUdnL2lwZGpiSW5EV1RYbFlUYVB0K3ZPdWtrYmxOd0s2
      aEVXQkJ1VUNXMVBFQWZ3QQpYU1dESHdCUGd2M1c4ZDBPUjV4a042eVc5a2NlYnpETTk0QW8xNDRCLzcv
      QzlJUlB4dnVtNjdJVkUzVFNydkRwCmlBU3NlZEpCQW9HQVBabDNXUXJhRm9kOW8rNGU1RjBzVStjOTlC
      NHRzK2dlRTU0RmdSTDNGYTdCSXZjZ2JkUDUKTkdpY2FIQWdBcldaOWtJVmE2RnVIZ3JpUDJwck9SY1Bu
      YWkveEhIbTZsQmxNZmlSc2VVcFBhS2JvRXQrSFFBaAp5WmNFY1JwbGRMeWd0b3JPL0o4MHltQUExZVRZ
      T2ttaXE3TnhETFROUUVMZHdJYXV3MS90REQ0PQotLS0tLUVORCBSU0EgUFJJVkFURSBLRVktLS0tLQo=
";
                    var orgKUBECONFIG = Environment.GetEnvironmentVariable("KUBECONFIG");

                    try
                    {
                        Environment.SetEnvironmentVariable("KUBECONFIG", tempFile.Path);
                        File.WriteAllText(tempFile.Path, config);

                        await Host.CreateDefaultBuilder(args)
                            .ConfigureWebHostDefaults(builder => { builder.UseStartup<Startup>(); })
                            .Build()
                            .RunOperatorAsync(args);

                        return true;
                    }
                    finally
                    {
                        Environment.SetEnvironmentVariable("KUBECONFIG", orgKUBECONFIG);
                    }
                }
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
