//-----------------------------------------------------------------------------
// FILE:	    RetryTransientArgs.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Retry
{
    /// <summary>
    /// Arguments passed to <see cref="IRetryPolicy.OnTransient"/> handlers so these can
    /// react to transient exceptions and optionally prevent further handlers from being
    /// invoked and also prevent the transient exception from being logged.
    /// </summary>
    public class RetryTransientArgs
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="e">Specifies the transient exception.</param>
        internal RetryTransientArgs(Exception e)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            this.Exception = e;
            this.Handled   = false;
        }

        /// <summary>
        /// Returns the transient exception detected by the retry policy.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// Handlers may set this to <c>true</c> to indicate that no subsequent 
        /// handlers should be called and also that the default transient logging
        /// should not occur.
        /// </summary>
        public bool Handled { get; set; }
    }
}
