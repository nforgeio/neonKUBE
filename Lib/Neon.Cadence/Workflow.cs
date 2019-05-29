//-----------------------------------------------------------------------------
// FILE:	    Workflow.cs
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

namespace Neon.Cadence
{
    /// <summary>
    /// Base class for all application Cadence workflow implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Workflows are pretty easy to implement.  You'll need to derive your custom
    /// workflow class from <see cref="Workflow"/> and implement a public constructor
    /// with a single <see cref="WorkerArgs"/> parameter and have your
    /// constructor call the corresponding base <see cref="Workflow(WorkerArgs)"/>)
    /// constructor to initialize the instance.  You'll also need to implement the
    /// <see cref="RunAsync(byte[])"/> method, which is where your workflow logic
    /// will reside.  
    /// </para>
    /// <para>
    /// Here's an overview describing the steps necessary to implement, deploy, and
    /// start a workflow:
    /// </para>
    /// <list type="number">
    /// <item>
    ///     A custom workflow is implemented by deriving a class from <see cref="Workflow"/>,
    ///     implementing the workflow logic via a <see cref="Workflow.RunAsync(byte[])"/>
    ///     method.  Any custom workflow activities will need to be implemented as classes
    ///     derived from <see cref="Activity"/>.
    /// </item>
    /// <item>
    ///     <para>
    ///     The custom <see cref="Workflow"/> class needs to be deployed as a service or
    ///     application that creates a <see cref="CadenceClient"/> connected to a Cadence
    ///     cluster.  This application needs to call <see cref="CadenceClient.StartWorkflowWorkerAsync{TWorkflow}(string, string, WorkerOptions, string)"/>
    ///     and <see cref="CadenceClient.StartActivityWorkerAsync{TActivity}(string, string, WorkerOptions, string)"/> to
    ///     start the workflow and activity workers as required.
    ///     </para>
    ///     <note>
    ///     By default, both workflow and activity workers will be registered using the
    ///     fully qualified name of the custom <see cref="Workflow"/> or <see cref="Activity"/>
    ///     derived implementation classes.  These names can be customized as required.
    ///     </note>
    /// </item>
    /// <item>
    ///     <para>
    ///     A global workflow instance can be started by calling <see cref="CadenceClient.StartWorkflowAsync(string, string, byte[], WorkflowOptions)"/>,
    ///     passing an optional byte array as workflow arguments as well as optional workflow options.  
    ///     Global workflows have no parent, as opposed to child workflows that run in the context of 
    ///     another workflow (the parent).
    ///     </para>
    ///     <note>
    ///     <see cref="CadenceClient.StartWorkflowAsync(string, string, byte[], WorkflowOptions)"/> returns immediately
    ///     after the new workflow has been submitted to Cadence.  This method does not wait
    ///     for the workflow to finish.
    ///     </note>
    /// </item>
    /// <item>
    ///     For Neon Cadence client instances that have started a worker that handles the named workflow,
    ///     Cadence will choose one of the workers and begin executing the workflow there.  The Neon Cadence
    ///     client will instantiate the registered custom <see cref="Workflow"/> call its
    ///     <see cref="Workflow.RunAsync(byte[])"/> method, passing the optional workflow arguments
    ///     encoded as a byte array.
    /// </item>
    /// <item>
    ///     <para>
    ///     The custom <see cref="Workflow.RunAsync(byte[])"/> method implements the workflow by
    ///     calling activities via <see cref="CallActivity(string, byte[])"/> or <see cref="CallLocalActivity{TActivity}(byte[], LocalActivityOptions)"/> 
    ///     and child workflows via <see cref="CallWorkflow(string, byte[], ChildWorkflowOptions, CancellationToken)"/>,
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
    ///     Workflow instances can be signalled when external events occur via the 
    ///     <see cref="CadenceClient.SignalWorkflow(string, string, byte[], string)"/> or
    ///     <see cref="CadenceClient.SignalWorkflow(string, WorkflowOptions, string, byte[], byte[])"/>
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
    ///     Running workflows can also be queried via <see cref="CadenceClient.QueryWorkflow(string, string, byte[], string)"/>.
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
    public abstract class Workflow : INeonLogger
    {
        //---------------------------------------------------------------------
        // Static members

