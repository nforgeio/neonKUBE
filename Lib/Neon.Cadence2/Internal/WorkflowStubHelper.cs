//-----------------------------------------------------------------------------
// FILE:	    WorkflowStubHelper.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Used to simplify the generation and implementation of typed
    /// workflow stubs.
    /// </summary>
    internal class WorkflowStubHelper
    {
        private CadenceClient       client;
        private WorkflowOptions     options;
        private string              domain;
        private WorkflowStub        untypedStub;
        private bool                hasStarted;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        public WorkflowStubHelper(CadenceClient client)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            this.client = client;
        }

        /// <summary>
        /// Indicates whether the workflow has already been started.
        /// </summary>
        /// <returns><c>true</c> if the workflow has been started.</returns>
        public bool HasStarted()
        {
            return hasStarted;
        }

        /// <summary>
        /// Sets the associated untyped <see cref="WorkflowStub"/>.
        /// </summary>
        /// <param name="untypedStub">The <see cref="WorkflowStub"/>.</param>
        /// <param name="hasStarted">Indicates whether the workflow has already been started.</param>
        public void SetUnTypedStub(WorkflowStub untypedStub, bool hasStarted)
        {
            Covenant.Requires<ArgumentNullException>(untypedStub != null);

            this.untypedStub = untypedStub;
            this.hasStarted  = hasStarted;
        }

        /// <summary>
        /// Returns the associated untyped <see cref="WorkflowStub"/>.
        /// </summary>
        /// <returns>The <see cref="WorkflowStub"/>.</returns>
        public WorkflowStub GetUntypedStub()
        {
            return untypedStub;
        }
    }
}
