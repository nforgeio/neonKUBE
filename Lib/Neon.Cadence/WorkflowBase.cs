//-----------------------------------------------------------------------------
// FILE:	    WorkflowBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Retry;
using Neon.Time;
using Neon.Diagnostics;
using Neon.Tasks;
using System.Reflection;

namespace Neon.Cadence
{
    /// <summary>
    /// Base class for all application Cadence workflow implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cadence workflows are intended to implement the decision logic around zero
    /// or more activities that actually interact with the outside world or perform
    /// longer running computations.  You'll provide this logic in your <see cref="RunAsync(byte[])"/>
    /// method implementation.  This method accepts workflow parameters as byte array
    /// and returns the workflow result as another byte array (both of these arrays
    /// may also be <c>null</c>).
    /// </para>
    /// <para>
    /// Workflows are pretty easy to implement.  You'll need to derive your custom
    /// workflow class from <see cref="WorkflowBase"/> and implement a default public
    /// constructor and then need to implement the <see cref="RunAsync(byte[])"/> method,
    /// which is where your workflow logic will reside.  
    /// </para>
    /// <note>
    /// <para>
    /// Workflow logic must be deterministic and idempotent:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     <para>
    ///     The code in your <see cref="RunAsync(byte[])"/> method must only rely on
    ///     state and data returned by Cadence methods for determining what to do.
    ///     This allows Cadence to replay previously completed workfow steps when
    ///     a workflow needs to be rescheduled on another worker.
    ///     </para>
    ///     <para>
    ///     This means that you must not call things like <see cref="DateTime.UtcNow"/>
    ///     directly in your workflow because this will likely return a different 
    ///     value every time it's called.  Instead, call  
    ///     </para>
    ///     </item>
    ///     <item>
    ///     <para>
    ///     Workflows are inherently single threaded.  You should never explicitly
    ///     create threads within <see cref="RunAsync(byte[])"/> or use things like
    ///     <see cref="Task.Run(Action)"/> which schedule work on background threads.
    ///     </para>
    ///     <note>
    ///     Workflows are allowed to run multiple activities in parallel and activities
    ///     can be multi-threaded, it's just the workflow code itself that can't use
    ///     threads because those will generally interfere with Cadence's ability to
    ///     replay workflow steps deterministically.
    ///     </note>
    ///     </item>
    ///     <item>
    ///     Workflows must never obtain the current time by using methods like 
    ///     <see cref="DateTime.UtcNow"/> directly.  Use <see cref="UtcNowAsync"/>
    ///     instead.
    ///     </item>
    ///     <item>
    ///     Workflows should never directly query the environment where the workflow 
    ///     code is currently running.  This includes things like environment variables,
    ///     the machine host name or IP address, local files, etc.  You should generally
    ///     use activities for this or obtain this indirectly state via
    ///     <see cref="GetValueAsync(string, byte[], bool)"/>.  Both of these mechanisms will 
    ///     ensure that Cadence can record the state in the workflow history so that it 
    ///     can be replayed if the workflow needs to be rescheduled.
    ///     </item>
    ///     <item>
    ///     Workflows should never obtain things like random numbers or UUIDs 
    ///     directly since these operations are implicitly are non-deterministic 
    ///     because they'll return different values every time.  You'll need to
    ///     use  <see cref="GetValueAsync(string, byte[], bool)"/> with a custom function 
    ///     for these as well or use activities, to ensure that the results are recorded
    ///     in the workflow history.
    ///     </item>
    ///     <item>
    ///     Workflows should never call <see cref="Thread.Sleep(TimeSpan)"/> or 
    ///     <see cref="Task.Delay(TimeSpan)"/>.  Use <see cref="SleepAsync(TimeSpan)"/>
    ///     instead.
    ///     </item>
    /// </list>
    /// </note>
    /// <para>
    /// Here's an overview describing the steps necessary to implement, deploy, and
    /// start a workflow:
    /// </para>
    /// <list type="number">
    /// <item>
    ///     A custom workflow is implemented by deriving a class from <see cref="WorkflowBase"/>,a
    ///     implementing the workflow logic via a <see cref="WorkflowBase.RunAsync(byte[])"/>
    ///     method.  Any custom workflow activities will need to be implemented as classes
    ///     derived from <see cref="ActivityBase"/>.
    /// </item>
    /// <item>
    ///     <para>
    ///     The custom <see cref="WorkflowBase"/> class needs to be deployed as a service or
    ///     application that creates a <see cref="CadenceClient"/> connected to a Cadence
    ///     cluster.  This application needs to call <see cref="CadenceClient.StartWorkflowWorkerAsync(string, string, WorkerOptions)"/>
    ///     and <see cref="CadenceClient.StartActivityWorkerAsync(string, string, WorkerOptions)"/> to
    ///     start the workflow and activity workers as required.
    ///     </para>
    /// </item>
    /// <item>
    ///     <para>
    ///     A global workflow instance can be started by calling <see cref="CadenceClient.StartWorkflowAsync(string, string, byte[], string, WorkflowOptions)"/>,
    ///     passing an optional byte array as workflow arguments as well as optional workflow options.  
    ///     Global workflows have no parent, as opposed to child workflows that run in the context of 
    ///     another workflow (the parent).
    ///     </para>
    ///     <note>
    ///     <see cref="CadenceClient.StartWorkflowAsync(string, string, byte[], string, WorkflowOptions)"/> returns immediately
    ///     after the new workflow has been submitted to Cadence.  This method does not wait
    ///     for the workflow to finish.
    ///     </note>
    /// </item>
    /// <item>
    ///     For Neon Cadence client instances that have started a worker that handles the named workflow,
    ///     Cadence will choose one of the workers and begin executing the workflow there.  The Neon Cadence
    ///     client will instantiate the registered custom <see cref="WorkflowBase"/> call its
    ///     <see cref="WorkflowBase.RunAsync(byte[])"/> method, passing the optional workflow arguments
    ///     encoded as a byte array.
    /// </item>
    /// <item>
    ///     <para>
    ///     The custom <see cref="WorkflowBase.RunAsync(byte[])"/> method implements the workflow by
    ///     calling activities via <see cref="CallActivityAsync(string, byte[], ActivityOptions, CancellationToken?)"/> or 
    ///     <see cref="CallLocalActivityAsync{TActivity}(byte[], LocalActivityOptions,  CancellationToken?)"/> 
    ///     and child workflows via <see cref="CallChildWorkflowAsync(string, byte[], ChildWorkflowOptions, CancellationToken?)"/>,
    ///     making decisions based on their results to call other activities and child workflows, 
    ///     and ultimately return a result or throwing an exception to indicate that the workflow
    ///     failed.
    ///     </para>
    ///     <para>
    ///     The Neon Cadence client expects workflow and activity parameters and results to be 
    ///     byte arrays or <c>null</c>.  It's up to the application to encode the actual values
    ///     into bytes using whatever encoding scheme that makes sense.  It is common though
    ///     to use the <see cref="NeonHelper.JsonSerialize(object, Formatting)"/> and
    ///     <see cref="NeonHelper.JsonDeserialize(Type, string, bool)"/> methods to serialize
    ///     parameters and results to JSON strings and then encode those as UTF-8 bytes.
    ///     </para>
    /// </item>
    /// <item>
    ///     <para>
    ///     Cadence also supports executing low overhead <b>local activities</b>.  These activities
    ///     are executed directly in the current process without needing to be scheduled by the
    ///     Cadence cluster and invoked on a worker.  Local activities are intended for tasks that
    ///     will execute quickly, on the order of a few seconds.
    ///     </para>
    ///     <para>
    ///     You'll use the <see cref="CallLocalActivityAsync{TActivity}(byte[], LocalActivityOptions, CancellationToken?)"/>,
    ///     specifying your custom <see cref="ActivityBase"/> implementation.
    ///     </para>
    ///     <note>
    ///     Local activity types do not need to be registered with a Cadence worker.
    ///     </note>
    ///     <para>
    ///     Local activities have some limitations:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///         Local activities cannot record Cadence heartbeats.
    ///         </item>
    ///         <item>
    ///         Local activity timeouts should be shorter than the decision task timeout
    ///         of the calling workflow.
    ///         </item>
    ///         <item>
    ///         The .NET Cadence client does not currently support cancellation of local activities.
    ///         </item>
    ///     </list>
    /// </item>
    /// <item>
    ///     <para>
    ///     Workflow instances can be signalled when external events occur via the 
    ///     <see cref="CadenceClient.SignalWorkflowAsync(string, string, byte[], string)"/> or
    ///     <see cref="CadenceClient.SignalWorkflowAsync(string, string, byte[], byte[], string, WorkflowOptions)"/>
    ///     methods.  Signals are identified by a string name and may include a byte
    ///     array payload.  Workflows receive signals by implementing a receive method
    ///     accepting a byte array payload parameter and tagging the method with a
    ///     <see cref="SignalHandlerAttribute"/> specifying the signal name, like:
    ///     </para>
    ///     <code language="c#">
    ///     [SignalHandler("my-signal")]
    ///     protected async Task OnMySignal(byte[] args)
    ///     {
    ///         await DoDomethingAsync();
    ///     }
    ///     </code>
    ///     <note>
    ///     Exceptions thrown by signal handlers are caught and logged but are not
    ///     returned to the signaller.
    ///     </note>
    /// </item>
    /// <item>
    ///     <para>
    ///     Running workflows can also be queried via <see cref="CadenceClient.QueryWorkflowAsync(string, string, byte[], string)"/>.
    ///     Queries are identified by a name and may include optional arguments encoded 
    ///     as a byte array and return a result encoded as a byte array or <c>null</c>.
    ///     Workflows receive queries by implementing a receive method accepting the
    ///     query arguments as a byte array that returns the byte array result.  You'll
    ///     need to tag this with a <see cref="QueryHandlerAttribute"/> specifying the
    ///     query name, like:
    ///     </para>
    ///     <code language="c#">
    ///     [QueryHandler("my-query")]
    ///     protected async Task&lt;byte[]&gt; OnMyQuery(byte[] args)
    ///     {
    ///         return await Task.FromResult(Encoding.UTF8.GetBytes("Hello World!"));
    ///     }
    ///     </code>
    ///     <note>
    ///     Exceptions thrown by query handlers are caught and will be returned to 
    ///     the caller to be thrown as an exception.
    ///     </note>
    /// </item>
    /// </list>
    /// </remarks>
    public abstract class WorkflowBase : INeonLogger
    {
        //---------------------------------------------------------------------
        // Static members

