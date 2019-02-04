//-----------------------------------------------------------------------------
// FILE:	    RetryPolicyBase.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading.Tasks;

using Neon.Diagnostics;

namespace Neon.Retry
{
    /// <summary>
    /// Base class for used to help implement a <see cref="IRetryPolicy"/>.
    /// </summary>
    public abstract class RetryPolicyBase : IRetryPolicy
    {
        private INeonLogger log;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sourceModule">Optionally enables transient error logging by identifying the source module (defaults to <c>null</c>).</param>
        public RetryPolicyBase(string sourceModule = null)
        {
            if (!string.IsNullOrEmpty(sourceModule))
            {
                log = LogManager.Default.GetLogger(sourceModule);
            }
        }

        /// <inheritdoc/>
        public abstract IRetryPolicy Clone();

        /// <inheritdoc/>
        public abstract Task InvokeAsync(Func<Task> action);

        /// <inheritdoc/>
        public abstract Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> action);

        /// <summary>
        /// Logs a transient exception that is going to be retried if logging
        /// is enabled.
        /// </summary>
        /// <param name="e">The exception.</param>
        protected void LogTransient(Exception e)
        {
            log?.LogWarn("[transient-retry]", e);
        }
    }
}
