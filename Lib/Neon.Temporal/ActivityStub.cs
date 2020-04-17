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
    // $todo(jefflill): https://github.com/nforgeio/neonKUBE/issues/615

    /// <summary>
    /// Used to execute an activity whose .NET type information is not known
    /// at runtime or activities written in different languages.
    /// </summary>
    public class ActivityStub
    {
        /// <summary>
        /// Executes an activity by activity type name that doesn't return a result (or when the caller doesn't
        /// care about the result).
        /// </summary>
        /// <param name="activityTypeName">Identifies the activity to execute (see the remarks).</param>
        /// <param name="args">The activity arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <para>
        /// <paramref name="activityTypeName"/> specifies the target activity implementation type name and optionally,
        /// the specific activity method to be called for activity interfaces that have multiple methods.  For
        /// activity methods tagged by <c>ActivityMethod]</c>[ with specifying a name, the activity type name will default
        /// to the fully qualified interface type name or the custom type name specified by <see cref="ActivityAttribute.Name"/>.
        /// </para>
        /// <para>
        /// For activity methods with <see cref="ActivityMethodAttribute.Name"/> specified, the activity type will
        /// look like:
        /// </para>
        /// <code>
        /// ACTIVITY-TYPE-NAME::METHOD-NAME
        /// </code>
        /// <para>
        /// You'll need to use this format when calling activities using external untyped stubs or 
        /// from other languages.  The Java Temporal client works the same way.
        /// </para>
        /// </remarks>
        public Task ExecuteAsync(string activityTypeName, params object[] args)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Executes an activity by activity type name that returns the <typeparamref name="TResult"/>.
        /// </summary>
        /// <typeparam name="TResult">The activity result type. </typeparam>
        /// <param name="activityTypeName">Identifies the activity to execute (see the remarks).</param>
        /// <param name="args">The activity arguments.</param>
        /// <returns>The activity result.</returns>
        /// <remarks>
        /// <para>
        /// <paramref name="activityTypeName"/> specifies the target activity implementation type name and optionally,
        /// the specific activity method to be called for activity interfaces that have multiple methods.  For
        /// activity methods tagged by <c>ActivityMethod]</c>[ with specifying a name, the activity type name will default
        /// to the fully qualified interface type name or the custom type name specified by <see cref="ActivityAttribute.Name"/>.
        /// </para>
        /// <para>
        /// For activity methods with <see cref="ActivityMethodAttribute.Name"/> specified, the activity type will
        /// look like:
        /// </para>
        /// <code>
        /// ACTIVITY-TYPE-NAME::METHOD-NAME
        /// </code>
        /// <para>
        /// You'll need to use this format when calling activities using external untyped stubs or 
        /// from other languages.  The Java Temporal client works the same way.
        /// </para>
        /// </remarks>
        public Task<TResult> ExecuteAsync<TResult>(string activityTypeName, params object[] args)
        {
            throw new NotImplementedException();
        }
    }
}
