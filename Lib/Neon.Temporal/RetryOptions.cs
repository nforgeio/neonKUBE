//-----------------------------------------------------------------------------
// FILE:	    RetryOptions.cs
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
using System.Diagnostics.Contracts;
using System.Linq;

using Neon.Common;
using Neon.Retry;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Describes a Temporal retry policy.
    /// </summary>
    public class RetryOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public RetryOptions()
        {
        }

        /// <summary>
        /// Constructs an instance from a <see cref="LinearRetryPolicy"/>.
        /// </summary>
        /// <param name="policy">The policy.</param>
        public RetryOptions(LinearRetryPolicy policy)
        {
            Covenant.Requires<ArgumentNullException>(policy != null, nameof(policy));

            this.InitialInterval    = TemporalHelper.Normalize(policy.RetryInterval);
            this.BackoffCoefficient = 1.0;

            if (policy.Timeout.HasValue)
            {
                this.ExpirationInterval = TemporalHelper.Normalize(policy.Timeout.Value);
            }

            this.MaximumAttempts = policy.MaxAttempts;
        }

        /// <summary>
        /// Constructs an instance from a <see cref="ExponentialRetryPolicy"/>,
        /// </summary>
        /// <param name="policy">The policy.</param>
        public RetryOptions(ExponentialRetryPolicy policy)
        {
            Covenant.Requires<ArgumentNullException>(policy != null, nameof(policy));

            this.InitialInterval    = TemporalHelper.Normalize(policy.InitialRetryInterval);
            this.BackoffCoefficient = 2.0;
            this.MaximumInterval    = TemporalHelper.Normalize(policy.MaxRetryInterval);

            if (policy.Timeout.HasValue)
            {
                this.ExpirationInterval = TemporalHelper.Normalize(policy.Timeout.Value);
            }

            this.MaximumAttempts = policy.MaxAttempts;
        }

        /// <summary>
        /// Specifies the backoff interval for the first retry.  If coefficient is 1.0 then
        /// it is used for all retries.  Required, no default value.
        /// </summary>
        public TimeSpan InitialInterval { get; set; }

        /// <summary>
        /// Specifies the coefficient used to calculate the next retry backoff interval.  
        /// The next retry interval is previous interval multiplied by this coefficient. 
        /// This must be 1 or larger. Default is 2.0.
        /// </summary>
        public double BackoffCoefficient { get; set; } = 2.0;

        /// <summary>
        /// Specifies the maximim retry interval.  Retries intervals will start at <see cref="InitialInterval"/>
        /// and then be multiplied by <see cref="BackoffCoefficient"/> for each retry attempt until the
        /// interval reaches or exceeds <see cref="MaximumInterval"/>, at which point point each
        /// retry will use <see cref="MaximumInterval"/> for all subsequent attempts.
        /// </summary>
        public TimeSpan MaximumInterval { get; set; }

        /// <summary>
        /// Maximum time to retry.  Either <see cref="ExpirationInterval"/> or <see cref="MaximumAttempts"/> is 
        /// required.  Retries will stop when this is exceeded even if maximum retries is not been reached.
        /// </summary>
        public TimeSpan ExpirationInterval { get; set; }

        /// <summary>
        /// Maximum number of attempts.  When exceeded the retries stop.  If not set or set to 0, it means 
        /// unlimited, and the policy will rely on <see cref="ExpirationInterval"/> to decide when to stop
        /// retrying.  Either <see cref="MaximumAttempts"/> or <see cref="MaximumInterval"/>"/> is required.
        /// </summary>
        public int MaximumAttempts { get; set; }

        // $todo(jefflill):
        //
        // We'd align better with the Java client if this was a list of [TemporalException] derived
        // exceptions rather than GOLANG error strings.  We'll revisit this when the port is further
        // along.

        /// <summary>
        /// <para>
        /// Specifies Temporal errors that <b>should not</b> trigger a retry. This is optional.  Temporal server 
        /// will stop retrying if error reason matches this list.  Use the <see cref="Temporal.NonRetriableErrors"/>
        /// class methods to initialize this list as required.
        /// </para>
        /// <note>
        /// Cancellation is not a failure, so that won't be retried.
        /// </note>
        /// </summary>
        public List<string> NonRetriableErrors { get; set; } = new List<string>();
    }
}