        private static object                               syncLock           = new object();
        private static INeonLogger                          log                = LogManager.Default.GetLogger<WorkflowBase>();
        private static SemanticVersion                      zeroVersion        = SemanticVersion.Create(0);
        private static Dictionary<string, Type>             nameToWorkflowType = new Dictionary<string, Type>();
        private static Dictionary<long, WorkflowBase>       idToWorkflow       = new Dictionary<long, WorkflowBase>();
        private static Dictionary<Type, WorkflowMethodMap>  typeToMethodMap    = new Dictionary<Type, WorkflowMethodMap>();

        /// <summary>
        /// Registers a workflow type.
        /// </summary>
        /// <typeparam name="TWorkflow">The workflow implementation type.</typeparam>
        /// <param name="workflowTypeName">The name used to identify the implementation.</param>
        internal static void Register<TWorkflow>(string workflowTypeName)
            where TWorkflow : WorkflowBase
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            Covenant.Requires<ArgumentException>(typeof(TWorkflow) != typeof(WorkflowBase), $"The base [{nameof(WorkflowBase)}] class cannot be registered.");

            lock (syncLock)
            {
                nameToWorkflowType[workflowTypeName] = typeof(TWorkflow);
            }
        }

        /// <summary>
        /// Returns the .NET type implementing the named Cadence workflow.
        /// </summary>
        /// <param name="workflowType">The Cadence workflow type string.</param>
        /// <returns>The workflow .NET type or <c>null</c> if the type was not found.</returns>
        private static Type GetWorkflowType(string workflowType)
        {
            Covenant.Requires<ArgumentNullException>(workflowType != null);

            lock (syncLock)
            {
                if (nameToWorkflowType.TryGetValue(workflowType, out var type))
                {
                    return type;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Called to handle a workflow related request message received from the cadence-proxy.
        /// </summary>
        /// <param name="client">The client that received the request.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        internal static async Task OnProxyRequestAsync(CadenceClient client, ProxyRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            ProxyReply reply;

            switch (request.Type)
            {
                case InternalMessageTypes.WorkflowInvokeRequest:

                    reply = await OnInvokeAsync(client, (WorkflowInvokeRequest)request);
                    break;

                case InternalMessageTypes.WorkflowSignalInvokeRequest:

                    reply = await OnSignalAsync((WorkflowSignalInvokeRequest)request);
                    break;

                case InternalMessageTypes.WorkflowQueryRequest:

                    reply = await OnQueryAsync((WorkflowQueryRequest)request);
                    break;

                case InternalMessageTypes.ActivityInvokeLocalRequest:

                    reply = await OnInvokeLocalActivity(client, (ActivityInvokeLocalRequest)request);
                    break;

                default:

                    throw new InvalidOperationException($"Unexpected message type [{request.Type}].");
            }

            await client.ProxyReplyAsync(request, reply);
        }

        /// <summary>
        /// Thread-safe method that maps a workflow ID to the corresponding workflow instance.
        /// </summary>
        /// <param name="contextId">The workflow's context ID.</param>
        /// <returns>The <see cref="WorkflowBase"/> instance or <c>null</c> if the workflow was not found.</returns>
        private static WorkflowBase GetWorkflow(long contextId)
        {
            lock (syncLock)
            {
                if (idToWorkflow.TryGetValue(contextId, out var workflow))
                {
                    return workflow;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Handles workflow invocation.
        /// </summary>
        /// <param name="client">The associated cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowInvokeReply> OnInvokeAsync(CadenceClient client, WorkflowInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            WorkflowBase    workflow;
            Type            workflowType;

            var contextId = request.ContextId;

            lock (syncLock)
            {
                if (idToWorkflow.TryGetValue(contextId, out workflow))
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"A workflow with [ID={contextId}] is already running on this worker.")
                    };
                }

                workflowType = GetWorkflowType(request.WorkflowType);

                if (workflowType == null)
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"A workflow [Type={request.WorkflowType}] is not registered for this worker.")
                    };
                }

            }

            workflow = (WorkflowBase)Activator.CreateInstance(workflowType);

            workflow.Initialize(client, contextId);

            lock (syncLock)
            {
                idToWorkflow.Add(contextId, workflow);
            }

            // We're going to record the workflow implementation version as a mutable
            // value and then obtain the value to set the [OriginalVersion] property.
            // The outcome will be that [OriginalVersion] will end up being set to the 
            // [Version] at the time the workflow instance was first invoked and [Version] 
            // will return the current version.
            //
            // This will give upgraded workflow implementations a chance to implement 
            // backwards compatibility for workflows already in flight.

            var version      = workflow.Version ?? zeroVersion;
            var versionBytes = await workflow.GetValueAsync("neon:original-version", Encoding.UTF8.GetBytes(version.ToString()));

            workflow.OriginalVersion = SemanticVersion.Parse(Encoding.UTF8.GetString(versionBytes));

            // Initialize the other workflow properties.

            workflow.Client           = client;
            workflow.contextId        = request.ContextId;
            workflow.Domain           = request.Domain;
            workflow.RunId            = request.RunId;
            workflow.TaskList         = request.TaskList;
            workflow.WorkflowId       = request.WorkflowId;
            workflow.WorkflowTypeName = request.WorkflowType;

            // Start the workflow by calling its [RunAsync(args)] method.  This method will
            // indicate that it has completed via one of these techniques:
            //
            //      1. The method returns normally with the workflow result.
            //
            //      2. The method calls [RestartAsync(result, args)] which throws an
            //         [InternalWorkflowRestartException] which will be caught and
            //         handled here.
            //
            //      3. The method throws another exception which will be caught
            //         and be used to indicate that the workflow failed.

            try
            {
                var result = await workflow.RunAsync(request.Args);

                return new WorkflowInvokeReply()
                {
                    Result = result
                };
            }
            catch (CadenceWorkflowRestartException e)
            {
                return new WorkflowInvokeReply()
                {
                    ContinueAsNew                             = true,
                    ContinueAsNewArgs                         = e.Args,
                    ContinueAsNewDomain                       = e.Domain,
                    ContinueAsNewTaskList                     = e.TaskList,
                    ContinueAsNewExecutionStartToCloseTimeout = CadenceHelper.ToCadence(e.ExecutionStartToCloseTimeout),
                    ContinueAsNewScheduleToCloseTimeout       = CadenceHelper.ToCadence(e.ScheduleToCloseTimeout),
                    ContinueAsNewScheduleToStartTimeout       = CadenceHelper.ToCadence(e.ScheduleToStartTimeout),
                    ContinueAsNewStartToCloseTimeout          = CadenceHelper.ToCadence(e.StartToCloseTimeout),
                };
            }
            catch (CadenceException e)
            {
                return new WorkflowInvokeReply()
                {
                    Error = e.ToCadenceError()
                };
            }
            catch (Exception e)
            {
                return new WorkflowInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        /// <summary>
        /// Handles workflow signals.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowSignalReply> OnSignalAsync(WorkflowSignalInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(request.ContextId);

                if (workflow != null)
                {
                    var method = workflow.methodMap.GetSignalMethod(request.SignalName);

                    if (method != null)
                    {
                        await (Task)(method.Invoke(workflow, new object[] { request.SignalArgs }));

                        return new WorkflowSignalReply()
                        {
                            ContextId = request.ContextId
                        };
                    }
                    else
                    {
                        return new WorkflowSignalReply()
                        {
                            Error = new CadenceEntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a signal handler for [signalName={request.SignalName}].").ToCadenceError()
                        };
                    }
                }
                else
                {
                    return new WorkflowSignalReply()
                    {
                        Error = new CadenceEntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToCadenceError()
                    };
                }
            }
            catch (Exception e)
            {
                return new WorkflowSignalReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        /// <summary>
        /// Handles workflow queries.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowQueryReply> OnQueryAsync(WorkflowQueryRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(request.ContextId);

                if (workflow != null)
                {
                    var method = workflow.methodMap.GetQueryMethod(request.QueryName);

                    if (method != null)
                    {
                        var result = await (Task<byte[]>)(method.Invoke(workflow, new object[] { request.QueryArgs }));

                        return new WorkflowQueryReply()
                        {
                            ContextId = request.ContextId,
                            Result    = result
                        };
                    }
                    else
                    {
                        return new WorkflowQueryReply()
                        {
                            Error = new CadenceEntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a query handler for [queryName={request.QueryName}].").ToCadenceError()
                        };
                    }
                }
                else
                {
                    return new WorkflowQueryReply()
                    {
                        Error = new CadenceEntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToCadenceError()
                    };
                }
            }
            catch (Exception e)
            {
                return new WorkflowQueryReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        /// <summary>
        /// Handles workflow local activity invocations.
        /// </summary>
        /// <param name="client">The client the request was received from.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<ActivityExecuteLocalReply> OnInvokeLocalActivity(CadenceClient client, ActivityInvokeLocalRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(request.ContextId);

                if (workflow != null)
                {
                    Type activityType;

                    lock (syncLock)
                    {
                        if (!workflow.idToLocalActivityType.TryGetValue(request.ActivityTypeId, out activityType))
                        {
                            return new ActivityExecuteLocalReply()
                            {
                                Error = new CadenceEntityNotExistsException($"Activity type does not exist for [activityTypeId={request.ActivityTypeId}].").ToCadenceError()
                            };
                        }
                    }

                    var workerArgs = new WorkerArgs() { Client = client, ContextId = request.ActivityContextId };
                    var activity   = ActivityBase.Create(activityType, client, null);
                    var result     = await activity.OnRunAsync(request.Args);

                    return new ActivityExecuteLocalReply()
                    {
                        Result = result
                    };
                }
                else
                {
                    return new ActivityExecuteLocalReply()
                    {
                        Error = new CadenceEntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToCadenceError()
                    };
                }
            }
            catch (Exception e)
            {
                return new ActivityExecuteLocalReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private long                        contextId;
        private WorkflowMethodMap           methodMap;
        private Dictionary<long, Type>      idToLocalActivityType;
        private long                        nextLocalActivityTypeId;
        private bool                        isDisconnected;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public WorkflowBase()
        {
        }

        /// <summary>
        /// Called internally to initialize the workflow.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="contextId">The workflow's context ID.</param>
        internal void Initialize(CadenceClient client, long contextId)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            this.Client                = client;
            this.contextId             = contextId;
            this.idToLocalActivityType = new Dictionary<long, Type>();

            // Generate the signal/query method map for the workflow type if we
            // haven't already done that for this workflow type.

            var workflowType = this.GetType();

            lock (syncLock)
            {
                if (!typeToMethodMap.TryGetValue(workflowType, out methodMap))
                {
                    methodMap = WorkflowMethodMap.Create(workflowType);

                    typeToMethodMap.Add(workflowType, methodMap);
                }
            }

            // Register the query handlers with Cadence.

            foreach (var queryName in methodMap.GetQueryNames())
            {
                SetQueryHandlerAsync(queryName).Wait();
            }

            // Register the signal handlers with Cadence.

            foreach (var signalName in methodMap.GetSignalNames())
            {
                SignalSubscribeAsync(signalName).Wait();
            }
        }

        /// <summary>
        /// Returns the <see cref="CadenceClient"/> managing this workflow.
        /// </summary>
        public CadenceClient Client { get; private set; }

        /// <summary>
        /// Returns the version of the workflow implementation that was executed when
        /// the workflow was first started.  This can be used to to implement workflow
        /// backwards compatability.
        /// </summary>
        public SemanticVersion OriginalVersion { get; private set; }

        /// <summary>
        /// Workflow implemenations that support version backwards compatability should
        /// override this to return the version of the implementation.  This returns
        /// <c>SemanticVersion(0, 0, 0)</c> by default.
        /// </summary>
        public virtual SemanticVersion Version => zeroVersion;

        /// <summary>
        /// Returns the domain hosting the workflow.
        /// </summary>
        public string Domain { get; private set; }

        /// <summary>
        /// Returns the original workflow ID.
        /// </summary>
        public string WorkflowId { get; private set; }

        /// <summary>
        /// Returns the workflow's current run ID.
        /// </summary>
        public string RunId { get; private set; }

        /// <summary>
        /// Returns the workflow type name.
        /// </summary>
        public string WorkflowTypeName { get; private set; }

        /// <summary>
        /// Returns the tasklist where the workflow is executing.
        /// </summary>
        public string TaskList { get; private set; }

        /// <summary>
        /// Called by Cadence to execute a workflow.  Derived classes will need to implement
        /// their workflow logic here.
        /// </summary>
        /// <param name="args">The workflow arguments encoded into a byte array or <c>null</c>.</param>
        /// <returns>The workflow result encoded as a byte array or <c>null</c>.</returns>
        /// <remarks>
        /// <para>
        /// There a several Cadence restrictions you need to keep in mind when implementing
        /// your workflow logic.  These are necessary so that Cadence will be able to
        /// transparently and deterministically replay previously completed workflow steps
        /// when workflows need to be restarted due to failures or other reasons.
        /// </para>
        /// <note>
        /// <para>
        /// Workflow logic must be deterministic and idempotent:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     <para>
        ///     The code in your <see cref="RunAsync(byte[])"/> method must only rely on
        ///     state and data returned by Cadence methods for determining what to do.
        ///     This allows Cadence to replay previously completed workfow steps when
        ///     a workflow needs to be rescheduled on another worker.
        ///     </para>
        ///     <para>
        ///     This means that you must not call things like <see cref="DateTime.UtcNow"/>
        ///     directly in your workflow because this will likely return a different 
        ///     value every time it's called.  Instead, call  
        ///     </para>
        ///     </item>
        ///     <item>
        ///     <para>
        ///     Workflows are inherently single threaded.  You should never explicitly
        ///     create threads within <see cref="RunAsync(byte[])"/> or use things like
        ///     <see cref="Task.Run(Action)"/> which schedule work on background threads.
        ///     </para>
        ///     <note>
        ///     Workflows are allowed to run multiple activities in parallel and activities
        ///     can be multi-threaded, it's just the workflow code itself that can't use
        ///     threads because those will generally interfere with Cadence's ability to
        ///     replay workflow steps deterministically.
        ///     </note>
        ///     </item>
        ///     <item>
        ///     Workflows must never obtain the current time by using methods like 
        ///     <see cref="DateTime.UtcNow"/> directly.  Use <see cref="UtcNowAsync"/>
        ///     instead.
        ///     </item>
        ///     <item>
        ///     Workflows should never directly query the environment where the workflow 
        ///     code is currently running.  This includes things like environment variables,
        ///     the machine host name or IP address, local files, etc.  You should generally
        ///     use activities for this or obtain this indirectly state via
        ///     <see cref="GetValueAsync(string, byte[], bool)"/>.  Both of these mechanisms will
        ///     ensure that Cadence can record the state in the workflow history so that it
        ///     can be replayed if the workflow needs to be rescheduled.
        ///     </item>
        ///     <item>
        ///     Workflows should never obtain things like random numbers or UUIDs 
        ///     directly since these operations are implicitly are non-deterministic 
        ///     because they'll return different values every time.  You'll need to
        ///     use  <see cref="GetValueAsync(string, byte[], bool)"/>
        ///     with a custom function for these as well or use activities, to ensure
        ///     that the results are recorded in the workflow history.
        ///     </item>
        ///     <item>
        ///     Workflows should never call <see cref="Thread.Sleep(TimeSpan)"/> or 
        ///     <see cref="Task.Delay(TimeSpan)"/>.  Use <see cref="SleepAsync(TimeSpan)"/>
        ///     instead.
        ///     </item>
        /// </list>
        /// </note>
        /// </remarks>
        protected abstract Task<byte[]> RunAsync(byte[] args);

        /// <summary>
        /// Registers a query handler with Cadence.
        /// </summary>
        /// <param name="queryName">The query name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task SetQueryHandlerAsync(string queryName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryName));

            var reply = (WorkflowSetQueryHandlerReply)await Client.CallProxyAsync(
                new WorkflowSetQueryHandlerRequest()
                {
                    QueryName = queryName
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Subscribes a workflow to a signal.
        /// </summary>
        /// <param name="signalName">The signal name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task SignalSubscribeAsync(string signalName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));

            var reply = (WorkflowSignalSubscribeReply)await Client.CallProxyAsync(
                new WorkflowSignalSubscribeRequest()
                {
                    SignalName = signalName
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Returns <c>true</c> if there is a completion result from previous runs of
        /// this workflow.  This is useful for CRON workflows that would like to pass
        /// ending state from from one workflow run to the next.  This property
        /// indicates whether the last run (if any) returned any state.
        /// </summary>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<bool> HasPreviousRunResultAsync()
        {
            var reply = (WorkflowHasLastResultReply)await Client.CallProxyAsync(
                new WorkflowHasLastResultRequest()
                {
                    ContextId = contextId
                });

            reply.ThrowOnError();

            return reply.HasResult;
        }

        /// <summary>
        /// Returns the result from the last workflow run or <c>null</c>.  This is useful 
        /// for CRON workflows that would like to pass information from from one workflow
        /// run to the next.
        /// </summary>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<byte[]> GetPreviousRunResultAsync()
        {
            var reply = (WorkflowGetLastLastReply)await Client.CallProxyAsync(
                new WorkflowGetLastResultRequest()
                {
                    ContextId = contextId
                });

            reply.ThrowOnError();

            return reply.Result;
        }

        /// <summary>
        /// Called when a workflow has been cancelled and additional cleanup related work
        /// must be performed.  Calling this method allows the workflow to continue
        /// executing activities after the parent workflow has been cancelled.
        /// </summary>
        /// <remarks>
        /// Under the covers, this replaces the underlying workflow context with
        /// a new disconnected context that is independent from the parent workflow
        /// context.  This method only substitutes the new context for the first call. 
        /// Subsequent calls won't actually do anything.
        /// </remarks>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task DisconnectContextAsync()
        {
            if (isDisconnected)
            {
                // Already disconnected.

                return;
            }

            var reply = (WorkflowDisconnectContextReply)await Client.CallProxyAsync(
                new WorkflowDisconnectContextRequest()
                {
                    ContextId = contextId
                });

            reply.ThrowOnError();

            isDisconnected = true;
        }

        /// <summary>
        /// Returns the current time (UTC).
        /// </summary>
        /// <returns>The current workflow time (UTC).</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<DateTime> UtcNowAsync()
        {
            var reply = (WorkflowGetTimeReply)await Client.CallProxyAsync(
                new WorkflowGetTimeRequest()
                {
                    ContextId = contextId
                });

            reply.ThrowOnError();

            return reply.Time;
        }

        /// <summary>
        /// Use this when your workflow needs to obtain a value that 
        /// may change at runtime.  You'll pass a string identifying the
        /// value along with the current value and the method will return
        /// the first value set for the value identifier during the workflow
        /// execution.
        /// </summary>
        /// <param name="valueId">Identifies the value.</param>
        /// <param name="value">The value encoded as a byte array or <c>null</c>.</param>
        /// <param name="update">
        /// Optionally indicates that the new value should be persisted to
        /// the workflow history.  This defaults to <c>false</c>.
        /// </param>
        /// <returns>The requested value as a byte array or <c>null</c>.</returns>
        /// <remarks>
        /// <note>
        /// This combines the functionality of the <b>SideEffect()</b> and
        /// <b>MutableSideEffect()</b> context functions provided by the GOLANG
        /// client.
        /// </note>
        /// <para>
        /// For example, a workflow step may require a random number
        /// when making a decision.  In this case, the workflow would
        /// call <see cref="GetValueAsync"/>, passing the generated
        /// random number.
        /// </para>
        /// <para>
        /// By default, the first time the method is executed in a workflow, 
        /// the random value passed will be persisted to the workflow history
        /// and also returned by <see cref="GetValueAsync(string, byte[], bool)"/>. 
        /// Then,  if the workflow needs to be replayed or this method is called 
        /// later on during workflow execution the random number will be returned
        /// from the history rather than using the new value passed.  This ensures 
        /// that the original random number would be returned resulting in the
        /// same decision being made during the replay.  This is equivalant to
        /// the GOLANG client's <b>SideEffect()</b> function, but using an ID
        /// to identify the value.
        /// </para>
        /// <para>
        /// You can also persist new values by passing <paramref name="update"/><c>=true</c>.
        /// This is very close to being equivalent to the GOLANG client's <b>MutableSideEffect()</b>
        /// function. 
        /// </para>
        /// </remarks>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<byte[]> GetValueAsync(string valueId, byte[] value, bool update = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(valueId));

            var reply = (WorkflowMutableReply)await Client.CallProxyAsync(
                new WorkflowMutableRequest()
                {
                    ContextId = this.contextId,
                    MutableId = valueId,
                    Result    = value,
                    Update    = update
                });

            reply.ThrowOnError();

            return reply.Result;
        }

        /// <summary>
        /// Pauses the workflow for at least the period specified.
        /// </summary>
        /// <param name="duration">The time to sleep.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation was cancelled.</exception>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task SleepAsync(TimeSpan duration)
        {
            var reply = (WorkflowSleepReply)await Client.CallProxyAsync(
                new WorkflowSleepRequest()
                {
                    ContextId = contextId,
                    Duration  = duration
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Pauses the workflow at least until the specified time UTC.
        /// </summary>
        /// <param name="wakeTimeUtc">The time to sleep.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation was cancelled.</exception>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task SleepUntilUtcAsync(DateTime wakeTimeUtc)
        {
            var utcNow   = await UtcNowAsync();
            var duration = wakeTimeUtc - utcNow;

            if (duration <= TimeSpan.Zero)
            {
                // We're already at or past the requested time.

                return;
            }

            var reply = (WorkflowSleepReply)await Client.CallProxyAsync(
                new WorkflowSleepRequest()
                {
                    ContextId = contextId,
                    Duration  = duration
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Executes a child workflow and waits for it to complete.
        /// </summary>
        /// <param name="args">Optionally specifies the workflow arguments.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The workflow result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<byte[]> CallChildWorkflowAsync<TWorkflow>(byte[] args = null, ChildWorkflowOptions options = null, CancellationToken? cancellationToken = null)
            where TWorkflow : WorkflowBase
        {
            return await CallChildWorkflowAsync(typeof(TWorkflow).FullName, args, options, cancellationToken);
        }

        /// <summary>
        /// Executes a child workflow by workflow type name and waits for it to complete.
        /// </summary>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="args">Optionally specifies the workflow arguments.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The workflow result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<byte[]> CallChildWorkflowAsync(string workflowTypeName, byte[] args = null, ChildWorkflowOptions options = null, CancellationToken? cancellationToken = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));

            var reply = (WorkflowExecuteChildReply)await Client.CallProxyAsync(
                new WorkflowExecuteChildRequest()
                {
                    Workflow = workflowTypeName,
                    Args     = args,
                    Options  = options?.ToInternal()
                },
                cancellationToken: cancellationToken);

            reply.ThrowOnError();

            if (cancellationToken != null)
            {
                cancellationToken.Value.Register(
                    () =>
                    {
                        Client.CallProxyAsync(new WorkflowCancelChildRequest() { ChildId = reply.ChildId }).Wait();
                    });
            }

            var reply2 = (WorkflowWaitForChildReply)await Client.CallProxyAsync(
                new WorkflowWaitForChildRequest()
                {
                    ChildId = reply.ChildId
                });

            reply2.ThrowOnError();

            return reply2.Result;
        }

        /// <summary>
        /// Starts a child workflow by type but does not wait for it to complete.
        /// </summary>
        /// <param name="args">Optionally specifies the workflow arguments.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// Returns an opaque <see cref="ChildWorkflow"/> that identifies the new workflow
        /// and that can be passed to <see cref="WaitForChildWorkflowAsync(ChildWorkflow, CancellationToken?)"/>,
        /// <see cref="SignalChildWorkflowAsync(ChildWorkflow, string, byte[])"/> and
        /// <see cref="CancelChildWorkflowAsync(ChildWorkflow)"/>.
        /// </returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<ChildWorkflow> StartChildWorkflowAsync<TWorkflow>(byte[] args = null, ChildWorkflowOptions options = null, CancellationToken? cancellationToken = null)
            where TWorkflow : WorkflowBase
        {
            return await StartChildWorkflowAsync(typeof(TWorkflow).FullName, args, options, cancellationToken);
        }

        /// <summary>
        /// Starts a child workflow by type name but does not wait for it to complete.
        /// </summary>
        /// <param name="name">The workflow name.</param>
        /// <param name="args">Optionally specifies the workflow arguments.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// Returns an opaque <see cref="ChildWorkflow"/> that identifies the new workflow
        /// and that can be passed to <see cref="WaitForChildWorkflowAsync(ChildWorkflow, CancellationToken?)"/>,
        /// <see cref="SignalChildWorkflowAsync(ChildWorkflow, string, byte[])"/> and
        /// <see cref="CancelChildWorkflowAsync(ChildWorkflow)"/>.
        /// </returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<ChildWorkflow> StartChildWorkflowAsync(string name, byte[] args = null, ChildWorkflowOptions options = null, CancellationToken? cancellationToken = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            var reply = (WorkflowExecuteChildReply)await Client.CallProxyAsync(
                new WorkflowExecuteChildRequest()
                {
                    Args    = args,
                    Options = options?.ToInternal()
                },
                cancellationToken: cancellationToken);

            reply.ThrowOnError();

            return new ChildWorkflow(reply.ChildId);
        }

        /// <summary>
        /// <para>
        /// Signals a child workflow.
        /// </para>
        /// <note>
        /// This method blocks until the child workflow is scheduled and
        /// actually started on a worker.
        /// </note>
        /// </summary>
        /// <param name="childWorkflow">
        /// Identifies the child workflow (this is returned by 
        /// <see cref="StartChildWorkflowAsync(string, byte[], ChildWorkflowOptions, CancellationToken?)"/>).
        /// </param>
        /// <param name="signalName">Specifies the signal name.</param>
        /// <param name="signalArgs">Optionally specifies the signal arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task SignalChildWorkflowAsync(ChildWorkflow childWorkflow, string signalName, byte[] signalArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));

            var reply = (WorkflowSignalChildReply)await Client.CallProxyAsync(
                new WorkflowSignalChildRequest()
                {
                    ChildId    = childWorkflow.Id,
                    SignalName = signalName,
                    SignalArgs = signalArgs
                }); ;

            reply.ThrowOnError();
        }

        /// <summary>
        /// Cancels a child workflow.
        /// </summary>
        /// <param name="childWorkflow">
        /// Identifies the child workflow (this is returned by 
        /// <see cref="StartChildWorkflowAsync(string, byte[], ChildWorkflowOptions, CancellationToken?)"/>).
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task CancelChildWorkflowAsync(ChildWorkflow childWorkflow)
        {
            var reply = (WorkflowCancelChildReply)await Client.CallProxyAsync(
                new WorkflowCancelChildRequest()
                {
                    ChildId = childWorkflow.Id
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Waits for a child workflow to complete.
        /// </summary>
        /// <param name="childWorkflow">
        /// Identifies the child workflow (this is returned by 
        /// <see cref="StartChildWorkflowAsync(string, byte[], ChildWorkflowOptions, CancellationToken?)"/>).
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The workflow results.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<byte[]> WaitForChildWorkflowAsync(ChildWorkflow childWorkflow, CancellationToken? cancellationToken = null)
        {
            if (cancellationToken != null)
            {
                cancellationToken.Value.Register(
                    () =>
                    {
                        CancelChildWorkflowAsync(childWorkflow).Wait();
                    });
            }

            var reply = (WorkflowWaitForChildReply)await Client.CallProxyAsync(
                new WorkflowWaitForChildRequest()
                {
                    ChildId = childWorkflow.Id
                });

            reply.ThrowOnError();

            return reply.Result;
        }

        /// <summary>
        /// Executes an activity and waits for it to complete.
        /// </summary>
        /// <param name="args">Optionally specifies the activity arguments.</param>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The activity result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<byte[]> CallActivityAsync<TActivity>(byte[] args = null, ActivityOptions options = null, CancellationToken? cancellationToken = null)
            where TActivity : ActivityBase
        {
            return await CallActivityAsync(typeof(TActivity).FullName, args, options, cancellationToken);
        }

        /// <summary>
        /// Executes an activity with a specific activity type name and waits for it to complete.
        /// </summary>
        /// <param name="activityTypeName">Identifies the activity.</param>
        /// <param name="args">Optionally specifies the activity arguments.</param>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The activity result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<byte[]> CallActivityAsync(string activityTypeName, byte[] args = null, ActivityOptions options = null, CancellationToken? cancellationToken = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName));

            options = options ?? new ActivityOptions();

            var reply = (ActivityExecuteReply)await Client.CallProxyAsync(
                new ActivityExecuteRequest()
                {
                    Activity = activityTypeName,
                    Args     = args,
                    Options  = options.ToInternal()
                });

            reply.ThrowOnError();

            return reply.Result;
        }

        /// <summary>
        /// Executes a local activity and waits for it to complete.
        /// </summary>
        /// <typeparam name="TActivity">Specifies the local activity implementation type.</typeparam>
        /// <param name="args">Optionally specifies the activity arguments.</param>
        /// <param name="options">Optionally specifies any local activity options.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The activity result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <remarks>
        /// This method can be used to optimize activities that will complete quickly
        /// (within seconds).  Rather than scheduling the activity on any worker that
        /// has registered an implementation for the activity, this method will simply
        /// instantiate an instance of <typeparamref name="TActivity"/> and call its
        /// <see cref="ActivityBase.RunAsync(byte[])"/> method.
        /// </remarks>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<byte[]> CallLocalActivityAsync<TActivity>(byte[] args = null, LocalActivityOptions options = null, CancellationToken? cancellationToken = null)
            where TActivity : ActivityBase
        {
            options = options ?? new LocalActivityOptions();

            // We need to register the local activity type with a workflow local ID
            // that we can sent to [cadence-proxy] in the [ActivityExecuteLocalRequest]
            // such that the proxy can send it back to us in the [ActivityInvokeLocalRequest]
            // so we'll know which activity type to instantate and run.

            var activityTypeId = Interlocked.Increment(ref nextLocalActivityTypeId);

            lock (syncLock)
            {
                idToLocalActivityType.Add(activityTypeId, typeof(TActivity));
            }

            try
            {
                var reply = (ActivityExecuteLocalReply)await Client.CallProxyAsync(
                    new ActivityExecuteLocalRequest()
                    {
                        ActivityTypeId = activityTypeId,
                        Args           = args,
                        Options        = options.ToInternal()
                    });

                reply.ThrowOnError();

                return reply.Result;
            }
            finally
            {
                // Remove the activity type mapping to prevent memory leaks.

                lock (syncLock)
                {
                    idToLocalActivityType.Remove(activityTypeId);
                }
            }
        }

        /// <summary>
        /// Exits and completes the current running workflow and then restarts it, passing the
        /// optional workflow arguments.
        /// </summary>
        /// <param name="args">Optional arguments for the new run.</param>
        /// <param name="domain">Optional domain for the new run.</param>
        /// <param name="taskList">Optional tasklist for the new run.</param>
        /// <param name="executionToStartTimeout">Optional execution to start timeout for the new run.</param>
        /// <param name="scheduleToCloseTimeout">Optional schedule to close timeout for the new run.</param>
        /// <param name="scheduleToStartTimeout">Optional schedule to start timeout for the new run.</param>
        /// <param name="startToCloseTimeout">Optional start to close timeout for the new run.</param>
        /// <param name="retryPolicy">Optional retry policy for the new run.</param>
        /// <remarks>
        /// This works by throwing a <see cref="CadenceWorkflowRestartException"/> that will be
        /// caught and handled by the base <see cref="WorkflowBase"/> class.    You'll need to allow
        /// this exception to exit your <see cref="RunAsync(byte[])"/> method for this to work.
        /// </remarks>
        protected async Task RestartAsync(
            byte[]              args                    = null,
            string              domain                  = null,
            string              taskList                = null,
            TimeSpan            executionToStartTimeout = default,
            TimeSpan            scheduleToCloseTimeout  = default,
            TimeSpan            scheduleToStartTimeout  = default,
            TimeSpan            startToCloseTimeout     = default,
            CadenceRetryPolicy  retryPolicy             = null)
        {
            // This method doesn't currently do any async operations but I'd
            // like to keep the method signature async just in case this changes
            // in the future.

            await Task.CompletedTask;

            // We're going to throw a [InternalWorkflowRestartException] with the
            // parameters.  This exception will be caught and handled by the 
            // [WorkflowInvoke()] method which will configure the reply such
            // that the cadence-proxy will be able to signal Cadence to continue
            // the workflow with a clean history.

            throw new CadenceWorkflowRestartException(
                args:                       args,
                domain:                     domain,
                taskList:                   taskList,
                executionToStartTimeout:    executionToStartTimeout,
                scheduleToCloseTimeout:     scheduleToCloseTimeout,
                scheduleToStartTimeout:     scheduleToStartTimeout,
                startToCloseTimeout:        startToCloseTimeout,
                retryPolicy:                retryPolicy);
        }

        //---------------------------------------------------------------------
        // Logging implementation

        // $todo(jeff.lill): Implement these.
        //
        // Note that these calls are all synchronous.  Perhaps we should consider dumping
        // the [INeonLogger] implementations in favor of simpler async methods?

        /// <inheritdoc/>
        public bool IsLogDebugEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogSInfoEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogInfoEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogWarnEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogErrorEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogSErrorEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogCriticalEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogLevelEnabled(LogLevel logLevel)
        {
            return false;
        }

        /// <inheritdoc/>
        public void LogDebug(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSInfo(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogInfo(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogWarn(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSError(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogError(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogCritical(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogDebug(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSInfo(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogInfo(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogWarn(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogError(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSError(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogCritical(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, IEnumerable<string> textFields, IEnumerable<double> numFields)
        {
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, params string[] textFields)
        {
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, params double[] numFields)
        {
        }
    }
}
