//-----------------------------------------------------------------------------
// FILE:	    CadenceRetryPolicy.cs
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
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <para>
    /// Used to serialize standard Neon <see cref="IRetryPolicy"/> instances into a
    /// form compatible with the Cadence GOLANG client.  This class maps closely to
    /// the Cadence GOLANG client structure:
    /// </para>
    /// <para>
    /// https://godoc.org/go.uber.org/cadence/internal#RetryPolicy
    /// </para>
    /// </summary>
    internal class CadenceRetryPolicy
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public CadenceRetryPolicy()
        {
        }

        /// <summary>
        /// Constructs an instance from a <see cref="LinearRetryPolicy"/>.
        /// </summary>
        /// <param name="policy">The policy.</param>
        public CadenceRetryPolicy(LinearRetryPolicy policy)
        {
            Covenant.Requires<ArgumentNullException>(policy != null);

            this.InitialInterval    = new GoTimeSpan(policy.RetryInterval).ToString();
            this.BackoffCoefficient = 1.0;

            if (policy.Timeout.HasValue)
            {
                this.ExpirationInterval = new GoTimeSpan(policy.Timeout.Value).ToString();
            }

            this.MaximumAttempts = policy.MaxAttempts;
        }

        /// <summary>
        /// Constructs an instance froma <see cref="ExponentialRetryPolicy"/>,
        /// </summary>
        /// <param name="policy">The policy.</param>
        public CadenceRetryPolicy(ExponentialRetryPolicy policy)
        {
            Covenant.Requires<ArgumentNullException>(policy != null);

            this.InitialInterval    = new GoTimeSpan(policy.InitialRetryInterval).ToString();
            this.BackoffCoefficient = 2.0;
            this.MaximumInterval    = new GoTimeSpan(policy.MaxRetryInterval).ToString();

            if (policy.Timeout.HasValue)
            {
                this.ExpirationInterval = new GoTimeSpan(policy.Timeout.Value).ToString();
            }

            this.MaximumAttempts = policy.MaxAttempts;
        }

        /// <summary>
        /// Backoff interval for the first retry. If coefficient is 1.0 then it is used for all retries.
        /// Required, no default value.
        /// </summary>
        [JsonProperty(PropertyName = "InitialInterval", Required = Required.Always)]
        public string InitialInterval { get; set; }

        /// <summary>
        /// Coefficient used to calculate the next retry backoff interval.
        // The next retry interval is previous interval multiplied by this coefficient.
        // Must be 1 or larger. Default is 2.0.
        /// </summary>
        [JsonProperty(PropertyName = "BackoffCoefficient", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(2.0)]
        public double BackoffCoefficient { get; set; } = 2.0;

        /// <summary>
        /// Maximum time to retry. Either ExpirationInterval or MaximumAttempts is required.
        /// When exceeded the retries stop even if maximum retries is not reached yet.
        /// </summary>
        [JsonProperty(PropertyName = "MaximumInterval", DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public string MaximumInterval { get; set; } = null;

        /// <summary>
        /// Maximum time to retry. Either ExpirationInterval or MaximumAttempts is required.
        /// When exceeded the retries stop even if maximum retries is not reached yet.
        /// </summary>
        [JsonProperty(PropertyName = "ExpirationInterval", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public string ExpirationInterval { get; set; } = null;

        /// <summary>
        /// Maximum number of attempts. When exceeded the retries stop even if not expired yet.
        /// If not set or set to 0, it means unlimited, and rely on ExpirationInterval to stop.
        /// Either MaximumAttempts or ExpirationInterval is required.
        /// </summary>
        [JsonProperty(PropertyName = "MaximumAttempts", Required = Required.Always)]
        public int MaximumAttempts { get; set; }

        /// <summary>
        /// <para>
        /// Non-Retriable errors. This is optional. Cadence server will stop retry if error reason matches this list.
        /// </para>
        /// <list type="bullet">
        /// <item>Error reason for custom error is specified when your activity/workflow return cadence.NewCustomError(reason).</item>
        /// <item>Error reason for panic error is "cadenceInternal:Panic".</item>
        /// <item>Error reason for any other error is "cadenceInternal:Generic".</item>
        /// <item>Error reason for timeouts is: "cadenceInternal:Timeout TIMEOUT_TYPE". TIMEOUT_TYPE could be START_TO_CLOSE or HEARTBEAT.</item>
        /// </list>
        /// <note>
        /// Note, cancellation is not a failure, so it won't be retried.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "NonRetriableErrorReasons", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public List<string> NonRetriableErrorReasons { get; set; } = new List<string>();
    }
}
