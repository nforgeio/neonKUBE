//-----------------------------------------------------------------------------
// FILE:	    LeaderElector.cs
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
using System.Threading;
using System.Threading.Tasks;

using k8s;

using Neon.Common;
using Neon.Retry;

using StockLeaderElector        = k8s.LeaderElection.LeaderElector;
using StockLeaderElectionConfig = k8s.LeaderElection.LeaderElectionConfig;
using StockLeaseLock            = k8s.LeaderElection.ResourceLock.LeaseLock;

namespace Neon.Kube
{
    /// <summary>
    /// Implements a thin wrapper over <see cref="k8s.LeaderElection.LeaderElector"/> by optionally
    /// implementing metrics counters.
    /// </summary>
    public sealed class LeaderElector : IDisposable
    {
        private IKubernetes         k8s;
        private StockLeaderElector  leaderElector;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s">
        /// Specifies the Kubernetes client that will be used by <see cref="RunAsync(CancellationToken)"/> 
        /// to communicate with the cluster.
        /// </param>
        /// <param name="config">Specifies the elector configuration.</param>
        public LeaderElector(IKubernetes k8s, LeaderElectionConfig config)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(config != null, nameof(config));

            leaderElector = new StockLeaderElector(
                new StockLeaderElectionConfig(new StockLeaseLock(k8s, config.Namespace, config.LeaseName, config.Identity))
                {
                    LeaseDuration = config.LeaseDuration,
                    RenewDeadline = config.RenewDeadline,
                    RetryPeriod   = config.RetryPeriod
                });
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            leaderElector?.Dispose();
            leaderElector = null;
        }

        /// <summary>
        /// Raised when the instance has been elected as leader.
        /// </summary>
        public event Action OnStartedLeading;

        /// <summary>
        /// Raised when leadership has changed or when the current leader is observed
        /// for the first time after the elector started.  The string passed identifies
        /// the new leader.
        /// </summary>
        public event Action<string> OnNewLeader;

        /// <summary>
        /// Raised when the current instance has been demoted.
        /// </summary>
        public event Action OnStoppedLeading;

        /// <summary>
        /// Returns <c>true</c> if the current instance is the leader.
        /// </summary>
        public bool IsLeader => leaderElector.IsLeader();

        /// <summary>
        /// Returns the identity of the current leader.
        /// </summary>
        public string Geteader() => leaderElector.GetLeader();

        /// <summary>
        /// Starts the elector.
        /// </summary>
        /// <param name="cancellationToken">Optionally specifies the cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public Task RunAsync(CancellationToken cancellationToken = default) => leaderElector.RunAsync(cancellationToken);
    }
}