        private static object                               syncLock        = new object();
        private static INeonLogger                          log             = LogManager.Default.GetLogger<Workflow>();
        private static SemanticVersion                      zeroVersion     = new SemanticVersion();
        private static Dictionary<long, Workflow>           idToWorkflow    = new Dictionary<long, Workflow>();
        private static Dictionary<Type, WorkflowMethodMap>  typeToMethodMap = new Dictionary<Type, WorkflowMethodMap>();

        /// <summary>
        /// Called to handle a workflow related request message received from the cadence-proxy.
        /// </summary>
        /// <param name="client">The client that received the request.</param>
        /// <param name="request">The workflow request message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        internal static async Task OnProxyRequestAsync(CadenceClient client, WorkflowRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            ProxyReply reply;

            switch (request.Type)
            {
                case InternalMessageTypes.WorkflowInvokeRequest:

                    reply = await InvokeAsync(client, (WorkflowInvokeRequest)request);
                    break;

                case InternalMessageTypes.WorkflowSignalReceivedRequest:

                    reply = await SignalAsync((WorkflowSignalReceivedRequest)request);
                    break;

                case InternalMessageTypes.WorkflowQueryRequest:

                    reply = await QueryAsync((WorkflowQueryRequest)request);
                    break;

                case InternalMessageTypes.WorkflowMutableInvokeRequest:

                    reply = await MutableInvokeAsync((WorkflowMutableInvokeRequest)request);
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
        /// <returns>The <see cref="Workflow"/> instance or <c>null</c> if the workflow was not found.</returns>
        private static Workflow GetWorkflow(long contextId)
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
        internal static async Task<ProxyReply> InvokeAsync(CadenceClient client, WorkflowInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            Workflow workflow;

            lock (syncLock)
            {
                var contextId = request.ContextId;

                if (idToWorkflow.TryGetValue(contextId, out workflow))
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"A workflow with [ID={contextId}] is already running on this worker.")
                    };
                }

                var workflowType = client.GetWorkflowType(request.WorkflowType);

