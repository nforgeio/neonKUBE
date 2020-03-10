//-----------------------------------------------------------------------------
// FILE:	    WorkflowBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Time;

namespace Neon.Cadence
{
    /// <summary>
    /// Base class that must be inherited for all workflow implementations.
    /// </summary>
    public class WorkflowBase
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Enumerates the possible contexts workflow code may be executing within.
        /// This is used to limit what code can do (i.e. query methods shouldn't be
        /// allowed to execute activities).  This is also used in some situations to
        /// modify how workflow code behaves.
        /// </summary>
        internal enum WorkflowCallContext
        {
            /// <summary>
            /// The current task is not executing within the context
            /// of any workflow method.
            /// </summary>
            None = 0,

            /// <summary>
            /// The current task is executing within the context of
            /// a workflow entrypoint.
            /// </summary>
            Entrypoint,

            /// <summary>
            /// The current task is executing within the context of a
            /// workflow signal method.
            /// </summary>
            Signal,

            /// <summary>
            /// The current task is executing within the context of a
            /// workflow query method.
            /// </summary>
            Query,

            /// <summary>
            /// The current task is executing within the context of a
            /// normal or local activity.
            /// </summary>
            Activity
        }

        /// <summary>
        /// Used to map a Cadence client ID and workflow context ID into a
        /// key that can be used to dereference <see cref="idToWorkflow"/>.
        /// </summary>
        private struct WorkflowInstanceKey
        {
            private long clientId;
            private long contextId;

            public WorkflowInstanceKey(CadenceClient client, long contextId)
            {
                this.clientId  = client.ClientId;
                this.contextId = contextId;
            }

            public override int GetHashCode()
            {
                return clientId.GetHashCode() ^ contextId.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj == null || !(obj is WorkflowInstanceKey))
                {
                    return false;
                }

                var other = (WorkflowInstanceKey)obj;

                return this.clientId == other.clientId && 
                       this.contextId == other.contextId;
            }

