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
using System.Reflection;
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
    /// Base class that can be used for Cadence workflow implementations.
    /// </summary>
    public class WorkflowBase : IWorkflowBase
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to map a Cadence client ID and workflow context ID into a
        /// key that can be used to dereference <see cref="idToWorkflow"/>.
        /// </summary>
        private struct WorkflowKey
        {
            private long clientId;
            private long contextId;

            public WorkflowKey(CadenceClient client, long contextId)
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
                if (obj == null || !(obj is WorkflowKey))
                {
                    return false;
                }

                var other = (WorkflowKey)obj;

                return this.clientId == other.clientId && 
                       this.contextId == other.contextId;
            }

            public override string ToString()
            {
                return $"clientID={clientId}, contextId={contextId}";
            }
        }

        /// <summary>
        /// Private activity used to set/get variable values.  This activity simply
        /// returns the arguments passed such that they'll be recorded in the workflow
        /// history.  This is intended to be executed as a local activity.
        /// </summary>
        private class VariableActivity : ActivityBase
        {
            protected override Task<byte[]> RunAsync(byte[] args)
            {
                return Task.FromResult(args);
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
            public MethodInfo EntryPoint { get; set; }

            /// <summary>
            /// Maps workflow signal and query names to the corresponding
            /// method implementations.
            /// </summary>
            public WorkflowMethodMap MethodMap { get; set; }
        }

        //---------------------------------------------------------------------
        // Static members

        private static object                                   syncLock           = new object();
        private static INeonLogger                              log                = LogManager.Default.GetLogger<WorkflowBase>();
        private static Dictionary<WorkflowKey, IWorkflowBase>   idToWorkflow       = new Dictionary<WorkflowKey, IWorkflowBase>();
        private static Dictionary<Type, WorkflowRegistration>   typeToRegistration = new Dictionary<Type, WorkflowRegistration>();

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
        /// Prepends the Cadence client ID to the workflow type name to generate the
        /// key used to dereference the <see cref="nameToWorkflowType"/> dictionary.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="workflowMethod">
        /// Optionally identifies the workflow (entry point) method name.  
        /// <paramref name="workflowTypeName"/> will be assumed to already
        /// include the method component then this is <c>null</c>.
        /// </param>
        /// <returns>The prepended type name.</returns>
        private static string GetWorkflowTypeKey(CadenceClient client, string workflowTypeName, MethodInfo workflowMethod = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(workflowTypeName != null);
            Covenant.Requires<ArgumentNullException>(workflowMethod != null);

            if (workflowMethod == null)
            {
                return $"{client.ClientId}::{workflowTypeName}";
            }

            var workflowMethodAttribute = workflowMethod.GetCustomAttribute<WorkflowMethodAttribute>();

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
        /// Registers a workflow implementation.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="workflowType">The workflow implementation type.</param>
        /// <param name="workflowTypeName">The name used to identify the implementation.</param>
        /// <returns><c>true</c> if the workflow was already registered.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a different workflow class has already been registered for <paramref name="workflowTypeName"/>.</exception>
        internal static bool Register(CadenceClient client, Type workflowType, string workflowTypeName)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            CadenceHelper.ValidateWorkflowImplementation(workflowType);

            // We need to register each workflow method defined for the workflow.

            var methodMap = WorkflowMethodMap.Create(workflowType);

            foreach (var method in workflowType.GetMethods())
            {
                var workflowMethodAttribute = method.GetCustomAttribute<WorkflowMethodAttribute>();

                if (workflowMethodAttribute == null)
                {
                    continue;
                }

                workflowTypeName = GetWorkflowTypeKey(client, workflowTypeName, method);

                lock (syncLock)
                {
                    if (nameToRegistration.TryGetValue(workflowTypeName, out var existingRegistration))
                    {
                        if (!object.ReferenceEquals(existingRegistration.WorkflowType, workflowType))
                        {
                            throw new InvalidOperationException($"Conflicting workflow interface registration: Workflow interface [{workflowType.FullName}] is already registered for workflow type name [{workflowTypeName}].");
                        }

                        return true;
                    }
                    else
                    {
                        nameToRegistration[workflowTypeName] =
                            new WorkflowRegistration()
                            {
                                WorkflowType = workflowType,
                                EntryPoint   = method,
                                MethodMap    = methodMap
                            };
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Removes all type workflow interface registrations for a Cadence client (when it's being disposed).
        /// </summary>
        /// <param name="client">The client being disposed.</param>
        internal static void UnregisterClient(CadenceClient client)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

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
        /// Returns the .NET type implementing the named Cadence workflow.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="workflowTypeName">The Cadence workflow type name.</param>
        /// <returns>The workflow .NET type or <c>null</c> if the type was not found.</returns>
        private static Type GetWorkflowType(CadenceClient client, string workflowTypeName)
        {
            Covenant.Requires<ArgumentNullException>(workflowTypeName != null);

            lock (syncLock)
            {
                if (nameToRegistration.TryGetValue(GetWorkflowTypeKey(client, workflowTypeName), out var registration))
                {
                    return registration.WorkflowType;
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

                    reply = await OnSignalAsync(client, (WorkflowSignalInvokeRequest)request);
                    break;

                case InternalMessageTypes.WorkflowQueryInvokeRequest:

                    reply = await OnQueryAsync(client, (WorkflowQueryInvokeRequest)request);
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
        /// <param name="client">The Cadence client.</param>
        /// <param name="contextId">The workflow's context ID.</param>
        /// <returns>The <see cref="WorkflowBase"/> instance or <c>null</c> if the workflow was not found.</returns>
        private static IWorkflowBase GetWorkflow(CadenceClient client, long contextId)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            lock (syncLock)
            {
                if (idToWorkflow.TryGetValue(new WorkflowKey(client, contextId), out var workflow))
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

            IWorkflowBase   workflow;
            Type            workflowType;

            var contextId   = request.ContextId;
            var workflowKey = new WorkflowKey(client, contextId);

            lock (syncLock)
            {
                if (idToWorkflow.TryGetValue(workflowKey, out workflow))
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"A workflow with [ID={workflowKey}] is already running on this worker.")
                    };
                }

                workflowType = GetWorkflowType(client, request.WorkflowType);

                if (workflowType == null)
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"A workflow [Type={request.WorkflowType}] is not registered for this worker.")
                    };
                }
            }

            workflow = (IWorkflowBase)Activator.CreateInstance(workflowType);

            workflow.Initialize(client, contextId);

            lock (syncLock)
            {
                idToWorkflow.Add(workflowKey, workflow);
            }

            // Initialize the other workflow properties.

            workflow.Client           = client;
            workflow.contextId        = request.ContextId;
            workflow.Domain           = request.Domain;
            workflow.RunId            = request.RunId;
            workflow.TaskList         = request.TaskList;
            workflow.WorkflowId       = request.WorkflowId;
            workflow.WorkflowTypeName = request.WorkflowType;

            // Register any workflow query or signal handlers.

            workflow.RegisterHandlers(client, contextId);

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
        /// <param name="client">The Cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowSignalInvokeReply> OnSignalAsync(CadenceClient client, WorkflowSignalInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    var method = workflow.methodMap.GetSignalMethod(request.SignalName);

                    if (method != null)
                    {
                        await (Task)(method.Invoke(workflow, new object[] { request.SignalArgs }));

                        return new WorkflowSignalInvokeReply()
                        {
                            RequestId = request.RequestId
                        };
                    }
                    else
                    {
                        return new WorkflowSignalInvokeReply()
                        {
                            Error = new CadenceEntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a signal handler for [signalName={request.SignalName}].").ToCadenceError()
                        };
                    }
                }
                else
                {
                    // It's possible that we'll land here if the workflow has been scheduled
                    // and/or started but execution has not actually started.  Since signals
                    // are fire-and-forget, we're just going to ignore these here.

                    return new WorkflowSignalInvokeReply();
                }
            }
            catch (Exception e)
            {
                return new WorkflowSignalInvokeReply()
                {
                    Error = new CadenceError(e)
                };
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
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    var method = workflow.methodMap.GetQueryMethod(request.QueryName);

                    if (method != null)
                    {
                        var result = await (Task<byte[]>)(method.Invoke(workflow, new object[] { request.QueryArgs }));

                        return new WorkflowQueryInvokeReply()
                        {
                            RequestId = request.RequestId,
                            Result    = result
                        };
                    }
                    else
                    {
                        return new WorkflowQueryInvokeReply()
                        {
                            Error = new CadenceEntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a query handler for [queryType={request.QueryName}].").ToCadenceError()
                        };
                    }
                }
                else
                {
                    return new WorkflowQueryInvokeReply()
                    {
                        Error = new CadenceEntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToCadenceError()
                    };
                }
            }
            catch (Exception e)
            {
                return new WorkflowQueryInvokeReply()
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
        internal static async Task<ActivityInvokeLocalReply> OnInvokeLocalActivity(CadenceClient client, ActivityInvokeLocalRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    Type activityType;

                    lock (syncLock)
                    {
                        if (!workflow.idToLocalActivityType.TryGetValue(request.ActivityTypeId, out activityType))
                        {
                            return new ActivityInvokeLocalReply()
                            {
                                Error = new CadenceEntityNotExistsException($"Activity type does not exist for [activityTypeId={request.ActivityTypeId}].").ToCadenceError()
                            };
                        }
                    }

                    var workerArgs = new WorkerArgs() { Client = client, ContextId = request.ActivityContextId };
                    var activity   = Activity.Create(client, activityType, null);
                    var result     = await activity.OnRunAsync(client, request.Args);

                    return new ActivityInvokeLocalReply()
                    {
                        Result = result
                    };
                }
                else
                {
                    return new ActivityInvokeLocalReply()
                    {
                        Error = new CadenceEntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToCadenceError()
                    };
                }
            }
            catch (Exception e)
            {
                return new ActivityInvokeLocalReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <inheritdoc/>
        public Workflow Workflow { get; private set; }
    }
}
