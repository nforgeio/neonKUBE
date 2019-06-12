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
        /// <typeparam name="TActivity">The <see cref="Activity"/> derived type implementing the activity.</typeparam>
        /// <param name="activityTypeName">
        /// Optionally specifies a custom activity type name that will be used 
        /// for identifying the activity implementation in Cadence.  This defaults
        /// to the fully qualified <typeparamref name="TActivity"/> type name.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task RegisterActivityAsync<TActivity>(string activityTypeName = null)
            where TActivity : Activity
        {
            if (string.IsNullOrEmpty(activityTypeName))
            {
                activityTypeName = activityTypeName ?? typeof(TActivity).FullName;
            }

            var reply = (ActivityRegisterReply)await CallProxyAsync(
                new ActivityRegisterRequest()
                {
                    Name = activityTypeName
                });

            reply.ThrowOnError();

            Activity.Register<TActivity>(activityTypeName);
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
