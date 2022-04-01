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
using k8s.Models;

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
    /// <remarks>
    /// <para>
    /// This class implements leader election uses a <see cref="V1Lease"/> object
    /// to manage leadership election by trying to ensure that only one leader is
    /// active at any time.
    /// </para>
    /// <note>
    /// Although this class tries fairly hard to ensure that only a single leader
    /// exists at any time, it is possible for two or more instances to believe 
    /// they are the leader due to network partitions or latency issues.
    /// </note>
    /// <para>
    /// A client only acts on timestamps captured locally to infer the state of the
    /// leader election. The client does not consider timestamps in the leader
    /// election record to be accurate because these timestamps may not have been
    /// produced by the local clock. The implemention does not depend on their
    /// accuracy and only uses their change to indicate that another client has
    /// renewed the leader lease. Thus the implementation is tolerant to arbitrary
    /// clock skew, but is not tolerant to arbitrary clock skew rate.
    /// </para>
    /// <para>
    /// However, the level of tolerance to skew rate can be configured by setting
    /// <see cref="LeaderElectionConfig.RenewDeadline"/> and <see cref="LeaderElectionConfig.LeaseDuration"/>
    /// appropriately. The tolerance expressed as a maximum tolerated ratio of time 
    /// passed on the fastest node to time passed on the slowest node can be approximately
    /// achieved with a configuration that sets the same ratio of LeaseDuration to RenewDeadline.
    /// For example if a user wanted to tolerate some nodes progressing forward in time
    /// twice as fast as other nodes, the user could set LeaseDuration to 60 seconds and 
    /// RenewDeadline to 30 seconds.
    /// </para>
    /// <para>
    /// While not required, some method of clock synchronization between nodes in the
    /// cluster is highly recommended. It's important to keep in mind when configuring
    /// this client that the tolerance to skew rate varies inversely to master node
    /// availability.
    /// </para>
    /// <para>
    /// Larger clusters often need a more lenient SLA for API latency. This should be
    /// taken into account when configuring the client. The rate of leader transitions
    /// should be monitored and RetryPeriod and LeaseDuration should be increased
    /// until the rate is stable and acceptably low. It's important to keep in mind
    /// when configuring this client that the tolerance to API latency varies inversely
    /// to master availability.
    /// </para>
    /// <para>
    /// This class is very easy to use:
    /// </para>
    /// <list type="number">
    /// <item>
    /// Use the <see cref="LeaderElector"/> constructor passing a <see cref="IKubernetes"/>
    /// client instance and your <see cref="LeaderElectionConfig"/>.
    /// </item>
    /// <item>
    /// Add handlers for the <see cref="OnNewLeader"/>, <see cref="OnStartedLeading"/>, and
    /// <see cref="OnStoppedLeading"/> events.  These events are raised as leaders are elected
    /// and demoted.
    /// </item>
    /// <item>
    /// Call <see cref="RunAsync(CancellationToken)"/> to start the elector.  You can signal
    /// is to stop by passing a <see cref="CancellationToken"/> and cancelling it.
    /// </item>
    /// </list>
    /// </remarks>
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

            this.k8s = k8s;

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
