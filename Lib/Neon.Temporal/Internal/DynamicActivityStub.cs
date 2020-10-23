//-----------------------------------------------------------------------------
// FILE:	    DynamicWorkflowStub.cs
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
using System.IO;
using System.Reflection;
using System.Runtime;
using System.Runtime.Loader;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Common;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Manages a dynamically generated type safe activity stub class for an activity interface.
    /// </summary>
    internal class DynamicActivityStub
    {
        private Type                activityInterface;
        private string              className;
        private Assembly            assembly;
        private Type                stubType;
        private ConstructorInfo     normalConstructor;
        private ConstructorInfo     localConstructor;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="activityInterface">Specifies the activity interface.</param>
        /// <param name="assembly">The assembly holding the generated stub class.</param>
        /// <param name="className">The fully qualified stub class name.</param>
        public DynamicActivityStub(Type activityInterface, Assembly assembly, string className)
        {
            this.activityInterface = activityInterface;
            this.assembly          = assembly;
            this.className         = className;

            // Fetch the stub type and reflect the required constructors.

            this.stubType          = assembly.GetType(className);
            this.normalConstructor = NeonHelper.GetConstructor(stubType, typeof(TemporalClient), typeof(IDataConverter), typeof(Workflow), typeof(string), typeof(ActivityOptions), typeof(Type));
            this.localConstructor  = NeonHelper.GetConstructor(stubType, typeof(TemporalClient), typeof(IDataConverter), typeof(Workflow), typeof(Type), typeof(LocalActivityOptions), typeof(Type));
        }

        /// <summary>
        /// Creates a normal (non-local) activity stub instance suitable for executing a non-local activity.
        /// </summary>
        /// <param name="client">The associated <see cref="TemporalClient"/>.</param>
        /// <param name="workflow">The parent workflow.</param>x
        /// <param name="activityTypeName">Specifies the activity type name.</param>
        /// <param name="options">Specifies the <see cref="ActivityOptions"/> or <c>null</c>.</param>
        /// <param name="activityInterface">Specifies the activity interface definition.</param>
        /// <returns>The activity stub as an <see cref="object"/>.</returns>
        public object Create(TemporalClient client, Workflow workflow, string activityTypeName, ActivityOptions options, System.Type activityInterface)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(workflow != null, nameof(workflow));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName), nameof(activityTypeName));
            Covenant.Requires<ArgumentNullException>(activityInterface != null, nameof(activityInterface));

            options = options ?? new ActivityOptions();

            return normalConstructor.Invoke(new object[] { client, client.DataConverter, workflow, activityTypeName, options, activityInterface });
        }

        /// <summary>
        /// Creates a local activity stub instance suitable for executing a non-local activity.
        /// </summary>
        /// <param name="client">The associated <see cref="TemporalClient"/>.</param>
        /// <param name="workflow">The parent workflow.</param>
        /// <param name="activityType">The activity implementation type.</param>
        /// <param name="options">Specifies the <see cref="LocalActivityOptions"/> or <c>null</c>.</param>
        /// <returns>The activity stub as an <see cref="object"/>.</returns>
        public object CreateLocal(TemporalClient client, Workflow workflow, Type activityType, LocalActivityOptions options)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(workflow != null, nameof(workflow));
            Covenant.Requires<ArgumentNullException>(activityType != null, nameof(activityType));

            options = options ?? new LocalActivityOptions();

            return localConstructor.Invoke(new object[] { client, client.DataConverter, workflow, activityType, options, TemporalHelper.GetActivityInterface(activityType) });
        }
    }
}