            public override string ToString()
            {
                return $"clientID={clientId}, contextId={contextId}";
            }
        }

        /// <summary>
        /// Describes the workflow implementation type, entry point method, and 
        /// signal/query methods for registered workflow.
        /// </summary>
        private class WorkflowRegistration
        {
            /// <summary>
            /// The workflow implemention type.
            /// </summary>
            public Type WorkflowType { get; set; }

            /// <summary>
            /// The workflow entry point method.
            /// </summary>
            public MethodInfo WorkflowMethod { get; set; }

            /// <summary>
            /// The workflow entry point parameter types.
            /// </summary>
            public Type[] WorkflowMethodParameterTypes { get; set; }

            /// <summary>
            /// Maps workflow signal and query names to the corresponding
            /// method implementations.
            /// </summary>
            public WorkflowMethodMap MethodMap { get; set; }
        }

        //---------------------------------------------------------------------
        // Static members

        private static object                                           syncLock     = new object();
        private static INeonLogger                                      log          = LogManager.Default.GetLogger<WorkflowBase>();
        private static Dictionary<WorkflowInstanceKey, WorkflowBase>    idToWorkflow = new Dictionary<WorkflowInstanceKey, WorkflowBase>();
        private static byte[]                                           emptyBytes   = new byte[0];

        // This dictionary is used to map workflow type names to the target workflow
        // registration.  Note that these mappings are scoped to specific cadence client
        // instances by prefixing the type name with:
        //
        //      CLIENT-ID::
        //
        // where CLIENT-ID is the locally unique ID of the client.  This is important,
        // because we'll need to remove entries the for clients when they're disposed.
        //
        // Workflow type names may also include a second "::" separator with the
        // workflow entry point name appended afterwards to handle workflow interfaces
        // that have multiple workflow entry points.  So, valid workflow registrations
        // may looks like:
        // 
        //      1::my-workflow                  -- clientId = 1, workflow type name = my-workflow
        //      1::my-workflow::my-entrypoint   -- clientId = 1, workflow type name = my-workflow, entrypoint = my-entrypoint

        private static Dictionary<string, WorkflowRegistration> nameToRegistration = new Dictionary<string, WorkflowRegistration>();

        /// <summary>
        /// Holds ambient task state indicatring whether the current task executing
        /// in the context of a workflow entry point, signal, or query.
        /// </summary>
        internal static AsyncLocal<WorkflowCallContext> CallContext { get; private set; } = new AsyncLocal<WorkflowCallContext>();

        /// <summary>
        /// Restores the class to its initial state.
        /// </summary>
        internal static void Reset()
        {
            lock (syncLock)
            {
                idToWorkflow.Clear();
            }
        }

        /// <summary>
        /// Ensures that the current <see cref="Task"/> is running within the context of a workflow 
        /// entry point, signal, or query method and also that the context matches one of the parameters
        /// indicating which contexts are allowed.  This is used ensure that only workflow operations
        /// that are valid for a context are allowed.
        /// </summary>
        /// <param name="allowWorkflow">Optionally indicates that calls from workflow entry point contexts are allowed.</param>
        /// <param name="allowQuery">Optionally indicates that calls from workflow query contexts are allowed.</param>
        /// <param name="allowSignal">Optionally indicates that calls from workflow signal contexts are allowed.</param>
        /// <param name="allowActivity">Optionally indicates that calls from activity contexts are allowed.</param>
        /// <exception cref="NotSupportedException">Thrown when the operation is not supported in the current context.</exception>
        internal static void CheckCallContext(
            bool allowWorkflow = false, 
            bool allowQuery    = false, 
            bool allowSignal   = false, 
            bool allowActivity = false)
        {
            switch (CallContext.Value)
            {
                case WorkflowCallContext.None:

                    throw new NotSupportedException("This operation cannot be performed outside of a workflow.");

                case WorkflowCallContext.Entrypoint:

                    if (!allowWorkflow)
                    {
                        throw new NotSupportedException("This operation cannot be performed within a workflow entry point method.");
                    }
                    break;

                case WorkflowCallContext.Query:

                    if (!allowQuery)
                    {
                        throw new NotSupportedException("This operation cannot be performed within a workflow query method.");
                    }
                    break;

                case WorkflowCallContext.Signal:

                    if (!allowSignal)
                    {
                        throw new NotSupportedException("This operation cannot be performed within a workflow signal method.");
                    }
                    break;

                case WorkflowCallContext.Activity:

                    if (!allowActivity)
                    {
                        throw new NotSupportedException("This operation cannot be performed within an activity method.");
                    }
                    break;
            }
        }

        /// <summary>
        /// Prepends the Cadence client ID to the workflow type name as well as the optional
        /// workflow method name to generate the key used to dereference the <see cref="nameToRegistration"/> 
        /// dictionary.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="workflowMethodAttribute">The workflow method attribute. </param>
        /// <returns>The workflow registration key.</returns>
        private static string GetWorkflowTypeKey(CadenceClient client, string workflowTypeName, WorkflowMethodAttribute workflowMethodAttribute)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));
            Covenant.Requires<ArgumentNullException>(workflowMethodAttribute != null, nameof(workflowMethodAttribute));

            if (string.IsNullOrEmpty(workflowMethodAttribute.Name))
            {
                return $"{client.ClientId}::{workflowTypeName}";
            }
            else
            {
                return $"{client.ClientId}::{workflowTypeName}::{workflowMethodAttribute.Name}";
            }
        }

        /// <summary>
        /// Strips the leading client ID from the workflow type key passed
        /// and returns the type name actually registered with Cadence.
        /// </summary>
        /// <param name="workflowTypeKey">The workflow type key.</param>
        /// <returns>The Cadence workflow type name.</returns>
        private static string GetWorkflowTypeNameFromKey(string workflowTypeKey)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeKey), nameof(workflowTypeKey));

            var separatorPos = workflowTypeKey.IndexOf(CadenceHelper.WorkflowTypeMethodSeparator);

            Covenant.Assert(separatorPos >= 0);

            return workflowTypeKey.Substring(separatorPos + CadenceHelper.WorkflowTypeMethodSeparator.Length);
        }

        /// <summary>
        /// Registers a workflow implementation.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="workflowType">The workflow implementation type.</param>
        /// <param name="workflowTypeName">The name used to identify the implementation.</param>
        /// <param name="domain">Specifies the target domain.</param>
        /// <exception cref="InvalidOperationException">Thrown if a different workflow class has already been registered for <paramref name="workflowTypeName"/>.</exception>
        internal static async Task RegisterAsync(CadenceClient client, Type workflowType, string workflowTypeName, string domain)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain), nameof(domain));
            CadenceHelper.ValidateWorkflowImplementation(workflowType);

            var methodMap = WorkflowMethodMap.Create(workflowType);

            // We need to register each workflow method that implements a workflow interface method
            // with the same signature that that was tagged by [WorkflowMethod].
            //
            // First, we'll create a dictionary that maps method signatures from any inherited
            // interfaces that are tagged by [WorkflowMethod] to the attribute.

            var methodSignatureToAttribute = new Dictionary<string, WorkflowMethodAttribute>();

            foreach (var interfaceType in workflowType.GetInterfaces())
            {
                foreach (var method in interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    var workflowMethodAttribute = method.GetCustomAttribute<WorkflowMethodAttribute>();

                    if (workflowMethodAttribute == null)
                    {
                        continue;
                    }

                    var signature = method.ToString();

                    if (methodSignatureToAttribute.ContainsKey(signature))
                    {
                        throw new NotSupportedException($"Workflow type [{workflowType.FullName}] cannot implement the [{signature}] method from two different interfaces.");
                    }

                    methodSignatureToAttribute.Add(signature, workflowMethodAttribute);
                }
            }

            // Next, we need to register the workflow methods that implement the
            // workflow interface.

            foreach (var method in workflowType.GetMethods())
            {
                if (!methodSignatureToAttribute.TryGetValue(method.ToString(), out var workflowMethodAttribute))
                {
                    continue;
                }

                var workflowTypeKey = GetWorkflowTypeKey(client, workflowTypeName, workflowMethodAttribute);

                lock (syncLock)
                {
                    if (nameToRegistration.TryGetValue(workflowTypeName, out var existingRegistration))
                    {
                        if (!object.ReferenceEquals(existingRegistration.WorkflowType, workflowType))
                        {
                            throw new InvalidOperationException($"Conflicting workflow interface registration: Workflow interface [{workflowType.FullName}] is already registered for workflow type name [{workflowTypeName}].");
                        }
                    }
                    else
                    {
                        nameToRegistration[workflowTypeKey] =
                            new WorkflowRegistration()
                            {
                                WorkflowType                 = workflowType,
                                WorkflowMethod               = method,
                                WorkflowMethodParameterTypes = method.GetParameterTypes(),
                                MethodMap                    = methodMap
                            };
                    }
                }

                var reply = (WorkflowRegisterReply)await client.CallProxyAsync(
                    new WorkflowRegisterRequest()
                    {
                        Name   = GetWorkflowTypeNameFromKey(workflowTypeKey),
                        Domain = client.ResolveDomain(domain)
                    });

                // $hack(jefflill): 
                //
                // We're going to ignore any errors here to address:
                //
                //      https://github.com/nforgeio/neonKUBE/issues/668

                // reply.ThrowOnError();
            }
        }

        /// <summary>
        /// Removes all type workflow interface registrations for a Cadence client (when it's being disposed).
        /// </summary>
        /// <param name="client">The client being disposed.</param>
        internal static void UnregisterClient(CadenceClient client)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            var prefix = $"{client.ClientId}::";

            lock (syncLock)
            {
                foreach (var key in nameToRegistration.Keys.Where(key => key.StartsWith(prefix)).ToList())
                {
                    nameToRegistration.Remove(key);
                }
            }
        }

        /// <summary>
        /// Returns the registration for the named Cadence workflow.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="workflowTypeName">The Cadence workflow type name.</param>
        /// <returns>The <see cref="WorkflowRegistration"/> or <c>null</c> if the type was not found.</returns>
        private static WorkflowRegistration GetWorkflowRegistration(CadenceClient client, string workflowTypeName)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));

            lock (syncLock)
            {
                if (nameToRegistration.TryGetValue($"{client.ClientId}::{workflowTypeName}", out var registration))
                {
                    return registration;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="SyncSignalStatus"/> for the specified workflow and signal.
        /// </summary>
        /// <param name="contextId">The target workflow context ID.</param>
        /// <param name="signalId">The target signal ID.</param>
        /// <returns>The <see cref="SyncSignalStatus"/> for the signal.</returns>
        internal static SyncSignalStatus GetSignalStatus(long contextId, string signalId)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalId));

            // Lookup the workflow.

            WorkflowBase workflow = null;

            lock (syncLock)
            {
                idToWorkflow.TryGetValue(new WorkflowInstanceKey(Workflow.Current.Client, contextId), out workflow);
            }

            if (workflow == null)
            {
                // The workflow doesn't exist so we'll return a dummy status
                // instance to prevent the caller from barfing.

                return new SyncSignalStatus() { Completed = false };
            }

            // Lookup the status for the signal.

            lock (workflow.signalIdToStatus)
            {
                if (!workflow.signalIdToStatus.TryGetValue(signalId, out var signalStatus))
                {
                    signalStatus = new SyncSignalStatus();

                    workflow.signalIdToStatus.Add(signalId, signalStatus);
                }

                return signalStatus;
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
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            ProxyReply reply;

            switch (request.Type)
            {
                case InternalMessageTypes.WorkflowInvokeRequest:

                    reply = await OnInvokeAsync(client, (WorkflowInvokeRequest)request);
                    break;

                case InternalMessageTypes.WorkflowSignalInvokeRequest:

                    reply = await OnSignalAsync(client, (WorkflowSignalInvokeRequest)request);
                    break;

                case InternalMessageTypes.WorkflowQueryInvokeRequest:

                    reply = await OnQueryAsync(client, (WorkflowQueryInvokeRequest)request);
                    break;

                case InternalMessageTypes.ActivityInvokeLocalRequest:

                    reply = await OnInvokeLocalActivity(client, (ActivityInvokeLocalRequest)request);
                    break;

                case InternalMessageTypes.WorkflowFutureReadyRequest:

                    // $todo(jefflill): We need to actually implement this.

                    reply = new WorkflowFutureReadyReply();
                    break;

                default:

                    throw new InvalidOperationException($"Unexpected message type [{request.Type}].");
            }

            await client.ProxyReplyAsync(request, reply);
        }

        /// <summary>
        /// Thread-safe method that maps a workflow ID to the corresponding workflow instance.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="contextId">The workflow's context ID.</param>
        /// <returns>The <see cref="WorkflowBase"/> instance or <c>null</c> if the workflow was not found.</returns>
        private static WorkflowBase GetWorkflow(CadenceClient client, long contextId)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            lock (syncLock)
            {
                if (idToWorkflow.TryGetValue(new WorkflowInstanceKey(client, contextId), out var workflow))
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
        /// <param name="client">The associated Cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowInvokeReply> OnInvokeAsync(CadenceClient client, WorkflowInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));
            Covenant.Requires<ArgumentException>(request.ReplayStatus != InternalReplayStatus.Unspecified, nameof(request));
CadenceHelper.DebugLog($"Workflow Invoke Message: workflowId={request.WorkflowId} contextId={request.ContextId}");

            WorkflowBase            workflow;
            WorkflowRegistration    registration;

            var contextId   = request.ContextId;
            var workflowKey = new WorkflowInstanceKey(client, contextId);

            lock (syncLock)
            {
                if (idToWorkflow.TryGetValue(workflowKey, out workflow))
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"A workflow with [ID={workflowKey}] is already running on this worker.")
                    };
                }

                registration = GetWorkflowRegistration(client, request.WorkflowType);

                if (registration == null)
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"Workflow type name [Type={request.WorkflowType}] is not registered for this worker.")
                    };
                }
            }

            workflow = (WorkflowBase)Activator.CreateInstance(registration.WorkflowType);
            workflow.Workflow = 
                new Workflow(
                    parent:             (WorkflowBase)workflow,
                    client:             client, 
                    contextId:          contextId,
                    workflowTypeName:   request.WorkflowType,
                    domain:             request.Domain,
                    taskList:           request.TaskList,
                    workflowId:         request.WorkflowId,
                    runId:              request.RunId,
                    isReplaying:        request.ReplayStatus == InternalReplayStatus.Replaying,
                    methodMap:          registration.MethodMap);

            Workflow.Current = workflow.Workflow;   // Initialize the ambient workflow information.

            lock (syncLock)
            {
                idToWorkflow.Add(workflowKey, workflow);
            }

            // Register any workflow signal and/or query methods with cadence-proxy.
            // Note that synchronous signals need special handling further below.

            foreach (var signalName in registration.MethodMap.GetSignalNames())
            {
                var reply = (WorkflowSignalSubscribeReply)await client.CallProxyAsync(
                    new WorkflowSignalSubscribeRequest()
                    {
                        ContextId  = contextId,
                        SignalName = signalName
                    });

                reply.ThrowOnError();
            }

            foreach (var queryType in registration.MethodMap.GetQueryTypes())
            {
                var reply = (WorkflowSetQueryHandlerReply)await client.CallProxyAsync(
                    new WorkflowSetQueryHandlerRequest()
                    {
                        ContextId = contextId,
                        QueryName = queryType
                    });

                reply.ThrowOnError();
            }

            // $hack(jefflill): 
            //
            // We registered any synchronous signal names above even though we probably
            // shouldn't have.  This shouldn't really cause any trouble.
            //
            // If the workflow has any synchronous signals, we need to register the
            // special synchronous signal dispatcher as well as the synchronous signal
            // query handler.

            if (registration.MethodMap.HasSynchronousSignals)
            {
                workflow.hasSynchronousSignals = true;

                var signalSubscribeReply = (WorkflowSignalSubscribeReply)await client.CallProxyAsync(
                    new WorkflowSignalSubscribeRequest()
                    {
                        ContextId  = contextId,
                        SignalName = CadenceClient.SignalSync
                    });

                signalSubscribeReply.ThrowOnError();

                var querySubscribeReply = (WorkflowSetQueryHandlerReply)await client.CallProxyAsync(
                    new WorkflowSetQueryHandlerRequest()
                    {
                        ContextId = contextId,
                        QueryName = CadenceClient.QuerySyncSignal
                    });

                querySubscribeReply.ThrowOnError();
            }

            // Start the workflow by calling its workflow entry point method.
            // This method will indicate that it has completed via one of these 
            // techniques:
            //
            //      1. The method returns normally with the workflow result.
            //
            //      2. The method calls [ContinuAsNewAsync()] which throws an
            //         [InternalWorkflowRestartException] which will be caught and
            //         handled here.
            //
            //      3. The method throws another exception which will be caught
            //         and be used to indicate that the workflow failed.

            try
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.Entrypoint;

                var workflowMethod   = registration.WorkflowMethod;
                var resultType       = workflowMethod.ReturnType;
                var args             = client.DataConverter.FromDataArray(request.Args, registration.WorkflowMethodParameterTypes);
                var serializedResult = emptyBytes;

                if (resultType.IsGenericType)
                {
                    // Workflow method returns: Task<T>

                    var result = await NeonHelper.GetTaskResultAsObjectAsync((Task)workflowMethod.Invoke(workflow, args));

                    serializedResult = client.DataConverter.ToData(result);
                }
                else
                {
                    // Workflow method returns: Task

                    await (Task)workflowMethod.Invoke(workflow, args);
                }

                await WaitForPendingWorkflowOperations(workflow);

                return new WorkflowInvokeReply()
                {
                    Result = serializedResult
                };
            }
            catch (ForceReplayException)
            {
                return new WorkflowInvokeReply()
                {
                    ForceReplay = true
                };
            }
            catch (ContinueAsNewException e)
            {
                await WaitForPendingWorkflowOperations(workflow);

                return new WorkflowInvokeReply()
                {
                    ContinueAsNew                             = true,
                    ContinueAsNewArgs                         = e.Args,
                    ContinueAsNewWorkflow                     = e.Workflow,
                    ContinueAsNewDomain                       = e.Domain,
                    ContinueAsNewTaskList                     = e.TaskList,
                    ContinueAsNewExecutionStartToCloseTimeout = CadenceHelper.ToCadence(e.ExecutionStartToCloseTimeout),
                    ContinueAsNewScheduleToCloseTimeout       = CadenceHelper.ToCadence(e.ScheduleToCloseTimeout),
                    ContinueAsNewScheduleToStartTimeout       = CadenceHelper.ToCadence(e.ScheduleToStartTimeout),
                    ContinueAsNewStartToCloseTimeout          = CadenceHelper.ToCadence(e.TaskStartToCloseTimeout),
                };
            }
            catch (CadenceException e)
            {
                log.LogError(e);

                await WaitForPendingWorkflowOperations(workflow);

                return new WorkflowInvokeReply()
                {
                    Error = e.ToCadenceError()
                };
            }
            catch (Exception e)
            {
                log.LogError(e);

                await WaitForPendingWorkflowOperations(workflow);

                return new WorkflowInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
            finally
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.None;
            }
        }

        /// <summary>
        /// Waits for any pending workflow operations (like outstanding synchronous signals) to 
        /// complete.  This is called before returning from a workflow method.
        /// </summary>
        /// <param name="workflow">The target workflow.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task WaitForPendingWorkflowOperations(WorkflowBase workflow)
        {
            // Right now, the only pending operations can be completed outstanding 
            // synchronous signals that haven't returned their results to the
            // calling client via polled queries.

            if (!workflow.hasSynchronousSignals)
            {
                // The workflow doesn't implement any synchronous signals, so we can
                // return immediately.

                return;
            }

            // Wait for a period of time for any signals to be acknowledged.  We're simply going
            // to loop until all of the signals have been acknowledged, sleeping for 1 second
            // between checks.
            //
            // I originally tried using [MutableSideEffectAsync()] for the polling and using
            // [Task.DelayAsync()] for the poll delay, but that didn't work because it
            // appears that Cadence doesn't process queries when MutableSideEffectAsync() 
            // is running (perhaps this doesn't count as a real decision task).
            //
            // The down side of doing it this way is that each of the sleeps will be
            // recorded to the workflow history.  We'll have to live with that.  I 
            // expect that we'll only have to poll for a second or two in most 
            // circumstances anyway.

            var sysDeadline = SysTime.Now + workflow.Workflow.Client.Settings.MaxWorkflowDelay;
            var signalCount = 0;

            while (SysTime.Now < sysDeadline)
            {
                // Break when all signals have been acknowledged.

                lock (workflow.signalIdToStatus)
                {
                    signalCount = workflow.signalIdToStatus.Count;

                    if (signalCount == 0)
                    {
                        break; // No synchronous signals were called.
                    }
                    else if (workflow.signalIdToStatus.Values.All(status => status.Acknowledged))
                    {
                        break; // All signals have been acknowledged
                    }
                }

                await workflow.Workflow.SleepAsync(TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// Handles workflow signals.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowSignalInvokeReply> OnSignalAsync(CadenceClient client, WorkflowSignalInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            // Handle synchronous signals in a specialized method.

            if (request.SignalName == CadenceClient.SignalSync)
            {
                return await OnSyncSignalAsync(client, request);
            }

            try
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.Signal;

                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    Workflow.Current = workflow.Workflow;   // Initialize the ambient workflow information.

                    var method = workflow.Workflow.MethodMap.GetSignalMethod(request.SignalName);

                    if (method != null)
                    {
                        await (Task)(method.Invoke(workflow, client.DataConverter.FromDataArray(request.SignalArgs, method.GetParameterTypes())));

                        return new WorkflowSignalInvokeReply()
                        {
                            RequestId = request.RequestId
                        };
                    }
                    else
                    {
                        return new WorkflowSignalInvokeReply()
                        {
                            Error = new EntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a signal handler for [signalName={request.SignalName}].").ToCadenceError()
                        };
                    }
                }
                else
                {
                    // I don't believe we'll ever land here because that would mean that
                    // Cadence sends signals to a workflow that hasn't started running
                    // on a worker (which wouldn't make sense).
                    //
                    // We're going go ahead and send a reply, just in case.

                    return new WorkflowSignalInvokeReply();
                }
            }
            catch (Exception e)
            {
                log.LogError(e);

                return new WorkflowSignalInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
            finally
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.None;
            }
        }

        /// <summary>
        /// Handles internal <see cref="CadenceClient.SignalSync"/> workflow signals.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowSignalInvokeReply> OnSyncSignalAsync(CadenceClient client, WorkflowSignalInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            try
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.Signal;

                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    Workflow.Current = workflow.Workflow;   // Initialize the ambient workflow information.

                    // The signal arguments should be just a single [SyncSignalCall] that specifies
                    // the target signal and also includes its encoded arguments.

                    var signalCallArgs = client.DataConverter.FromDataArray(request.SignalArgs, typeof(SyncSignalCall));
                    var signalCall     = (SyncSignalCall)signalCallArgs[0];
                    var signalMethod   = workflow.Workflow.MethodMap.GetSignalMethod(signalCall.TargetSignal);
                    var userSignalArgs = client.DataConverter.FromDataArray(signalCall.UserArgs, signalMethod.GetParameterTypes());

                    Workflow.Current.SignalId = signalCall.SignalId;

                    // Create a dictionary with the signal method arguments keyed by parameter name.

                    var args       = new Dictionary<string, object>();
                    var parameters = signalMethod.GetParameters();

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        args.Add(parameters[i].Name, userSignalArgs[i]);
                    }

                    // Persist the state that the signal status queries will examine.
                    // We're also going to use the presence of this state to make
                    // synchronous signal calls idempotent by ensuring that we'll
                    // only call the signal method once per signal ID.
                    //
                    // Note that it's possible that a record has already exists.

                    var newSignal = false;
                    lock (workflow.signalIdToStatus)
                    {
                        if (!workflow.signalIdToStatus.TryGetValue(signalCall.SignalId, out var signalStatus))
                        {
                            newSignal    = true;
                            signalStatus = new SyncSignalStatus();

                            workflow.signalIdToStatus.Add(signalCall.SignalId, signalStatus);
                        }

                        signalStatus.Args = args;
                    }

                    if (newSignal && signalMethod != null)
                    {
                        Workflow.Current = workflow.Workflow;   // Initialize the ambient workflow information for workflow library code.

                        var result    = (object)null;
                        var exception = (Exception)null;

                        // Execute the signal method (if there is one).

                        try
                        {
                            if (CadenceHelper.IsTask(signalMethod.ReturnType))
                            {
                                // Method returns [Task]: AKA void.

                                await (Task)(signalMethod.Invoke(workflow, userSignalArgs));
                            }
                            else
                            {
                                // Method returns [Task<T>]: AKA a result.

                                // $note(jefflill):
                                //
                                // I would have liked to do something like this:
                                //
                                //      result = await (Task<object>)(method.Invoke(workflow, userArgs));
                                //
                                // here, but that not going to work because the Task<T>
                                // being returned won't typically be a Task<object>,
                                // so the cast will fail.
                                //
                                // So instead, I'm going to use reflection to obtain the 
                                // Task.Result property and then obtain the result from that.

                                var task           = (Task)(signalMethod.Invoke(workflow, userSignalArgs));
                                var resultProperty = task.GetType().GetProperty("Result");

                                await task;

                                result = resultProperty.GetValue(task);
                            }
                        }
                        catch (Exception e)
                        {
                            exception = e;
                        }

                        if (exception?.GetType() == typeof(WaitForSignalReplyException))
                        {
                            // This will be thrown by synchronous signal handlers that marshalled
                            // the signal to the workflow logic.  We're going to ignore the signal
                            // method result in this case and NOT MARK THE SIGNAL AS COMPLETED.
                        }
                        else
                        {
                            lock (workflow.signalIdToStatus)
                            {
                                if (workflow.signalIdToStatus.TryGetValue(signalCall.SignalId, out var syncSignalStatus))
                                {
                                    if (exception == null)
                                    {
                                        syncSignalStatus.Result = client.DataConverter.ToData(result);
                                    }
                                    else
                                    {
                                        log.LogError(exception);

                                        syncSignalStatus.Error = SyncSignalException.GetError(exception);
                                    }

                                    syncSignalStatus.Completed = true;
                                }
                                else
                                {
                                    Covenant.Assert(false);
                                }
                            }
                        }

                        return new WorkflowSignalInvokeReply()
                        {
                            RequestId = request.RequestId
                        };
                    }
                    else
                    {
                        return new WorkflowSignalInvokeReply()
                        {
                            Error = new EntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a signal handler for [signalName={request.SignalName}].").ToCadenceError()
                        };
                    }
                }
                else
                {
                    // I don't believe we'll ever land here because that would mean that
                    // Cadence sends signals to a workflow that hasn't started running
                    // on a worker (which wouldn't make sense).
                    //
                    // We're going go ahead and send a reply, just in case.

                    return new WorkflowSignalInvokeReply();
                }
            }
            catch (Exception e)
            {
                log.LogError(e);

                return new WorkflowSignalInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
            finally
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.None;
            }
        }

        /// <summary>
        /// Handles workflow queries.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowQueryInvokeReply> OnQueryAsync(CadenceClient client, WorkflowQueryInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            try
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.Query;

                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    Workflow.Current = workflow.Workflow;   // Initialize the ambient workflow information for workflow library code.

                    // Handle built-in queries.

                    switch (request.QueryName)
                    {
                        case CadenceClient.QueryStack:

                            var trace = string.Empty;

                            if (workflow.StackTrace != null)
                            {
                                trace = workflow.StackTrace.ToString();
                            }

                            return new WorkflowQueryInvokeReply()
                            {
                                RequestId = request.RequestId,
                                Result    = client.DataConverter.ToData(trace)
                            };

                        case CadenceClient.QuerySyncSignal:

                            // The arguments for this signal is the (string) ID of the target
                            // signal being polled for status.

                            var syncSignalArgs   = client.DataConverter.FromDataArray(request.QueryArgs, typeof(string));
                            var syncSignalId     = (string) (syncSignalArgs.Length > 0 ? syncSignalArgs[0] : null);
                            var syncSignalStatus = (SyncSignalStatus)null;

                            lock (workflow.signalIdToStatus)
                            {
                                if (!workflow.signalIdToStatus.TryGetValue(syncSignalId, out syncSignalStatus))
                                {
                                    syncSignalStatus = new SyncSignalStatus() { Completed = false };
                                }

                                if (syncSignalStatus.Completed)
                                {
                                    // Indicate that the completed signal has reported the status
                                    // to the calling client as well as returned the result, if any.

                                    syncSignalStatus.Acknowledged       = true;
                                    syncSignalStatus.AcknowledgeTimeUtc = DateTime.UtcNow;
                                }
                            }

                            return new WorkflowQueryInvokeReply()
                            {
                                RequestId = request.RequestId,
                                Result    = client.DataConverter.ToData(syncSignalStatus)
                            };
                    }

                    // Handle user queries.

                    var method = workflow.Workflow.MethodMap.GetQueryMethod(request.QueryName);

                    if (method != null)
                    {
                        var resultType           = method.ReturnType;
                        var methodParameterTypes = method.GetParameterTypes();

                        var serializedResult = emptyBytes;

                        if (resultType.IsGenericType)
                        {
                            // Query method returns: Task<T>

                            var result = await NeonHelper.GetTaskResultAsObjectAsync((Task)method.Invoke(workflow, client.DataConverter.FromDataArray(request.QueryArgs, methodParameterTypes)));

                            serializedResult = client.DataConverter.ToData(result);
                        }
                        else
                        {
                            // Query method returns: Task

                            await (Task)method.Invoke(workflow, client.DataConverter.FromDataArray(request.QueryArgs, methodParameterTypes));
                        }

                        return new WorkflowQueryInvokeReply()
                        {
                            RequestId = request.RequestId,
                            Result    = serializedResult
                        };
                    }
                    else
                    {
                        return new WorkflowQueryInvokeReply()
                        {
                            Error = new EntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a query handler for [queryType={request.QueryName}].").ToCadenceError()
                        };
                    }
                }
                else
                {
                    return new WorkflowQueryInvokeReply()
                    {
                        Error = new EntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToCadenceError()
                    };
                }
            }
            catch (Exception e)
            {
                log.LogError(e);

                return new WorkflowQueryInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
            finally
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.None;
            }
        }

        /// <summary>
        /// Handles workflow local activity invocations.
        /// </summary>
        /// <param name="client">The client the request was received from.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<ActivityInvokeLocalReply> OnInvokeLocalActivity(CadenceClient client, ActivityInvokeLocalRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            try
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.Activity;

                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    LocalActivityAction activityAction;

                    lock (syncLock)
                    {
                        if (!workflow.Workflow.IdToLocalActivityAction.TryGetValue(request.ActivityTypeId, out activityAction))
                        {
                            return new ActivityInvokeLocalReply()
                            {
                                Error = new EntityNotExistsException($"Activity type does not exist for [activityTypeId={request.ActivityTypeId}].").ToCadenceError()
                            };
                        }
                    }

                    var workerArgs = new WorkerArgs() { Client = client, ContextId = request.ActivityContextId };
                    var activity   = ActivityBase.Create(client, activityAction, null);
                    var result     = await activity.OnInvokeAsync(client, request.Args);

                    return new ActivityInvokeLocalReply()
                    {
                        Result = result
                    };
                }
                else
                {
                    return new ActivityInvokeLocalReply()
                    {
                        Error = new EntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToCadenceError()
                    };
                }
            }
            catch (Exception e)
            {
                log.LogError(e);

                return new ActivityInvokeLocalReply()
                {
                    Error = new CadenceError(e)
                };
            }
            finally
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.None;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private Dictionary<string, SyncSignalStatus>    signalIdToStatus      = new Dictionary<string, SyncSignalStatus>();
        private bool                                    hasSynchronousSignals = false;

        /// <summary>
        /// This field holds the stack trace for the most recent decision related 
        /// <see cref="Workflow"/> method calls.  This will be returned for internal
        /// workflow <b>"__stack_trace"</b> queries.
        /// </summary>
        internal StackTrace StackTrace { get; set; } = null;

        /// <summary>
        /// Returns a <see cref="Workflow"/> instance with utilty methods you'll use
        /// for implementing your workflows.
        /// </summary>
        public Workflow Workflow { get; set; }
    }
}
