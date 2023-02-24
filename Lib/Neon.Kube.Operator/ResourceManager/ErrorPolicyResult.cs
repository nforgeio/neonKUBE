//-----------------------------------------------------------------------------
// FILE:	    ErrorPolicyResult.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using k8s;

namespace Neon.Kube.Operator.ResourceManager
{
    /// <summary>
    /// Describes a error policy result.
    /// </summary>
    public class ErrorPolicyResult
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="delay">Optionally override the default requruing delay.</param>
        /// <param name="eventType">Optionally specfies the event type (defaults to <see cref="WatchEventType.Modified"/>.</param>
        /// <param name="requeue">Optionally disable requeuing (defaults to <c>true</c>).</param>
        internal ErrorPolicyResult(
            TimeSpan?       delay     = null,
            WatchEventType  eventType = WatchEventType.Modified,
            bool            requeue   = true)
        {
            RequeueDelay = delay;
            EventType    = eventType;
            Requeue      = requeue;
        }

        /// <summary>
        /// Whether the item should be requeued.
        /// </summary>
        public bool Requeue { get; } = false;

        /// <summary>
        /// Time that should be waited for a requeue.
        /// </summary>
        public TimeSpan? RequeueDelay { get; }

        /// <summary>
        /// Type of the event to be queued.
        /// </summary>
        public WatchEventType? EventType { get; }
    }
}
