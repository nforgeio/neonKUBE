//-----------------------------------------------------------------------------
// FILE:	    AutoRegisterAttribute.cs
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

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Use this to tag workflow and activity implementations that inherit from
    /// <see cref="Workflow"/> and <see cref="Activity"/> such that calls
    /// to <see cref="CadenceClient.RegisterAssemblyWorkflowsAsync(Assembly)"/> and
    /// <see cref="CadenceClient.RegisterAssemblyActivitiesAsync(Assembly)"/> can 
    /// automatically register the tagged classes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoRegisterAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="typeName">
        /// Optionally specifies the workflow or activity type name to override the
        /// tagged class' fully qualified type name as the workflow or activity type
        /// name used to register the type with Cadence.
        /// </param>
        public AutoRegisterAttribute(string typeName = null)
        {
            Covenant.Requires<ArgumentException>(typeName == null || typeName.Length > 0, $"[{nameof(typeName)}] cannot be empty.");

            this.TypeName = typeName;
        }

        /// <summary>
        /// Returns the type name.
        /// </summary>
        public string TypeName { get; private set; }
    }
}
