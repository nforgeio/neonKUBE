//-----------------------------------------------------------------------------
// FILE:	    ActivityStub.cs
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Used to execute an untyped activity whose .NET type information is not known
    /// at runtime or an activity written in different languages.
    /// </summary>
    public class ActivityStub
    {
        private TemporalClient      client;
        private Workflow            parentWorkflow;
        private string              activityTypeName;
        private ActivityOptions     options;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="parentWorkflow">Identifies the parent workflow.</param>
        /// <param name="activityTypeName">Specifies the target activity type name.</param>
        /// <param name="options">Optionally specifies custom activity options.</param>
        /// <remarks>
        /// <para>
        /// <paramref name="activityTypeName"/> specifies the target activity implementation type name and optionally,
        /// the specific activity method to be called for activity interfaces that have multiple methods.  For
        /// activity methods tagged by <c>ActivityMethod]</c>[ with specifying a name, the activity type name will default
        /// to the fully qualified interface type name or the custom type name specified by <see cref="ActivityAttribute.Name"/>.
        /// </para>
        /// <para>
        /// For activity methods with <see cref="ActivityMethodAttribute.Name"/> specified, the activity type will
        /// look like this by default:
        /// </para>
        /// <code>
        /// ACTIVITY-TYPE-NAME::METHOD-NAME
        /// </code>
        /// <note>
        /// You may need to customize activity type name when interoperating with activities written
        /// in other languages.  See <a href="https://doc.neonkube.com/Neon.Temporal-CrossPlatform.htm">Cadence Cross-Platform</a>
        /// for more information.
        /// </note>
        /// </remarks>
        internal ActivityStub(TemporalClient client, Workflow parentWorkflow, string activityTypeName, ActivityOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName), nameof(activityTypeName));

            this.client           = client;
            this.parentWorkflow   = parentWorkflow;
            this.activityTypeName = activityTypeName;
            this.options          = options;
        }

        /// <summary>
        /// Executes an activity that doesn't return a result (or when the caller doesn't
        /// care about the result).
        /// </summary>
        /// <param name="args">The activity arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ExecuteAsync(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(activityTypeName != null, nameof(activityTypeName));

            var dataConverter = client.DataConverter;
            var argBytes      = dataConverter.ToDataArray(args);
            
            await parentWorkflow.ExecuteActivityAsync(activityTypeName, argBytes, options);
        }

        /// <summary>
        /// Executes an activity by activity type name that returns the <typeparamref name="TResult"/>.
        /// </summary>
        /// <typeparam name="TResult">The activity result type. </typeparam>
        /// <param name="args">The activity arguments.</param>
        /// <returns>The activity result.</returns>
        public async Task<TResult> ExecuteAsync<TResult>(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(activityTypeName != null, nameof(activityTypeName));

            var dataConverter = client.DataConverter;
            var argBytes      = dataConverter.ToDataArray(args);
            var resultBytes   = await parentWorkflow.ExecuteActivityAsync(activityTypeName, argBytes, options);

            return dataConverter.FromData<TResult>(resultBytes);
        }
    }
}
