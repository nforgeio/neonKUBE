//-----------------------------------------------------------------------------
// FILE:	    Worker.Activity.cs
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

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
        // Activity related private types

        /// <summary>
        /// Used for mapping an activity type name to its underlying type
        /// and entry point method.
        /// </summary>
        private struct ActivityRegistration
        {
            /// <summary>
            /// The activity type.
            /// </summary>
            public Type ActivityType { get; set; }

            /// <summary>
            /// The activity entry point method.
            /// </summary>
            public MethodInfo ActivityMethod { get; set; }

            /// <summary>
            /// The activity method parameter types.
            /// </summary>
            public Type[] ActivityMethodParameterTypes { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private List<Type>                                  registeredActivityTypes    = new List<Type>();
        private Dictionary<string, ActivityRegistration>    nameToActivityRegistration = new Dictionary<string, ActivityRegistration>();
        private Dictionary<long, ActivityBase>              idToActivity               = new Dictionary<long, ActivityBase>();

        /// <summary>
        /// Registers an activity implementation with the temporal-proxy.
        /// </summary>
        /// <param name="activityType">The activity implementation type.</param>
        /// <exception cref="RegistrationException">Thrown when there's a problem with the registration.</exception>
        private async Task RegisterActivityImplementationAsync(Type activityType)
        {
            await SyncContext.Clear();
            TemporalHelper.ValidateActivityImplementation(activityType);

            var interfaceType = TemporalHelper.GetActivityInterface(activityType);

            TemporalHelper.ValidateActivityInterface(interfaceType);

            // We need to register each activity method that implements an activity interface method
            // with the same signature that that was tagged by [ActivityMethod].
            //
            // First, we'll create a dictionary that maps method signatures from any inherited
            // interfaces that are tagged by [ActivityMethod] to the attribute.

            var methodSignatureToAttribute = new Dictionary<string, ActivityMethodAttribute>();

            foreach (var method in interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var activityMethodAttribute = method.GetCustomAttribute<ActivityMethodAttribute>();

                if (activityMethodAttribute == null)
                {
                    continue;
                }

                var signature = method.ToString();

                if (methodSignatureToAttribute.ContainsKey(signature))
                {
                    throw new NotSupportedException($"Activity type [{activityType.FullName}] cannot implement the [{signature}] method from two different interfaces.");
                }

                methodSignatureToAttribute.Add(signature, activityMethodAttribute);
            }

            // Next, we need to register the activity methods that implement the
            // activity interface.

            foreach (var method in activityType.GetMethods())
            {
                if (!methodSignatureToAttribute.TryGetValue(method.ToString(), out var activityMethodAttribute))
                {
                    continue;
                }

                var activityTarget = TemporalHelper.GetActivityTarget(interfaceType, activityMethodAttribute.Name);

                lock (nameToActivityRegistration)
                {
                    if (nameToActivityRegistration.TryGetValue(activityTarget.ActivityTypeName, out var registration))
                    {
                        if (!object.ReferenceEquals(registration.ActivityType, registration.ActivityType))
                        {
                            throw new InvalidOperationException($"Conflicting activity type registration: Activity type [{activityType.FullName}] is already registered for activity type name [{activityTarget.ActivityTypeName}].");
                        }
                    }
                    else
                    {
                        nameToActivityRegistration[activityTarget.ActivityTypeName] =
                            new ActivityRegistration()
                            {
                                ActivityType                 = activityType,
                                ActivityMethod               = method,
                                ActivityMethodParameterTypes = method.GetParameterTypes()
                            };
                    }
                }

                var reply = (ActivityRegisterReply)await Client.CallProxyAsync(
                    new ActivityRegisterRequest()
                    {
                        WorkerId = WorkerId,
                        Name     = activityTarget.ActivityTypeName,
                    });

                reply.ThrowOnError();
            }
        }

        /// <summary>
        /// Registers an activity implementation with Temporal.
        /// </summary>
        /// <typeparam name="TActivity">The <see cref="ActivityBase"/> derived class implementing the activity.</typeparam>
        /// <param name="disableDuplicateCheck">Disable checks for duplicate activity registrations.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the worker has already been started.  You must register workflow 
        /// and activity implementations before starting a worker.
        /// </exception>
        /// <exception cref="RegistrationException">Thrown when there's a problem with the registration.</exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all services you will be injecting into activities via
        /// <see cref="NeonHelper.ServiceContainer"/> before you call this as well as 
        /// registering of your activity implementations before starting a worker.
        /// </note>
        /// </remarks>
        public async Task RegisterActivityAsync<TActivity>(bool disableDuplicateCheck = false)
            where TActivity : ActivityBase
        {
            await SyncContext.Clear();
            TemporalHelper.ValidateActivityImplementation(typeof(TActivity));
            EnsureNotDisposed();
            EnsureCanRegister();

            lock (registeredActivityTypes)
            {
                var activityType = typeof(TActivity);

                if (registeredActivityTypes.Contains(activityType))
                {
                    if (disableDuplicateCheck)
                    {
                        return;
                    }
                    else
                    {
                        throw new RegistrationException($"Activity implementation [{typeof(TActivity).FullName}] has already been registered.");
                    }
                }

                registeredActivityTypes.Add(activityType);
            }
        }

        /// <summary>
        /// Scans the assembly passed looking for activity implementations derived from
        /// <see cref="ActivityBase"/> and tagged by <see cref="ActivityAttribute"/> and
        /// registers them with Temporal.
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <param name="disableDuplicateCheck">Disable checks for duplicate activity registrations.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TypeLoadException">
        /// Thrown for types tagged by <see cref="ActivityAttribute"/> that are not 
        /// derived from <see cref="ActivityBase"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown if one of the tagged classes conflict with an existing registration.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the worker has already been started.  You must register workflow 
        /// and activity implementations before starting workers.
        /// </exception>
        /// <exception cref="RegistrationException">Thrown when there's a problem with the registration.</exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all services you will be injecting into activities via
        /// <see cref="NeonHelper.ServiceContainer"/> before you call this as well as 
        /// registering of your activity implementations before starting a worker.
        /// </note>
        /// </remarks>
        public async Task RegisterAssemblyActivitiesAsync(Assembly assembly, bool disableDuplicateCheck = false)
        {
            await SyncContext.Clear();
            Covenant.Requires<ArgumentNullException>(assembly != null, nameof(assembly));
            EnsureNotDisposed();
            EnsureCanRegister();

            lock (registeredActivityTypes)
            {
                foreach (var activityType in assembly.GetTypes().Where(type => type.IsClass))
                {
                    var activityAttribute = activityType.GetCustomAttribute<ActivityAttribute>();

                    if (activityAttribute != null && activityAttribute.AutoRegister)
                    {
                        var activityTypeName = TemporalHelper.GetActivityTypeName(activityType, activityAttribute);

                        if (registeredActivityTypes.Contains(activityType))
                        {
                            if (disableDuplicateCheck)
                            {
                                continue;
                            }
                            else
                            {
                                throw new RegistrationException($"Activity implementation [{activityType.FullName}] has already been registered.");
                            }
                        }

                        registeredActivityTypes.Add(activityType);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="ActivityRegistration"/> for an activity type and type name.
        /// </summary>
        /// <param name="activityType">The target activity type.</param>
        /// <param name="activityTypeName">The target activity type name.</param>
        /// <returns>The <see cref="ActivityRegistration"/>.</returns>
        private ActivityRegistration GetActivityInvokeInfo(Type activityType, string activityTypeName)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null, nameof(activityType));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName), nameof(activityTypeName));

            var info = new ActivityRegistration();

            // Locate the target method.  Note that the activity type name will be
            // formatted like:
            //
            //      TYPE-NAME
            // or   TYPE-NAME::METHOD-NAME

            var activityMethodName = (string)null;
            var separatorPos       = activityTypeName.IndexOf("::");

            if (separatorPos != -1)
            {
                activityMethodName = activityTypeName.Substring(separatorPos + 2);

                if (string.IsNullOrEmpty(activityMethodName))
                {
                    activityMethodName = null;
                }
            }

            foreach (var method in activityType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var activityMethodAttribute = method.GetCustomAttribute<ActivityMethodAttribute>();

                if (activityMethodAttribute == null)
                {
                    continue;
                }

                var name = activityMethodAttribute.Name;

                if (string.IsNullOrEmpty(name))
                {
                    name = null;
                }

                if (name == activityMethodName)
                {
                    info.ActivityMethod = method;
                    break;
                }
            }

            if (info.ActivityMethod == null)
            {
                throw new ArgumentException($"Activity type [{activityType.FullName}] does not have an entry point method tagged with [ActivityMethod(Name = \"{activityMethodName}\")].", nameof(activityType));
            }

            return info;
        }

        /// <summary>
        /// Constructs an activity instance suitable for executing a normal (non-local) activity.
        /// </summary>
        /// <param name="invokeInfo">The activity invocation information.</param>
        /// <param name="contextId">The activity context ID.</param>
        /// <returns>The constructed activity.</returns>
        private ActivityBase CreateNormalActivity(ActivityRegistration invokeInfo, long contextId)
        {
            var activity = (ActivityBase)ActivatorUtilities.CreateInstance(NeonHelper.ServiceContainer, invokeInfo.ActivityType);

            activity.IsLocal = false;
            activity.Initialize(this, invokeInfo.ActivityType, invokeInfo.ActivityMethod, this.Client.DataConverter, contextId);

            return activity;
        }

        /// <summary>
        /// Constructs an activity instance suitable for executing a local activity.
        /// </summary>
        /// <param name="activityAction">The target activity action.</param>
        /// <param name="contextId">The activity context ID.</param>
        /// <returns>The constructed activity.</returns>
        internal ActivityBase CreateLocalActivity(LocalActivityAction activityAction, long contextId)
        {
            var activity = (ActivityBase)ActivatorUtilities.CreateInstance(NeonHelper.ServiceContainer, activityAction.ActivityType);

            activity.IsLocal = true;
            activity.Initialize(this, activityAction.ActivityType, activityAction.ActivityMethod, this.Client.DataConverter, contextId);

            return activity;
        }

        /// <summary>
        /// Called to handle activity related request messages received from the <b>temporal-proxy</b>.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        internal async Task OnActivityProxyRequestAsync(ProxyRequest request)
        {
            await SyncContext.Clear();
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            ProxyReply reply;

            switch (request.Type)
            {
                case InternalMessageTypes.ActivityInvokeRequest:

                    reply = await OnActivityInvokeRequest((ActivityInvokeRequest)request);
                    break;

                case InternalMessageTypes.ActivityInvokeLocalRequest:

                    reply = await OnInvokeLocalActivity((ActivityInvokeLocalRequest)request);
                    break;

                case InternalMessageTypes.ActivityStoppingRequest:

                    reply = await ActivityStoppingRequest((ActivityStoppingRequest)request);
                    break;

                default:

                    throw new InvalidOperationException($"Unexpected message type [{request.Type}].");
            }

            await Client.ProxyReplyAsync(request, reply);
        }

        /// <summary>
        /// Handles received <see cref="ActivityInvokeRequest"/> messages.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        private async Task<ActivityInvokeReply> OnActivityInvokeRequest(ActivityInvokeRequest request)
        {
            await SyncContext.Clear();

            ActivityRegistration    invokeInfo;
            ActivityBase            activity;

            lock (nameToActivityRegistration)
            {
                if (!nameToActivityRegistration.TryGetValue(request.Activity, out invokeInfo))
                {
                    throw new KeyNotFoundException($"Cannot resolve [activityTypeName = {request.Activity}] to a registered activity type and activity method.");
                }

                activity = CreateNormalActivity(invokeInfo, request.ContextId);
                idToActivity.Add(request.ContextId, activity);
            }

            try
            {
                var result = await activity.OnInvokeAsync(request.Args);

                if (activity.CompleteExternally)
                {
                    return new ActivityInvokeReply()
                    {
                        Pending = true
                    };
                }
                else
                {
                    return new ActivityInvokeReply()
                    {
                        Result = result,
                    };
                }
            }
            catch (TemporalException e)
            {
                return new ActivityInvokeReply()
                {
                    Error = e.ToTemporalError()
                };
            }
            catch (TaskCanceledException e)
            {
                return new ActivityInvokeReply()
                {
                    Error = new CancelledException(e.Message).ToTemporalError()
                };
            }
            catch (Exception e)
            {
                return new ActivityInvokeReply()
                {
                    Error = new TemporalError(e)
                };
            }
            finally
            {
                lock (idToActivity)
                {
                    idToActivity.Remove(activity.ContextId);
                }
            }
        }

        /// <summary>
        /// Handles workflow local activity invocations.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal async Task<ActivityInvokeLocalReply> OnInvokeLocalActivity(ActivityInvokeLocalRequest request)
        {
            await SyncContext.Clear();
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            try
            {
                WorkflowBase.CallContext.Value = WorkflowCallContext.Activity;

                var workflow = GetWorkflow(request.ContextId);

                if (workflow != null)
                {
                    var activityAction = workflow.GetActivityAction(request.ActivityTypeId);

                    if (activityAction == null)
                    {
                        return new ActivityInvokeLocalReply()
                        {
                            Error = new EntityNotExistsException($"Activity type does not exist for [activityTypeId={request.ActivityTypeId}].").ToTemporalError()
                        };
                    }

                    var workerArgs = new WorkerArgs() { Worker = this, ContextId = request.ActivityContextId };
                    var activity   = CreateLocalActivity(activityAction, request.ActivityContextId);
                    var result     = await activity.OnInvokeAsync(request.Args);

                    return new ActivityInvokeLocalReply()
                    {
                        Result = result
                    };
                }
                else
                {
                    return new ActivityInvokeLocalReply()
                    {
                        Error = new EntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToTemporalError()
                    };
                }
            }
            catch (Exception e)
            {
                log.LogError(e);

                return new ActivityInvokeLocalReply()
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
        /// Handles received <see cref="ActivityStoppingRequest"/> messages.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        private async Task<ActivityStoppingReply> ActivityStoppingRequest(ActivityStoppingRequest request)
        {
            await SyncContext.Clear();

            ActivityBase    activity;

            lock (idToActivity)
            {
                if (idToActivity.TryGetValue(request.ContextId, out activity))
                {
                    idToActivity.Remove(request.ContextId);
                }
                else
                {
                    activity = null;
                }
            }

            activity?.CancellationTokenSource.Cancel();

            return await Task.FromResult(new ActivityStoppingReply());
        }
    }
}
