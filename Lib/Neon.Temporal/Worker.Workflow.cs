//-----------------------------------------------------------------------------
// FILE:	    Worker.Workflow.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Tasks;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    public sealed partial class Worker : IDisposable
    {
        //---------------------------------------------------------------------
        // Workflow related private types

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
        // Implementation

        private List<Type>                                  registeredWorkflowTypes    = new List<Type>();
        private Dictionary<string, WorkflowRegistration>    nameToWorkflowRegistration = new Dictionary<string, WorkflowRegistration>();
        private Dictionary<long, WorkflowBase>              idToWorkflow               = new Dictionary<long, WorkflowBase>();

        /// <summary>
        /// Registers a workflow implementation with temporal-proxy.
        /// </summary>
        /// <param name="workflowType">The workflow implementation type.</param>
        /// <exception cref="RegistrationException">Thrown when there's a problem with the registration.</exception>
        private async Task RegisterWorkflowImplementationAsync(Type workflowType)
        {
            TemporalHelper.ValidateWorkflowImplementation(workflowType);

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

                var workflowTypeName = TemporalHelper.GetWorkflowTypeName(workflowType, workflowMethodAttribute);

                lock (nameToWorkflowRegistration)
                {
                    if (nameToWorkflowRegistration.TryGetValue(workflowTypeName, out var existingRegistration))
                    {
                        if (!object.ReferenceEquals(existingRegistration.WorkflowType, workflowType))
                        {
                            throw new InvalidOperationException($"Conflicting workflow interface registration: Workflow interface [{workflowType.FullName}] is already registered for workflow type name [{workflowTypeName}].");
                        }
                    }
                    else
                    {
                        nameToWorkflowRegistration[workflowTypeName] =
                            new WorkflowRegistration()
                            {
                                WorkflowType                 = workflowType,
                                WorkflowMethod               = method,
                                WorkflowMethodParameterTypes = method.GetParameterTypes(),
                                MethodMap                    = methodMap
                            };
                    }
                }

                var reply = (WorkflowRegisterReply)await Client.CallProxyAsync(
                    new WorkflowRegisterRequest()
                    {
                        WorkerId = WorkerId,
                        Name     = workflowTypeName,
                    });

                reply.ThrowOnError();
            }
        }

        /// <summary>
        /// Registers a workflow implementation with Temporal.
        /// </summary>
        /// <typeparam name="TWorkflow">The <see cref="WorkflowBase"/> derived class implementing the workflow.</typeparam>
        /// <param name="disableDuplicateCheck">Disable checks for duplicate registrations.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the worker has already been started.  You must register workflow 
        /// and activity implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your workflow implementations before starting a worker.
        /// </note>
        /// </remarks>
        public async Task RegisterWorkflowAsync<TWorkflow>(bool disableDuplicateCheck = false)
            where TWorkflow : WorkflowBase
        {
            await SyncContext.ClearAsync;
            TemporalHelper.ValidateWorkflowImplementation(typeof(TWorkflow));
            EnsureNotDisposed();
            EnsureCanRegister();

            var workflowType = typeof(TWorkflow);

            lock (registeredWorkflowTypes)
            {
                if (registeredWorkflowTypes.Contains(workflowType))
                {
                    if (disableDuplicateCheck)
                    {
                        return;
                    }
                    else
                    {
                        throw new RegistrationException($"Workflow implementation [{workflowType.FullName}] has already been registered.");
                    }
                }

                registeredWorkflowTypes.Add(workflowType);
            }
        }

        /// <summary>
        /// Scans the assembly passed looking for workflow implementations derived from
        /// <see cref="WorkflowBase"/> and tagged by <see cref="WorkflowAttribute"/> with
        /// <see cref="WorkflowAttribute.AutoRegister"/> set to <c>true</c> and registers 
        /// them with Temporal.
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <param name="disableDuplicateCheck">Disable checks for duplicate registrations.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TypeLoadException">
        /// Thrown for types tagged by <see cref="WorkflowAttribute"/> that are not 
        /// derived from <see cref="WorkflowBase"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown if one of the tagged classes conflict with an existing registration.</exception>
        /// <exception cref="InvalidOperationException">
        /// <exception cref="RegistrationException">Thrown when there's a problem with the registration.</exception>
        /// Thrown if the worker has already been started.  You must register workflow 
        /// and activity implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your workflow implementations before starting a worker.
        /// </note>
        /// </remarks>
        public async Task RegisterAssemblyWorkflowsAsync(Assembly assembly, bool disableDuplicateCheck = false)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(assembly != null, nameof(assembly));
            EnsureNotDisposed();
            EnsureCanRegister();

            lock (registeredWorkflowTypes)
            {
                foreach (var workflowType in assembly.GetTypes().Where(t => t.IsClass))
                {
                    var workflowAttribute = workflowType.GetCustomAttribute<WorkflowAttribute>();

                    if (workflowAttribute != null && workflowAttribute.AutoRegister)
                    {
                        if (registeredWorkflowTypes.Contains(workflowType))
                        {
                            if (disableDuplicateCheck)
                            {
                                return;
                            }
                            else
                            {
                                throw new RegistrationException($"Workflow implementation [{workflowType.FullName}] has already been registered.");
                            }
                        }

                        registeredWorkflowTypes.Add(workflowType);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the registration for the named Temporal workflow.
        /// </summary>
        /// <param name="workflowTypeName">The Temporal workflow type name.</param>
        /// <returns>The <see cref="WorkflowRegistration"/> or <c>null</c> if the type was not found.</returns>
        private WorkflowRegistration GetWorkflowRegistration(string workflowTypeName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));

            lock (nameToWorkflowRegistration)
            {
                if (this.nameToWorkflowRegistration.TryGetValue(workflowTypeName, out var registration))
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
        /// Returns the existing workflow corresponding to a context ID.
        /// </summary>
        /// <param name="contextId">The workflow context ID.</param>
        /// <returns>The <see cref="Workflow"/> or <c>null</c>.</returns>
        private WorkflowBase GetWorkflow(long contextId)
        {
            lock (idToWorkflow)
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
        /// Called to handle workflow related request messages received from the <b>temporal-proxy</b>.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        internal async Task OnProxyRequestAsync(ProxyRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            ProxyReply reply;

            switch (request.Type)
            {
                case InternalMessageTypes.WorkflowInvokeRequest:

                    reply = await OnInvokeAsync((WorkflowInvokeRequest)request);
                    break;

                case InternalMessageTypes.WorkflowSignalInvokeRequest:

                    reply = await OnSignalAsync((WorkflowSignalInvokeRequest)request);
                    break;

                case InternalMessageTypes.WorkflowQueryInvokeRequest:

                    reply = await OnQueryAsync((WorkflowQueryInvokeRequest)request);
                    break;

                default:

                    throw new InvalidOperationException($"Unexpected message type [{request.Type}].");
            }

            await Client.ProxyReplyAsync(request, reply);
        }

        /// <summary>
        /// Handles workflow invocation.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal async Task<WorkflowInvokeReply> OnInvokeAsync(WorkflowInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));
            Covenant.Requires<ArgumentException>(request.ReplayStatus != InternalReplayStatus.Unspecified, nameof(request));

            WorkflowBase            workflow;
            WorkflowRegistration    registration;

            var contextId = request.ContextId;

            lock (idToWorkflow)
            {
                if (idToWorkflow.TryGetValue(contextId, out workflow))
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new TemporalError($"A workflow with [ContextId={contextId}] is already running on this worker.")
                    };
                }
            }

            registration = GetWorkflowRegistration(request.WorkflowType);

            if (registration == null)
            {
                return new WorkflowInvokeReply()
                {
                    Error = new TemporalError($"Workflow type name [Type={request.WorkflowType}] is not registered for this worker.")
                };
            }

            workflow = (WorkflowBase)Activator.CreateInstance(registration.WorkflowType);
            workflow.Workflow = 
                new Workflow(
                    parent:             (WorkflowBase)workflow,
                    worker:             this, 
                    contextId:          contextId,
                    workflowTypeName:   request.WorkflowType,
                    @namespace:         request.Namespace,
                    taskQueue:          request.TaskQueue,
                    workflowId:         request.WorkflowId,
                    runId:              request.RunId,
                    isReplaying:        request.ReplayStatus == InternalReplayStatus.Replaying,
                    methodMap:          registration.MethodMap);

            Workflow.Current = workflow.Workflow;   // Initialize the ambient workflow information.

            lock (idToWorkflow)
            {
                idToWorkflow.Add(contextId, workflow);
            }

            // Register any workflow signal and/or query methods with [temporal-proxy].
            // Note that synchronous signals need special handling further below.

            foreach (var signalName in registration.MethodMap.GetSignalNames())
            {
                var reply = (WorkflowSignalSubscribeReply)await Client.CallProxyAsync(
                    new WorkflowSignalSubscribeRequest()
                    {
                        ContextId  = contextId,
                        SignalName = signalName,
                        WorkerId   = this.WorkerId
                    });

                reply.ThrowOnError();
            }

            foreach (var queryType in registration.MethodMap.GetQueryTypes())
            {
                var reply = (WorkflowSetQueryHandlerReply)await Client.CallProxyAsync(
                    new WorkflowSetQueryHandlerRequest()
                    {
                        ContextId = contextId,
                        QueryName = queryType,
                        WorkerId  = this.WorkerId
                    });

                reply.ThrowOnError();
            }

            // $hack(jefflill): 
            //
            // We registered synchronous signal names above even though we probably
            // shouldn't have.  This shouldn't really cause any trouble.
            //
            // If the workflow has any synchronous signals, we need to register the
            // special synchronous signal dispatcher as well as the synchronous signal
            // query handler.

            if (registration.MethodMap.HasSynchronousSignals)
            {
                workflow.HasSynchronousSignals = true;

                var signalSubscribeReply = (WorkflowSignalSubscribeReply)await Client.CallProxyAsync(
                    new WorkflowSignalSubscribeRequest()
                    {
                        ContextId  = contextId,
                        SignalName = TemporalClient.SignalSync,
                        WorkerId   = this.WorkerId
                    });

                signalSubscribeReply.ThrowOnError();

                var querySubscribeReply = (WorkflowSetQueryHandlerReply)await Client.CallProxyAsync(
                    new WorkflowSetQueryHandlerRequest()
                    {
                        ContextId = contextId,
                        QueryName = TemporalClient.QuerySyncSignal,
                        WorkerId  = this.WorkerId
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
                var args             = TemporalHelper.BytesToArgs(Client.DataConverter, request.Args, registration.WorkflowMethodParameterTypes);
                var serializedResult = Array.Empty<byte>();

                if (resultType.IsGenericType)
                {
                    // Workflow method returns: Task<T>

                    var result = await NeonHelper.GetTaskResultAsObjectAsync((Task)workflowMethod.Invoke(workflow, args));

                    serializedResult = Client.DataConverter.ToData(result);
                }
                else
                {
                    // Workflow method returns: Task

                    await (Task)workflowMethod.Invoke(workflow, args);
                }

                await workflow.WaitForPendingWorkflowOperationsAsync();

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
                await workflow.WaitForPendingWorkflowOperationsAsync();

                return new WorkflowInvokeReply()
                {
                    ContinueAsNew                             = true,
                    ContinueAsNewArgs                         = e.Args,
                    ContinueAsNewWorkflow                     = e.Workflow,
                    ContinueAsNewNamespace                    = e.Namespace,
                    ContinueAsNewTaskQueue                    = e.TaskQueue,
                    ContinueAsNewExecutionStartToCloseTimeout = TemporalHelper.ToTemporal(e.StartToCloseTimeout),
                    ContinueAsNewScheduleToCloseTimeout       = TemporalHelper.ToTemporal(e.ScheduleToCloseTimeout),
                    ContinueAsNewScheduleToStartTimeout       = TemporalHelper.ToTemporal(e.ScheduleToStartTimeout),
                    ContinueAsNewStartToCloseTimeout          = TemporalHelper.ToTemporal(e.DecisionTaskTimeout),
                };
            }
            catch (TemporalException e)
            {
                log.LogError(e);

                await workflow.WaitForPendingWorkflowOperationsAsync();

                return new WorkflowInvokeReply()
                {
                    Error = e.ToTemporalError()
                };
            }
            catch (Exception e)
            {
                log.LogError(e);

                await workflow.WaitForPendingWorkflowOperationsAsync();

                return new WorkflowInvokeReply()
                {
                    Error = new TemporalError(e)
                };
            }
            finally
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.None;
            }
        }

        /// <summary>
        /// Handles workflow signals.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal async Task<WorkflowSignalInvokeReply> OnSignalAsync(WorkflowSignalInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            // Handle synchronous signals in a specialized method.

            if (request.SignalName == TemporalClient.SignalSync)
            {
                return await OnSyncSignalAsync(request);
            }

            try
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.Signal;

                var workflow = GetWorkflow(request.ContextId);

                if (workflow != null)
                {
                    Workflow.Current = workflow.Workflow;   // Initialize the ambient workflow information.

                    var method = workflow.Workflow.MethodMap.GetSignalMethod(request.SignalName);

                    if (method != null)
                    {
                        await (Task)(method.Invoke(workflow, TemporalHelper.BytesToArgs(Client.DataConverter, request.SignalArgs, method.GetParameterTypes())));

                        return new WorkflowSignalInvokeReply()
                        {
                            RequestId = request.RequestId
                        };
                    }
                    else
                    {
                        return new WorkflowSignalInvokeReply()
                        {
                            Error = new EntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a signal handler for [signalName={request.SignalName}].").ToTemporalError()
                        };
                    }
                }
                else
                {
                    // I don't believe we'll ever land here because that would mean that
                    // Temporal sends signals to a workflow that hasn't started running
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
                    Error = new TemporalError(e)
                };
            }
            finally
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.None;
            }
        }

        /// <summary>
        /// Handles internal <see cref="TemporalClient.SignalSync"/> workflow signals.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal async Task<WorkflowSignalInvokeReply> OnSyncSignalAsync(WorkflowSignalInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            try
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.Signal;

                var workflow = GetWorkflow(request.ContextId);

                if (workflow != null)
                {
                    Workflow.Current = workflow.Workflow;   // Initialize the ambient workflow information.

                    // The signal arguments should be just a single [SyncSignalCall] that specifies
                    // the target signal and also includes its encoded arguments.

                    var signalCallArgs = TemporalHelper.BytesToArgs(JsonDataConverter.Instance, request.SignalArgs, new Type[] { typeof(SyncSignalCall) });
                    var signalCall     = (SyncSignalCall)signalCallArgs[0];
                    var signalMethod   = workflow.Workflow.MethodMap.GetSignalMethod(signalCall.TargetSignal);
                    var userSignalArgs = TemporalHelper.BytesToArgs(Client.DataConverter, signalCall.UserArgs, signalMethod.GetParameterTypes());

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

                    var signalStatus = workflow.SetSignalStatus(signalCall.SignalId, args, out var newSignal);

                    if (newSignal && signalMethod != null)
                    {
                        Workflow.Current = workflow.Workflow;   // Initialize the ambient workflow information for workflow library code.

                        var result    = (object)null;
                        var exception = (Exception)null;

                        // Execute the signal method (if there is one).

                        try
                        {
                            if (TemporalHelper.IsTask(signalMethod.ReturnType))
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
                            var syncSignalStatus = workflow.GetSignalStatus(signalCall.SignalId);

                            if (syncSignalStatus != null)
                            {
                                if (exception == null)
                                {
                                    syncSignalStatus.Result = Client.DataConverter.ToData(result);
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
                                Covenant.Assert(false); // This should never happen.
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
                            Error = new EntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a signal handler for [signalName={request.SignalName}].").ToTemporalError()
                        };
                    }
                }
                else
                {
                    // I don't believe we'll ever land here because that would mean that
                    // Temporal sends signals to a workflow that hasn't started running
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
                    Error = new TemporalError(e)
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
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal async Task<WorkflowQueryInvokeReply> OnQueryAsync(WorkflowQueryInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            try
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.Query;

                var workflow = GetWorkflow(request.ContextId);

                if (workflow != null)
                {
                    Workflow.Current = workflow.Workflow;   // Initialize the ambient workflow information for workflow library code.

                    // Handle built-in queries.

                    switch (request.QueryName)
                    {
                        case TemporalClient.QueryStack:

                            var trace = string.Empty;

                            if (workflow.StackTrace != null)
                            {
                                trace = workflow.StackTrace.ToString();
                            }

                            return new WorkflowQueryInvokeReply()
                            {
                                RequestId = request.RequestId,
                                Result    = Client.DataConverter.ToData(trace)
                            };

                        case TemporalClient.QuerySyncSignal:

                            // The arguments for this signal is the (string) ID of the target
                            // signal being polled for status.

                            var syncSignalArgs   = TemporalHelper.BytesToArgs(JsonDataConverter.Instance, request.QueryArgs, new Type[] { typeof(string) });
                            var syncSignalId     = (string) (syncSignalArgs.Length > 0 ? syncSignalArgs[0] : null);
                            var syncSignalStatus = workflow.GetSignalStatus(syncSignalId);

                            Covenant.Assert(false); // This should never happen

                            if (syncSignalStatus.Completed)
                            {
                                // Indicate that the completed signal has reported the status
                                // to the calling client as well as returned the result, if any.

                                syncSignalStatus.Acknowledged    = true;
                                syncSignalStatus.AcknowledgeTime = DateTime.UtcNow;
                            }

                            return new WorkflowQueryInvokeReply()
                            {
                                RequestId = request.RequestId,
                                Result    = Client.DataConverter.ToData(syncSignalStatus)
                            };
                    }

                    // Handle user queries.

                    var method = workflow.Workflow.MethodMap.GetQueryMethod(request.QueryName);

                    if (method != null)
                    {
                        var resultType           = method.ReturnType;
                        var methodParameterTypes = method.GetParameterTypes();

                        var serializedResult = Array.Empty<byte>();

                        if (resultType.IsGenericType)
                        {
                            // Query method returns: Task<T>

                            var result = await NeonHelper.GetTaskResultAsObjectAsync((Task)method.Invoke(workflow, TemporalHelper.BytesToArgs(Client.DataConverter, request.QueryArgs, methodParameterTypes)));

                            serializedResult = Client.DataConverter.ToData(result);
                        }
                        else
                        {
                            // Query method returns: Task

                            await (Task)method.Invoke(workflow, TemporalHelper.BytesToArgs(Client.DataConverter, request.QueryArgs, methodParameterTypes));
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
                            Error = new EntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a query handler for [queryType={request.QueryName}].").ToTemporalError()
                        };
                    }
                }
                else
                {
                    return new WorkflowQueryInvokeReply()
                    {
                        Error = new EntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToTemporalError()
                    };
                }
            }
            catch (Exception e)
            {
                log.LogError(e);

                return new WorkflowQueryInvokeReply()
                {
                    Error = new TemporalError(e)
                };
            }
            finally
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.None;
            }
        }
    }
}
