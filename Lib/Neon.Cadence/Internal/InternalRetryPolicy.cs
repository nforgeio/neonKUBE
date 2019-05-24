//-----------------------------------------------------------------------------
// FILE:	    InternalRetryPolicy.cs
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

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;
using Neon.Retry;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <para>
    /// <b>INTERNAL USE ONLY:</b> Used to serialize standard Neon <see cref="IRetryPolicy"/> 
    /// instances into a form compatible with the Cadence GOLANG client.  This class maps
    /// to the Cadence GOLANG client structure:
    /// </para>
    /// <para>
    /// https://godoc.org/go.uber.org/cadence/internal#RetryPolicy
    /// </para>
    /// </summary>
    internal class InternalRetryPolicy
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public InternalRetryPolicy()
        {
        }

        /// <summary>
        /// Constructs an instance from a <see cref="LinearRetryPolicy"/>.
        /// </summary>
        /// <param name="policy">The policy.</param>
        public InternalRetryPolicy(LinearRetryPolicy policy)
        {
            Covenant.Requires<ArgumentNullException>(policy != null);

            this.InitialInterval    = CadenceHelper.ToCadence(policy.RetryInterval);
            this.BackoffCoefficient = 1.0;

            if (policy.Timeout.HasValue)
            {
                this.ExpirationInterval = CadenceHelper.ToCadence(policy.Timeout.Value);
            }

            this.MaximumAttempts = policy.MaxAttempts;
        }

        /// <summary>
        /// Constructs an instance from a <see cref="ExponentialRetryPolicy"/>,
        /// </summary>
        /// <param name="policy">The policy.</param>
        public InternalRetryPolicy(ExponentialRetryPolicy policy)
        {
            Covenant.Requires<ArgumentNullException>(policy != null);

            this.InitialInterval    = CadenceHelper.ToCadence(policy.InitialRetryInterval);
            this.BackoffCoefficient = 2.0;
            this.MaximumInterval    = CadenceHelper.ToCadence(policy.MaxRetryInterval);

            if (policy.Timeout.HasValue)
            {
                this.ExpirationInterval = CadenceHelper.ToCadence(policy.Timeout.Value);
            }

            this.MaximumAttempts = policy.MaxAttempts;
        }

        /// <summary>
        /// Backoff interval for the first retry. If coefficient is 1.0 then it is used for all retries.
        /// Required, no default value.
        /// </summary>
        [JsonProperty(PropertyName = "InitialInterval", Required = Required.Always)]
        public long InitialInterval { get; set; }

        /// <summary>
        /// Coefficient used to calculate the next retry backoff interval.
        /// The next retry interval is previous interval multiplied by this coefficient.
        /// Must be 1 or larger. Default is 2.0.
        /// </summary>
        [JsonProperty(PropertyName = "BackoffCoefficient", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(2.0)]
        public double BackoffCoefficient { get; set; } = 2.0;

        /// <summary>
        /// Specifies the maximim retry interval.  Retries intervals will start at <see cref="InitialInterval"/>
        /// and then be multiplied by <see cref="BackoffCoefficient"/> for each retry attempt until the
        /// interval reaches or exceeds <see cref="MaximumInterval"/>, at which point point each
        /// retry will use <see cref="MaximumInterval"/> for all subsequent attempts.
        /// </summary>
        [JsonProperty(PropertyName = "MaximumInterval", DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public long MaximumInterval { get; set; }

        /// <summary>
        /// Maximum time to retry.  Either <see cref="ExpirationInterval"/> or <see cref="MaximumAttempts"/> is 
        /// required.  Retries will stop when this is exceeded even if maximum retries is not been reached.
        /// </summary>
        [JsonProperty(PropertyName = "ExpirationInterval", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public long ExpirationInterval { get; set; }

        /// <summary>
        /// Maximum number of attempts.  When exceeded the retries stop.  If not set or set to 0, it means 
        /// unlimited, and the policy will rely on <see cref="ExpirationInterval"/> to decide when to stop
        /// retrying.  Either <see cref="MaximumAttempts"/> or <see cref="MaximumInterval"/>"/> is required.
        /// </summary>
        [JsonProperty(PropertyName = "MaximumAttempts", Required = Required.Always)]
        public int MaximumAttempts { get; set; }

        /// <summary>
        /// <para>
        /// Specifies Cadence errors that should not be retried. This is optional. Cadence server 
        /// will stop retrying if error reason matches this list.
        /// </para>
        /// <list type="bullet">
        /// <item>Custom errors: <b>cadence.NewCustomError(reason)</b></item>
        /// <item>Panic errors: <b>cadenceInternal:Panic</b></item>
        /// <item>Generic errors: <b>cadenceInternal:Generic</b></item>
        /// <item>
        /// Timeout errors: <b>cadenceInternal:Timeout TIMEOUT_TYPE</b>, where
        /// <b>TIMEOUT_TYPE</b> can be be <b>START_TO_CLOSE</b> or <b>HEARTBEAT</b>.
        /// </item>
        /// </list>
        /// <note>
        /// Cancellation is not a failure, so it won't be retried.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "NonRetriableErrorReasons", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public List<string> NonRetriableErrorReasons { get; set; } = new List<string>();
    }
}
