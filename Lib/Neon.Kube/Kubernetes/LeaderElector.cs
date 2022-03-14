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
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;
using Prometheus;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Tasks;

namespace Neon.Kube
{
    /// <summary>
    /// Handles leader election between one or more actors (containers, processes, etc)
    /// using a <see cref="V1Lease"/>.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This implementation is loosely based on this GoLANG API: 
    /// <a href="https://github.com/kubernetes/client-go/blob/master/tools/leaderelection/leaderelection.go">leaderelection.go</a>
    /// </note>
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
    /// produced by a local clock. The implemention does not depend on their
    /// accuracy and only uses their change to indicate that another client has
    /// renewed the leader lease. Thus the implementation is tolerant to arbitrary
    /// clock skew, but is not tolerant to arbitrary clock skew rate.
    /// </para>
    /// <para>
    /// However, the level of tolerance to skew rate can be configured by setting
    /// <see cref="LeaderElectorSettings.RenewDeadline"/> and <see cref="LeaderElectorSettings.LeaseDuration"/>
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
    /// this client that the tolerance to skew rate varies inversely to master
    /// availability.
    /// </para>
    /// <para>
    /// Larger clusters often have a more lenient SLA for API latency. This should be
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
    /// client instance and your <see cref="LeaderElectorSettings"/>.
    /// </item>
    /// <item>
    /// A a handler to the <see cref="StateChanged"/> event.  This event will be raised
    /// when the elector detects a leadership change, passing the new and previous
    /// <see cref="LeaderState"/>.
    /// </item>
    /// <item>
    /// Call <see cref="StartAsync()"/> to start the elector.
    /// </item>
    /// <item>
    /// Call <see cref="StopAsync()"/> when you're done with the elector.
    /// </item>
    /// </list>
    /// <para>
    /// You'll be monitoring the <see cref="StateChanged"/> for leadership changes.
    /// When the new state is <see cref="LeaderState.Leader"/> then the current 
    /// instance has become the leader.  <see cref="LeaderState.Follower"/> indicates
    /// that another instance is the leader and <see cref="LeaderState.Unknown"/>
    /// means that there is no leader or this instance hasn't identified the
    /// leader yet.
    /// </para>
    /// <note>
    /// The <see cref="StateChanged"/> event is raise from an inner lease monitoring
    /// loop and your handler should update your application state very quickly and
    /// return.  You must not perform lengthly operations in your handlers.
    /// </note>
    /// </remarks>
    public class LeaderElector
    {
        private static INeonLogger          log = LogManager.Default.GetLogger<LeaderElector>();

        private IKubernetes                 k8s;
        private LeaderElectorSettings       settings;
        private string                      logPrefix;
        private CancellationTokenSource     cts;                    // Used to signal stop
        private Task                        electionTask;           // The election loop task
        private V1Lease                     cachedLease;            // Local copy of the lease
        private DateTime                    remoteRenewTimeUtc;     // Renew time as persisted by the API server
        private DateTime                    localRenewTimeUtc;      // Renew time relative to the local clock
        private Counter                     promotedCount;          // Counts instance leader promotions
        private Counter                     demotedCount;           // Counts instance leader demotions
        private Counter                     renewalSuccessCount;    // Counts successful leader renewals
        private Counter                     renewalFailCount;       // Counts failed leader renewals
        private Counter                     requestFailCount;       // Counts unexpected failed API server requests

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s">Specifies the Kubernetes client to be used by the instance.</param>
        /// <param name="settings">Specifies the elector settings.</param>
        public LeaderElector(IKubernetes k8s, LeaderElectorSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(settings != null, nameof(settings));

            this.k8s                 = k8s;
            this.settings            = settings;
            this.logPrefix           = $"LeaderElector[lease={settings.Namespace}/{settings.LeaseName} identity={settings.Identity}]";
            this.cts                 = new CancellationTokenSource();
            this.State               = LeaderState.Unknown;
            this.remoteRenewTimeUtc  = DateTime.MinValue;
            this.localRenewTimeUtc   = DateTime.MinValue;
            this.promotedCount       = Metrics.CreateCounter("promoted", "Number of times this instance has been promoted to leader.", "lease");
            this.demotedCount        = Metrics.CreateCounter("demoted", "Number of times this instance has been demoted from leader.", "lease");
            this.renewalSuccessCount = Metrics.CreateCounter("renewal_success", "Number of lease has been renewed.", "lease");
            this.renewalFailCount    = Metrics.CreateCounter("renewal_fail", "Number of failed lease renewals.", "lease");
            this.requestFailCount    = Metrics.CreateCounter("request_fail", "Number of unexpected API server request failures.", "lease");
        }

