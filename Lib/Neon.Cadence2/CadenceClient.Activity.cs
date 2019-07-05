//-----------------------------------------------------------------------------
// FILE:	    CadenceClient.Activity.cs
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
using System.Reflection;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    public partial class CadenceClient
    {
        //---------------------------------------------------------------------
        // Cadence activity related operations.

        /// <summary>
        /// Registers an activity implementation with Cadence.
        /// </summary>
        /// <typeparam name="TActivity">The <see cref="ActivityBase"/> derived type implementing the activity.</typeparam>
        /// <param name="activityTypeName">
        /// Optionally specifies a custom activity type name that will be used 
        /// for identifying the activity implementation in Cadence.  This defaults
        /// to the fully qualified <typeparamref name="TActivity"/> type name.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a different activity class has already been registered for <paramref name="activityTypeName"/>.</exception>
        /// <exception cref="CadenceActivityWorkerStartedException">
        /// Thrown if an activity worker has already been started for the client.  You must
        /// register activity implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your activity implementations before starting a workflow worker.
        /// </note>
        /// </remarks>
        public async Task RegisterActivityAsync<TActivity>(string activityTypeName = null)
            where TActivity : ActivityBase
        {
            if (string.IsNullOrEmpty(activityTypeName))
            {
                activityTypeName = activityTypeName ?? typeof(TActivity).FullName;
            }

            if (activityWorkerStarted)
            {
                throw new CadenceActivityWorkerStartedException();
            }

            if (!ActivityBase.Register(this, typeof(TActivity), activityTypeName))
            {
                var reply = (ActivityRegisterReply)await CallProxyAsync(
                    new ActivityRegisterRequest()
                    {
                        Name = activityTypeName
                    });

                reply.ThrowOnError();
            }            
        }

        /// <summary>
        /// Scans the assembly passed looking for activity implementations derived from
        /// <see cref="ActivityBase"/> and tagged with <see cref="AutoRegisterAttribute"/>
        /// and registers them with Cadence.
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TypeLoadException">
        /// Thrown for types tagged by <see cref="AutoRegisterAttribute"/> that are not 
        /// derived from <see cref="WorkflowBase"/> or <see cref="ActivityBase"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown if one of the tagged classes conflict with an existing registration.</exception>
        /// <exception cref="CadenceActivityWorkerStartedException">
        /// Thrown if an activity worker has already been started for the client.  You must
        /// register activity implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your activity implementations before starting a workflow worker.
        /// </note>
        /// </remarks>
        public async Task RegisterAssemblyActivitiesAsync(Assembly assembly)
        {
            Covenant.Requires<ArgumentNullException>(assembly != null);

            if (activityWorkerStarted)
            {
                throw new CadenceActivityWorkerStartedException();
            }

            foreach (var type in assembly.GetTypes())
            {
                var autoRegisterAttribute = type.GetCustomAttribute<AutoRegisterAttribute>();

                if (autoRegisterAttribute != null)
                {
                    if (type.IsSubclassOf(typeof(WorkflowBase)))
                    {
                        // Ignore these here.
                    }
                    else if (type.IsSubclassOf(typeof(ActivityBase)))
                    {
                        var activityTypeName = autoRegisterAttribute.TypeName ?? type.FullName;

                        if (!ActivityBase.Register(this, type, activityTypeName))
                        {
                            var reply = (ActivityRegisterReply)await CallProxyAsync(
                                new ActivityRegisterRequest()
                                {
                                    Name = activityTypeName
                                });

                            reply.ThrowOnError();
                        }
                    }
                    else
                    {
                        throw new TypeLoadException($"Type [{type.FullName}] is tagged by [{nameof(AutoRegisterAttribute)}] but is not derived from [{nameof(WorkflowBase)}].");
                    }
                }
            }
        }

        /// <summary>
        /// Used to send an activity heartbeat externally.
        /// </summary>
        /// <param name="taskToken">The opaque activity task token.</param>
        /// <param name="details">Optional heartbeart details.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task SendActivityHeartbeatAsync(byte[] taskToken, byte[] details = null)
        {
            Covenant.Requires<ArgumentNullException>(taskToken != null && taskToken.Length > 0);
            
            var reply = (ActivityRecordHeartbeatReply)await CallProxyAsync(
                new ActivityRecordHeartbeatRequest()
                {
                    TaskToken = taskToken,
                    Details   = details
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to complete an activity externally.
        /// </summary>
        /// <param name="taskToken">The opaque activity task token.</param>
        /// <param name="result">Passed as the activity result for activity success.</param>
        /// <param name="e">Passed as an exception when the activity failed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the activity no longer exists.</exception>
        public async Task CompleteActivityAsync(byte[] taskToken, byte[] result = null, Exception e = null)
        {
            Covenant.Requires<ArgumentNullException>(taskToken != null && taskToken.Length > 0);

            var reply = (ActivityCompleteReply)await CallProxyAsync(
                new ActivityCompleteRequest()
                {
                    TaskToken = taskToken,
                    Result    = result,
                    Error     = e != null ? new CadenceError(e) : null
                });

            reply.ThrowOnError();
        }
    }
}
