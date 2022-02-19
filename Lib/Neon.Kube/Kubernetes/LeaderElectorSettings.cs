﻿//-----------------------------------------------------------------------------
// FILE:	    LeaderElectorSettings.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Settings used to configure a <see cref="LeaderElector"/>.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This implementation is based loosely on this OGLANG API: <a href="https://github.com/kubernetes/client-go/blob/master/tools/leaderelection/leaderelection.go">leaderelection.go</a>
    /// </note>
    /// </remarks>
    public class LeaderElectorSettings
    {
        private const double jitterFactor = 1.2;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="namespace">Identifies the namespace where the lease will be hosted.</param>
        /// <param name="leaseName">Specifies the lease name.</param>
        /// <param name="identity">
        /// <para>
        /// Specifies the unique identity of the entity using to elector to run for the leadership
        /// role.  This will typically be passed as the name of the host pod but this may be customized 
        /// for other scenarios.
        /// </para>
        /// <note>
        /// It's very important that the identifiers used by different candidate entities be 
        /// unique.  As mentioned above, the host pod name is a great option for most situations
        /// but this could also be a UUID or some other identity scheme which guarentees uniqueness.
        /// </note>
        /// </param>
        /// <param name="leaseDuration">
        /// Optionally specifies the interval a follower must wait before attempting to become
        /// the leader.  This defaults to <b>15 seconds</b>.
        /// </param>
        /// <param name="renewDeadline">
        /// Optionally specifies the interval the leader will attempt to renew the lease before
        /// abandonding leadership.  This defaults to <b>10 seconds</b>.
        /// </param>
        /// <param name="retryInterval">
        /// Optionally specifies the interval that <see cref="LeaderElector"/> instances should 
        /// wait before retrying any actions.  This defaults to <b>2 seconds</b>.
        /// </param>
        public LeaderElectorSettings(
            string      @namespace, 
            string      leaseName, 
            string      identity,
            TimeSpan    leaseDuration = default,
            TimeSpan    renewDeadline = default,
            TimeSpan    retryInterval = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(@namespace), nameof(@namespace));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(leaseName), nameof(leaseName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(identity), nameof(identity));

            if (leaseDuration <= TimeSpan.Zero)
            {
                leaseDuration = TimeSpan.FromSeconds(15);
            }

            if (renewDeadline <= TimeSpan.Zero)
            {
                renewDeadline = TimeSpan.FromSeconds(10);
            }

            if (retryInterval <= TimeSpan.Zero)
            {
                retryInterval = TimeSpan.FromSeconds(2);
            }

            if (leaseDuration <= renewDeadline)
            {
                throw new ArgumentException($"[{nameof(leaseDuration)}={leaseDuration}] is not greater than [{nameof(renewDeadline)}={renewDeadline}].");
            }

            var renewDeadlineFloor = TimeSpan.FromTicks((long)(retryInterval.Ticks * jitterFactor));

            if (renewDeadline <= renewDeadlineFloor)
            {
                throw new ArgumentException($"[{nameof(renewDeadline)}={renewDeadline}] is not greater than [JitterFactor*{nameof(retryInterval)}={renewDeadlineFloor}]");
            }

            this.Namespace     = @namespace;
            this.LeaseName     = leaseName;
            this.LeaseRef      = $"{@namespace}/{leaseName}";
            this.Identity      = identity;
            this.LeaseDuration = leaseDuration;
            this.RenewDeadline = renewDeadline;
            this.RetryInterval = retryInterval;
        }

        /// <summary>
        /// Returns the Kubernetes namespace where the lease will reside.
        /// </summary>
        public string Namespace { get; private set; }

        /// <summary>
        /// Returns the lease name.
        /// </summary>
        public string LeaseName { get; private set; }

        /// <summary>
        /// Returns the lease reference formatted as: NAMESPACE/LEASE-aNAME.
        /// </summary>
        internal string LeaseRef { get; private set; }

        /// <summary>
        /// Returns the unique identity of the entity using the elector to running for the leadership
        /// role.  This is typically the hosting pod name.
        /// </summary>
        public string Identity { get; private set; }

        /// <summary>
        /// Returns the interval a follower must wait before attempting to become the leader.
        /// </summary>
        public TimeSpan LeaseDuration { get; private set; }

        /// <summary>
        /// Returns <see cref="LeaseDuration"/> rounded up to the nearest second
        /// and limited to the range supported by a 32-bit integer.
        /// </summary>
        internal int LeaseDurationSeconds => (int)Math.Min((long)Math.Ceiling(LeaseDuration.TotalSeconds), int.MaxValue);

        /// <summary>
        /// Returns the interval durning the leader will attempt to renew the lease before 
        /// abandonding leadership upon failures.
        /// </summary>
        public TimeSpan RenewDeadline { get; private set; }

        /// <summary>
        /// The interval that <see cref="LeaderElector"/> instances should wait before
        /// retrying any actions.
        /// </summary>
        public TimeSpan RetryInterval { get; private set; }
    }
}