                if (workflowType == null)
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"A workflow [Type={request.WorkflowType}] is not registered for this worker.")
                    };
                }

                workflow = (Workflow)Activator.CreateInstance(workflowType);

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

            var versionBytes = await workflow.GetMutableValueAsync("neon:original-version",
                async () =>
                {
                    var version = workflow.Version ?? zeroVersion;

                    return await Task.FromResult(Encoding.UTF8.GetBytes(version.ToString()));
                });

            workflow.OriginalVersion = SemanticVersion.Parse(Encoding.UTF8.GetString(versionBytes));

            // Initialize the other workflow properties.

            workflow.Client          = client;
            workflow.contextId       = request.ContextId;
            workflow.Domain          = request.Domain;
            workflow.RunId           = request.RunId;
            workflow.TaskList        = request.TaskList;
            workflow.WorkflowId      = request.WorkflowId;
            workflow.WorkflowTimeout = request.Timeout;
            workflow.WorkflowType    = request.WorkflowType;

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
            catch (InternalWorkflowRestartException e)
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
        internal static async Task<ProxyReply> SignalAsync(WorkflowSignalReceivedRequest request)
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
        internal static async Task<ProxyReply> QueryAsync(WorkflowQueryRequest request)
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
        /// Handles workflow mutable value lookups.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<ProxyReply> MutableInvokeAsync(WorkflowMutableInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(request.ContextId);

                if (workflow != null)
                {
                    Func<Task<byte[]>> getter;

                    lock (workflow.idToMutableFunc)
                    {
                        if (!workflow.idToMutableFunc.TryGetValue(request.MutableId, out getter))
                        {
                            return new WorkflowMutableInvokeReply()
                            {
                                Error = new CadenceEntityNotExistsException($"Workflow mutable function callback does not exist for [mutableId={request.MutableId}].").ToCadenceError()
                            };
                        }
                    }

                    return new WorkflowMutableInvokeReply()
                    {
                        Result = await getter()
                    };
                }
                else
                {
                    return new WorkflowMutableInvokeReply()
                    {
                        Error = new CadenceEntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToCadenceError()
                    };
                }
            }
            catch (Exception e)
            {
                return new WorkflowMutableInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private long                                    contextId;
        private WorkflowMethodMap                       methodMap;
        private Dictionary<string, Func<Task<byte[]>>>  idToMutableFunc;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="args">The low-level worker initialization arguments.</param>
        protected Workflow(WorkerArgs args)
        {
            Covenant.Requires<ArgumentNullException>(args != null);

            this.Client          = args.Client;
            this.contextId       = args.ContextId;
            this.idToMutableFunc = new Dictionary<string, Func<Task<byte[]>>>();

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
        /// Returns the workflow type.
        /// </summary>
        public string WorkflowType { get; private set; }

        /// <summary>
        /// Returns the tasklist where the workflow is executing.
        /// </summary>
        public string TaskList { get; private set; }

        /// <summary>
        /// Returns the maximum time allowed for the workflow to complete.
        /// </summary>
        public TimeSpan WorkflowTimeout { get; private set; }

        /// <summary>
        /// Called by Cadence to execute a workflow.  Derived classes will need to implement
        /// their workflow logic here.
        /// </summary>
        /// <param name="args">The workflow arguments encoded into a byte array or <c>null</c>.</param>
        /// <returns>The workflow result encoded as a byte array or <c>null</c>.</returns>
        protected abstract Task<byte[]> RunAsync(byte[] args);

        /// <summary>
        /// Returns <c>true</c> if there is a completion result from previous runs of
        /// this workflow.  This is useful for CRON workflows that would like to pass
        /// ending state from from one workflow run to the next.  This property
        /// indicates whether the last run (if any) returned any state.
        /// </summary>
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
        protected async Task DisconnectContextAsync()
        {
            var reply = (WorkflowDisconnectContextReply)await Client.CallProxyAsync(
                new WorkflowDisconnectContextRequest()
                {
                    ContextId = contextId
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Returns the current time (UTC).
        /// </summary>
        /// <returns>The current workflow time (UTC).</returns>
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
        /// Use this when your workflow needs to obtain an external value that 
        /// may change at runtime.  When a workflow executes this for the first
        /// time, the <paramref name="getter"/> function will be called to
        /// fetch the value and persist it to the workflow history.  When
        /// the workflow is being replayed, the value from the history
        /// will be returned rather than calling the function again.
        /// </summary>
        /// <param name="mutableId">Identifies the mutable value.</param>
        /// <param name="getter">The value retrival function.</param>
        /// <returns>The requested value as a byte array or <c>null</c>.</returns>
        /// <remarks>
        /// <para>
        /// This mirrors the <b>MutableSideEffect</b> context function
        /// provided by the GOLANG client and is used to ensure that
        /// workflow replays will use the same values as the original
        /// execution.
        /// </para>
        /// <para>
        /// For example, a workflow step may require a random number
        /// when making a decision.  In this case, the workflow would
        /// call <see cref="GetMutableValueAsync"/>, passing a function
        /// that generates a random number.
        /// </para>
        /// <para>
        /// The first time the step is executed, the function will be called,
        /// a random number would be returned, be persisted to the history,
        /// and then to the workflow implementation which would use the 
        /// value when making a decision.  Then, if the workflow needs
        /// to be replayed, and this step is reached, the random number
        /// will be returned from the history rather than calling the 
        /// function again.  This ensures that the original random number
        /// would be returned resulting in the same decision being made
        /// during the replay.
        /// </para>
        /// </remarks>
        protected async Task<byte[]> GetMutableValueAsync(string mutableId, Func<Task<byte[]>> getter)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(mutableId));
            Covenant.Requires<ArgumentNullException>(getter != null);

            // We're going to persist the getter function here, keyed
            // by mutable ID so we'll be able to obtain it when the
            // cadence-proxy sends us the [WorkflowMutableInvokeRequest].

            lock (idToMutableFunc)
            {
                idToMutableFunc.Add(mutableId, getter);
            }

            var reply = (WorkflowMutableReply)await Client.CallProxyAsync(
                new WorkflowMutableRequest()
                {
                    ContextId = this.contextId,
                    MutableId = mutableId
                });

            lock (idToMutableFunc)
            {
                idToMutableFunc.Remove(mutableId);
            }

            reply.ThrowOnError();

            return reply.Result;
        }

        /// <summary>
        /// Pauses the workflow for at least the period specified.
        /// </summary>
        /// <param name="duration">The time to sleep.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="TaskCanceledException">
        /// Thrown if the operation was cancelled via <see cref="CancellationToken"/> or the
        /// workflow was cancelled externally.
        /// </exception>
        protected async Task SleepAsync(TimeSpan duration, CancellationToken cancellationToken = default)
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
        /// Executes a child workflow and waits for it to complete.
        /// </summary>
        /// <param name="name">The workflow name.</param>
        /// <param name="args">Optionally specifies the workflow arguments.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The workflow result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        protected async Task<byte[]> CallWorkflow(string name, byte[] args = null, ChildWorkflowOptions options = null, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Executes an activity and waits for it to complete.
        /// </summary>
        /// <param name="name">Identifies the activity.</param>
        /// <param name="args">Optionally specifies the activity name.</param>
        /// <returns>The activity result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        protected async Task<byte[]> CallActivity(string name, byte[] args = null)
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Executes a local activity and waits for it to complete.
        /// </summary>
        /// <typeparam name="TActivity">Specifies the local activity implementation type.</typeparam>
        /// <param name="args">Optionally specifies the activity name.</param>
        /// <param name="options">Optionally specifies any local activity options.</param>
        /// <returns>The activity result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <remarks>
        /// This method can be used to optimize activities that will complety quickly
        /// (within seconds).  Rather than scheduling the activity on any worker that
        /// has registered an implementation for the activity, this method will simply
        /// instantiate an instance of <typeparamref name="TActivity"/> and call its
        /// <see cref="Activity.RunAsync(byte[])"/> method.
        /// </remarks>
        protected async Task<byte[]> CallLocalActivity<TActivity>(byte[] args = null, LocalActivityOptions options = null)
            where TActivity : Activity
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Exits and completes the current running workflow and then restarts it, passing the
        /// optional workflow arguments.
        /// </summary>
        /// <param name="args">Optional arguments for the new run.</param>
        /// <param name="domain">Optional domain for the new run.</param>
        /// <param name="tasklist">Optional tasklist for the new run.</param>
        /// <param name="executionToStartTimeout">Optional execution to start timeout for the new run.</param>
        /// <param name="scheduleToCloseTimeout">Optional schedule to close timeout for the new run.</param>
        /// <param name="scheduleToStartTimeout">Optional schedule to start timeout for the new run.</param>
        /// <param name="startToCloseTimeout">Optional start to close timeout for the new run.</param>
        /// <param name="retryPolicy">Optional retry policy for the new run.</param>
        protected void Restart(
            byte[]              args                    = null,
            string              domain                  = null,
            string              tasklist                = null,
            TimeSpan            executionToStartTimeout = default,
            TimeSpan            scheduleToCloseTimeout  = default,
            TimeSpan            scheduleToStartTimeout  = default,
            TimeSpan            startToCloseTimeout     = default,
            CadenceRetryPolicy  retryPolicy             = null)
        {
            // We're going to throw a [InternalWorkflowRestartException] with the
            // parameters.  This exception will be caught and handled by the 
            // [WorkflowInvoke()] method which will configure the reply such
            // that the cadence-proxy will be able to signal Cadence to continue
            // the workflow with a clean history.

            throw new InternalWorkflowRestartException(
                args:                       args,
                domain:                     domain,
                tasklist:                   tasklist,
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
