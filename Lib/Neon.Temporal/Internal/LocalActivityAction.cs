//-----------------------------------------------------------------------------
// FILE:	    LocalActivityAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Diagnostics;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Holds information about the activity type to be instantiated and the
    /// method to be called when a local activity is invoked.
    /// </summary>
    internal class LocalActivityAction
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="activityConstructor">The activity constructor.</param>
        /// <param name="activityType">The target activity type.</param>
        /// <param name="activityMethod">The target activity method.</param>
        public LocalActivityAction(Type activityType, ConstructorInfo activityConstructor, MethodInfo activityMethod)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null, nameof(activityType));
            Covenant.Requires<ArgumentNullException>(activityConstructor != null, nameof(activityConstructor));
            Covenant.Requires<ArgumentNullException>(activityMethod != null, nameof(activityMethod));

            this.ActivityType        = activityType;
            this.ActivityConstructor = activityConstructor;
            this.ActivityMethod      = activityMethod;
        }

        /// <summary>
        /// The target activity type.
        /// </summary>
        public Type ActivityType { get; private set; }

        /// <summary>
        /// The target activity constructor.
        /// </summary>
        public ConstructorInfo ActivityConstructor { get; private set; }

        /// <summary>
        /// The target activity method.
        /// </summary>
        public MethodInfo ActivityMethod { get; private set; }
    }
}
