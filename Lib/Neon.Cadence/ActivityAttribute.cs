//-----------------------------------------------------------------------------
// FILE:	    ActivityAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Used to tag activity implementations that inherit from
    /// <see cref="ActivityBase"/> to customize the how the activity is
    /// registered.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ActivityAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">
        /// Optionally specifies the activity type name used to
        /// register an activity implementation with Cadence.
        /// </param>
        public ActivityAttribute(string name = null)
        {
            CadenceHelper.ValidateActivityTypeName(name);

            this.Name = name;
        }

        /// <summary>
        /// The activity type name.  This defaults to the fully qualified name
        /// of the implemented activity interface (without an leading "I").
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Indicates that <see cref="CadenceClient.RegisterAssemblyActivitiesAsync(Assembly, string)"/> will
        /// automatically register the tagged activity implementation for the specified assembly.
        /// This defaults to <c>false</c>
        /// </summary>
        public bool AutoRegister { get; set; } = false;
    }
}