        /// <summary>
        /// Returns the current elector state.
        /// </summary>
        public LeaderState State { get; private set; }

        /// <summary>
        /// Returns the identity of the current leader or <c>null</c>.
        /// </summary>
        public string LeaderIdentity { get; private set; }

        /// <summary>
        /// <para>
        /// Raised for leadership status changes.
        /// </para>
        /// <note>
        /// The <see cref="StateChanged"/> event is raised from an inner lease control
        /// loop as the leader status changes.  Your handler should update your application 
        /// state very quickly and return immediately.  You must not perform lengthly 
        /// operations in your handlers as this may interfere with election management.
        /// </note>
        /// </summary>
        public event LeaderEventHandler StateChanged;

        /// <summary>
        /// Starts the elector.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task StartAsync()
        {
            await SyncContext.Clear;
            Covenant.Requires<InvalidOperationException>(electionTask == null, $"You cannot reuse a [{nameof(LeaderElector)}].");

            log.LogInfo(() => $"{logPrefix}: starting [leaseDuration={settings.LeaseDuration}] [renewDeadline={settings.RenewDeadline}] [retryInterval={settings.RetryInterval}]");

            electionTask = Task.Run(ElectionLoopAsync);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Stops the elector if it's running.  This abdicates leadership, allowing
        /// any remaining instances to elect a new leader.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task StopAsync()
        {
            await SyncContext.Clear;

            if (cts.IsCancellationRequested)
            {
                return;
            }

            log.LogInfo(() => $"{logPrefix}: stopping");

            cts.Cancel();
            await electionTask;

            log.LogInfo(() => $"{logPrefix}: stopped");
        }

        /// <summary>
        /// Updates the elector current state and raises the <see cref="StateChanged"/> event
        /// for state transitions.
        /// </summary>
        /// <param name="newState">The new state.</param>
        private void RaiseStateChanged(LeaderState newState)
        {
            var newLeaderIdentity = cachedLease?.Spec.HolderIdentity ?? "-na-";

            if (newState == State && newLeaderIdentity == LeaderIdentity)
            {
                return; // Nothing has changed
            }

            var oldState = State;

            log.LogInfo(() => $"{logPrefix}: Transition to [{newState}/{newLeaderIdentity}] from [{State}/{LeaderIdentity}]]");

            State          = newState;
            LeaderIdentity = newLeaderIdentity;

            switch (State)
            {
                case LeaderState.Leader:

                    promotedCount.WithLabels(settings.LeaseRef).Inc();
                    break;

                case LeaderState.Follower:

                    demotedCount.WithLabels(settings.LeaseRef).Inc();
                    break;
            }

            StateChanged?.Invoke(new LeaderTransition(newState, oldState, newLeaderIdentity));
        }

        /// <summary>
        /// Attempts to acquire or renew the lease.
        /// </summary>
        /// <returns><c>true</c> on success.</returns>
        private async Task<bool> AcquireOrRenewAsync()
        {
            await SyncContext.Clear;

            // Read the current lease information.

            V1Lease remoteLease;

            try
            {
                remoteLease = await k8s.ReadNamespacedLeaseAsync(settings.LeaseName, settings.Namespace, cancellationToken: cts.Token);
            }
            catch (KubernetesException e)
            {
                switch ((HttpStatusCode)e.Status.Code)
                {
                    case HttpStatusCode.NotFound:

                        remoteLease = null;
                        cachedLease = null;
                        break;

                    default:

                        log.LogInfo($"{logPrefix}: Cannot retrieve lease.", e);
                        return false;
                }
            }
            catch (Exception e)
            {
                log.LogInfo($"{logPrefix}: Cannot retrieve lease.", e);
                requestFailCount.WithLabels(settings.LeaseRef).Inc();
                return false;
            }

            // When there's no lease on the API server, we'll try creating a new one.

            if (remoteLease == null)
            {
                try
                {
                    var utcNow = DateTime.UtcNow;

                    cachedLease = await k8s.CreateNamespacedLeaseAsync(
                        namespaceParameter: settings.Namespace,
                        body: new V1Lease(spec:
                                new V1LeaseSpec(
                                    acquireTime:          utcNow,
                                    holderIdentity:       settings.Identity,
                                    leaseDurationSeconds: (int)settings.LeaseDuration.TotalSeconds,
                                    leaseTransitions:     1,
                                    renewTime:            utcNow + settings.LeaseDuration)), cancellationToken: cts.Token);

                    localRenewTimeUtc  =
                    remoteRenewTimeUtc = utcNow;

                    return true;
                }
                catch (KubernetesException e)
                {
                    switch ((HttpStatusCode)e.Status.Code)
                    {
                        case HttpStatusCode.Conflict:

                            // Another elector instance must have created a lease since we checked
                            // above.  We'll try retrieving the lease again and exit.

                            try
                            {
                                cachedLease        = await k8s.ReadNamespacedLeaseAsync(settings.LeaseName, settings.Namespace, cancellationToken: cts.Token);
                                localRenewTimeUtc  = DateTime.UtcNow;
                                remoteRenewTimeUtc = (cachedLease.Spec.RenewTime ?? cachedLease.Spec.AcquireTime).Value;
                                return false;
                            }
                            catch (KubernetesException e2)
                            {
                                switch ((HttpStatusCode)e2.Status.Code)
                                {
                                    case HttpStatusCode.NotFound:

                                        log.LogWarn($"{logPrefix}: Lease was deleted out from under us.", e2);
                                        cachedLease = null;
                                        return false;

                                    default:

                                        log.LogWarn($"{logPrefix}: Cannot retrieve lease.", e2);
                                        return false;
                                }
                            }
                            catch (Exception e2)
                            {
                                log.LogWarn($"{logPrefix}: Cannot retrieve lease.", e2);
                                return false;
                            }

                        default:

                            log.LogInfo(e);
                            return false;
                    }
                }
                catch (Exception e)
                {
                    log.LogInfo($"{logPrefix}: Cannot create lease.", e);
                    requestFailCount.WithLabels(settings.LeaseRef).Inc();
                    return false;
                }
            }

            // Attempt to renew the lease when we own it.

            if (remoteLease.Spec.HolderIdentity == settings.Identity)
            {
                var clonedLease = NeonHelper.JsonClone(remoteLease);

                clonedLease.Spec.LeaseDurationSeconds = settings.LeaseDurationSeconds;
                clonedLease.Spec.RenewTime            = DateTime.UtcNow;

                try
                {
                    cachedLease = await k8s.ReplaceNamespacedLeaseAsync(clonedLease, settings.LeaseName, settings.Namespace, cancellationToken: cts.Token);

                    return true;
                }
                catch (KubernetesException e)
                {
                    switch ((HttpStatusCode)e.Status.Code)
                    {
                        case HttpStatusCode.Conflict:

                            // Looks like another instance acquired the lease out from under us,
                            // so fetch the updated lease.

                            try
                            {
                                log.LogInfo(() => $"{logPrefix}: Lease acquired out from under this instance by [{cachedLease.Spec.HolderIdentity}].");

                                cachedLease        = await k8s.ReadNamespacedLeaseAsync(settings.LeaseName, settings.Namespace, cancellationToken: cts.Token);
                                remoteRenewTimeUtc = cachedLease.Spec.RenewTime.Value;
                                localRenewTimeUtc  = DateTime.UtcNow;

                                renewalSuccessCount.WithLabels(settings.LeaseRef).Inc();
                            }
                            catch (KubernetesException e2)
                            {
                                renewalFailCount.WithLabels(settings.LeaseRef).Inc();

                                switch ((HttpStatusCode)e.Status.Code)
                                {
                                    case HttpStatusCode.NotFound:

                                        log.LogWarn(() => $"{logPrefix}: Lease deleted out from under us.");
                                        cachedLease = null;
                                        break;

                                    default:

                                        log.LogWarn($"{logPrefix}: Cannot acquire updated lease.", e2);
                                        break;
                                }
                            }
                            catch (Exception e2)
                            {
                                log.LogWarn($"{logPrefix}: Cannot acquire updated lease.", e2);
                            }

                            return false;

                        case HttpStatusCode.NotFound:

                            log.LogWarn(() => $"{logPrefix}: Lease deleted out from under us.");
                            renewalFailCount.WithLabels(settings.LeaseRef).Inc();
                            cachedLease = null;
                            return false;

                        default:

                            return false;
                    }
                }
                catch (Exception e)
                {
                    log.LogInfo($"{logPrefix}: Cannot renew lease.", e);
                    requestFailCount.WithLabels(settings.LeaseRef).Inc();
                    renewalFailCount.WithLabels(settings.LeaseRef).Inc();
                    cachedLease = null;
                    return false;
                }
            }

            // This instance does not own the lease.
            //
            // Compare the current lease's AcquireTime/RenewTime to the saved [renewTime]
            // to detect lease renewals and to compute the [localRenewTime] based on the
            // local clock.

            var acquireOrRenewTimeUtc = (remoteLease.Spec.RenewTime ?? remoteLease.Spec.AcquireTime).Value;

            if (acquireOrRenewTimeUtc != remoteRenewTimeUtc)
            {
                remoteRenewTimeUtc = acquireOrRenewTimeUtc;
                localRenewTimeUtc  = DateTime.UtcNow;
            }

            // If the local time is greater than or equal to [localRenewTime + leaseDuration],
            // then the other leader may have died so we'll try to acquire the lease.

            if (DateTime.UtcNow - localRenewTimeUtc >= settings.LeaseDuration)
            {
                // We're not the leader and the current leader hasn't renewed the lease
                // in the required time so we'll try to aquire the lease.

                var clonedLease = NeonHelper.JsonClone(cachedLease);

                clonedLease.Spec.HolderIdentity       = settings.Identity;
                clonedLease.Spec.AcquireTime          = 
                clonedLease.Spec.RenewTime            = DateTime.UtcNow;
                clonedLease.Spec.LeaseDurationSeconds = (int)settings.LeaseDuration.TotalSeconds;
                clonedLease.Spec.LeaseTransitions++;

                try
                {
                    cachedLease        = await k8s.ReplaceNamespacedLeaseAsync(clonedLease, settings.LeaseName, settings.Namespace, cancellationToken: cts.Token);
                    remoteRenewTimeUtc = 
                    localRenewTimeUtc  = clonedLease.Spec.RenewTime.Value;

                    renewalSuccessCount.WithLabels(settings.LeaseRef).Inc();
                    return true;
                }
                catch (KubernetesException e)
                {
                    switch ((HttpStatusCode)e.Status.Code)
                    {
                        case HttpStatusCode.Conflict:

                            log.LogInfo(() => $"{logPrefix}: Could not acquire lease due to conflict.");
                            return false;

                        case HttpStatusCode.NotFound:

                            log.LogWarn(() => $"{logPrefix}: Lease deleted out from under us.");
                            return false;

                        default:

                            log.LogWarn($"{logPrefix}: Cannot acquire lease.", e);
                            return false;
                    }
                }
                catch (Exception e)
                {
                    // We couldn't acquire the lease.

                    log.LogInfo($"{logPrefix}: Cannot acquire lease.", e);
                    requestFailCount.WithLabels(settings.LeaseRef).Inc();
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Loops to acquire and renew leases until <see cref="cts"/> signals to stop.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ElectionLoopAsync()
        {
            await SyncContext.Clear;

            try
            {
                StateChanged?.Invoke(new LeaderTransition(LeaderState.Unknown, LeaderState.Unknown, null));

                while (!cts.IsCancellationRequested)
                {
                    if (await AcquireOrRenewAsync())
                    {
                        if (State == LeaderState.Leader)
                        {
                            RaiseStateChanged(LeaderState.Leader);
                        }
                        else
                        {
                            RaiseStateChanged(LeaderState.Follower);
                        }
                    }
                    else
                    {
                        RaiseStateChanged(LeaderState.Follower);
                    }

                    await Task.Delay(settings.RetryInterval, cts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // This is expected and normal.
            }
            catch (Exception e)
            {
                log.LogError(e);
                throw;
            }

            cachedLease = null;
            RaiseStateChanged(LeaderState.Stopped);
        }
    }
}
