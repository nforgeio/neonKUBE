//-----------------------------------------------------------------------------
// FILE:        LeaderElector.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using Neon.Tasks;

using StockLeaderElector        = k8s.LeaderElection.LeaderElector;
using StockLeaderElectionConfig = k8s.LeaderElection.LeaderElectionConfig;
using StockLeaseLock            = k8s.LeaderElection.ResourceLock.LeaseLock;

namespace Neon.Kube
{
    /// <summary>
    /// Implements a thin wrapper over <see cref="k8s.LeaderElection.LeaderElector"/>
    /// integrating optional metric counters for tracking leadership changes.
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
    /// this client that the tolerance to skew rate varies inversely to control-plane node
    /// availability.
    /// </para>
    /// <para>
    /// Larger clusters often need a more lenient SLA for API latency. This should be
    /// taken into account when configuring the client. The rate of leader transitions
    /// should be monitored and RetryPeriod and LeaseDuration should be increased
    /// until the rate is stable and acceptably low. It's important to keep in mind
    /// when configuring this client that the tolerance to API latency varies inversely
    /// to control-plane availability.
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
    /// Call <see cref="RunAsync()"/> to start the elector.  This method will return
    /// when the elector is disposed.
    /// </item>
    /// </list>
    /// </remarks>
    public sealed class LeaderElector : IDisposable
    {
        private StockLeaderElector          leaderElector;
        private CancellationTokenSource     tcs;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client to be used to communicate with th\e cluster.</param>
        /// <param name="config">Specifies the elector configuration.</param>
        /// <param name="onStartedLeading">
        /// Optionally specifies the action to be called when the instance assumes 
        /// leadership.
        /// </param>
        /// <param name="onNewLeader">
        /// Optionally specifies the action to be called when leadership changes.  
        /// The identity of the new leader will be passed.
        /// </param>
        /// <param name="onStoppedLeading">
        /// Optionally specifies the action to be called when the instance is demoted.
        /// </param>
        public LeaderElector(
            IKubernetes             k8s,
            LeaderElectionConfig    config,
            Action                  onStartedLeading = null,
            Action<string>          onNewLeader      = null,
            Action                  onStoppedLeading = null)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(config != null, nameof(config));

            tcs = new CancellationTokenSource();

            leaderElector = new StockLeaderElector(
                new StockLeaderElectionConfig(new StockLeaseLock(k8s, config.Namespace, config.LeaseName, config.Identity))
                {
                    LeaseDuration = config.LeaseDuration,
                    RenewDeadline = config.RenewDeadline,
                    RetryPeriod   = config.RetryPeriod
                });

            var hasCounterLabels = config.CounterLabels != null && config.CounterLabels.Length > 0;

            leaderElector.OnStartedLeading +=
                () =>
                {
                    config.PromotionCounter?.Inc();

                    onStartedLeading?.Invoke();
                };

            leaderElector.OnStoppedLeading +=
                () =>
                {
                    config.NewLeaderCounter.Inc();

                    onStoppedLeading?.Invoke();
                };

            leaderElector.OnNewLeader += 
                identity =>
                {
                    config.NewLeaderCounter?.Inc();

                    onNewLeader?.Invoke(identity);
                };
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (leaderElector == null)
            {
                return;
            }

            tcs.Cancel();

            leaderElector.Dispose();
            leaderElector = null;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Ensures that the instance is not dispoed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the instance is disposed.</exception>
        private void EnsureNotDisposed()
        {
            if (leaderElector == null)
            {
                throw new ObjectDisposedException(nameof(LeaderElector));
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current instance is currently the leader.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the instance is disposed.</exception>
        public bool IsLeader
        {
            get
            {
                EnsureNotDisposed();

                return leaderElector.IsLeader();
            }
        }

        /// <summary>
        /// Returns the identity of the current leader or <c>null</c> when there's no leader.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the instance is disposed.</exception>
        public string Leader
        {
            get
            {
                EnsureNotDisposed();

                return leaderElector.GetLeader();
            }
        }

        /// <summary>
        /// Starts the elector.  Note that this will return when the elector is disposed.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the instance is disposed.</exception>
        public async Task RunAsync()
        {
            await SyncContext.Clear;
            EnsureNotDisposed();

            while (true)
            {
                try
                {
                    await leaderElector.RunAsync(tcs.Token);
                }
                catch (OperationCanceledException)
                {
                    // This signals that the leader elector has been disposed.

                    return;
                }
                catch
                {
                    // We're going to ignore all other exceptions and try to restart the elector
                    // after waiting a few seconds to avoid getting into a tight failure loop.

                    await Task.Delay(TimeSpan.FromSeconds(15));
                }
            }
        }
    }
}
