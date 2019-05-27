//-----------------------------------------------------------------------------
// FILE:	    InternalWorkflowRestartException.cs
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

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Thrown by <see cref="Workflow.RestartAsync(byte[], byte[])"/> to be handled
    /// by <see cref="Workflow.InvokeAsync(CadenceClient, WorkflowInvokeRequest)"/>
    /// as one of the special case mechanisms for completing a workflow.
    /// </summary>
    internal class InternalWorkflowRestartException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="args">The arguments for the next workflow run.</param>
        /// <param name="e">Optional exception to be returned for the current workflow run.</param>
        public InternalWorkflowRestartException(byte[] args, Exception e = null)
            : base()
        {
            this.Args      = args;
            this.Exception = e;
        }

        /// <summary>
        /// Returns the arguments for the next workflow run.
        /// </summary>
        public byte[] Args { get; private set; }

        /// <summary>
        /// Returns the optional exception to be returned for the
        /// current workflow run.
        /// </summary>
        public Exception Exception { get; private set; }
    }
}
