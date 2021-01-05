//-----------------------------------------------------------------------------
// FILE:	    WorkflowAttribute.cs
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
using System.Reflection;

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Used to tag workflow implementations that inherit from
    /// <see cref="WorkflowBase"/> to customize the how the workflow is
    /// registered.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class WorkflowAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public WorkflowAttribute()
        {
        }

        /// <summary>
        /// Indicates that <see cref="Worker.RegisterAssemblyWorkflowsAsync(Assembly, bool)"/> will
        /// automatically register the tagged workflow implementation for the specified assembly.
        /// This defaults to <c>false</c>
        /// </summary>
        public bool AutoRegister { get; set; } = false;
    }
}
