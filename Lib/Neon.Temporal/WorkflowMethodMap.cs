//-----------------------------------------------------------------------------
// FILE:	    WorkflowMethodMap.cs
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
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Maps workflow query and signal names to the methods implementing the queries
    /// and signals for a given workflow interface.
    /// </summary>
    internal class WorkflowMethodMap
    {
        //---------------------------------------------------------------------
        // Static members

        private static INeonLogger Log = LogManager.Default.GetLogger<WorkflowMethodMap>();

        /// <summary>
        /// Constructs a query/signal method map for a workflow type.
        /// </summary>
        /// <param name="workflowType">The workflow type.</param>
        /// <returns>The <see cref="WorkflowMethodMap"/>.</returns>
        public static WorkflowMethodMap Create(Type workflowType)
        {
            Covenant.Requires<ArgumentNullException>(workflowType != null, nameof(workflowType));
            Covenant.Requires<ArgumentException>(!workflowType.IsInterface, nameof(workflowType));

            var map = new WorkflowMethodMap();

            foreach (var @interface in workflowType.GetInterfaces())
            {
                foreach (var method in @interface.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    // Signal methods are tagged by [SignalMethod].

                    var signalMethodAttribute = method.GetCustomAttribute<SignalMethodAttribute>();

                    if (signalMethodAttribute != null)
                    {
                        if (signalMethodAttribute.Synchronous)
                        {
                            map.HasSynchronousSignals = true;
                        }

                        map.nameToSignalMethod[signalMethodAttribute.Name] = method;
                        continue;
                    }

                    // Query methods are tagged by [QueryMethod].

                    var queryMethodAttribute = method.GetCustomAttribute<QueryMethodAttribute>();

                    if (queryMethodAttribute != null)
                    {
                        map.nameToQueryMethod[queryMethodAttribute.Name] = method;
                        continue;
                    }
                }
            }

            return map;
        }

        //---------------------------------------------------------------------
        // Instance members.

        private Dictionary<string, MethodInfo> nameToSignalMethod = new Dictionary<string, MethodInfo>();
        private Dictionary<string, MethodInfo> nameToQueryMethod  = new Dictionary<string, MethodInfo>();

        /// <summary>
        /// Private constructor.
        /// </summary>
        private WorkflowMethodMap()
        {
        }

        /// <summary>
        /// Returns the <see cref="MethodInfo"/> for the handler for a given signal.
        /// </summary>
        /// <param name="name">Ths signal name.</param>
        /// <returns>
        /// The <see cref="MethodInfo"/> for the handler or <c>null</c> when there
        /// is no handler for the named signal.
        /// </returns>
        public MethodInfo GetSignalMethod(string name)
        {
            if (nameToSignalMethod.TryGetValue(name, out var methodInfo))
            {
                return methodInfo;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the <see cref="MethodInfo"/> for the handler for a given query.
        /// </summary>
        /// <param name="name">Ths query name.</param>
        /// <returns>
        /// The <see cref="MethodInfo"/> for the handler or <c>null</c> when there
        /// is no handler for the named query.
        /// </returns>
        public MethodInfo GetQueryMethod(string name)
        {
            if (nameToQueryMethod.TryGetValue(name, out var methodInfo))
            {
                return methodInfo;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the names of the mapped signals.
        /// </summary>
        /// <returns>The signal name list.</returns>
        public List<string> GetSignalNames()
        {
            return nameToSignalMethod.Keys.ToList();
        }

        /// <summary>
        /// Returns the names of the mapped queries.
        /// </summary>
        /// <returns>The query name list.</returns>
        public List<string> GetQueryTypes()
        {
            return nameToQueryMethod.Keys.ToList();
        }

        /// <summary>
        /// Returns <c>true</c> if the workflow defines any synchronous signals.
        /// </summary>
        public bool HasSynchronousSignals { get; private set; }
    }
}
