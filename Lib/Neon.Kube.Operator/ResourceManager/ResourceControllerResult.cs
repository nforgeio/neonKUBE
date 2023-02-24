//-----------------------------------------------------------------------------
// FILE:	    ResourceControllerResult.cs
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
    /// Describes a reconcile result.
    /// </summary>
    public abstract class ResourceControllerResult
    {
        internal ResourceControllerResult() { }
        internal ResourceControllerResult(TimeSpan delay)
        {
            RequeueDelay = delay;
        }

        internal ResourceControllerResult(TimeSpan delay, WatchEventType eventType)
        {
            RequeueDelay = delay;
            EventType    = eventType;
        }

        /// <summary>
        /// Time that should be waited for a requeue.
        /// </summary>
        public TimeSpan RequeueDelay { get; }

        /// <summary>
        /// Type of the event to be queued.
        /// </summary>
        public WatchEventType? EventType { get; }

        /// <summary>
        /// Create a <see cref="ResourceControllerResult"/> that requeues a resource
        /// with a given delay. When the event fires (after the delay) the resource
        /// cache is consulted and the new <see cref="WatchEventType"/> is calculated.
        /// Based on this new calculation, the new event triggers the according function.
        /// </summary>
        /// <returns>The <see cref="ResourceControllerResult"/> with the configured delay.</returns>
        public static ResourceControllerResult Ok() => null;

        /// <summary>
        /// Create a <see cref="ResourceControllerResult"/> that requeues a resource
        /// with a given delay. When the event fires (after the delay) the resource
        /// cache is consulted and the new <see cref="WatchEventType"/> is calculated.
        /// Based on this new calculation, the new event triggers the according function.
        /// </summary>
        /// <param name="delay">
        /// The delay. Please note, that a delay of <see cref="TimeSpan.Zero"/>
        /// will result in an immediate trigger of the function. This can lead to infinite circles.
        /// </param>
        /// <returns>The <see cref="ResourceControllerResult"/> with the configured delay.</returns>
        public static ResourceControllerResult RequeueEvent(TimeSpan delay) => new RequeueEventResult(delay);

        /// <summary>
        /// Create a <see cref="ResourceControllerResult"/> that requeues a resource
        /// with a given delay. When the event fires (after the delay) the resource
        /// cache is ignored in favor the specified <see cref="WatchEventType"/>.
        /// Based on the specified type, the new event triggers the according function.
        /// </summary>
        /// <param name="delay">
        /// The delay. Please note, that a delay of <see cref="TimeSpan.Zero"/>
        /// will result in an immediate trigger of the function. This can lead to infinite circles.
        /// </param>
        /// <param name="eventType">
        /// The event type to queue.
        /// </param>
        /// <returns>The <see cref="ResourceControllerResult"/> with the configured delay and event type.</returns>
        public static ResourceControllerResult RequeueEvent(TimeSpan delay, WatchEventType eventType) => new RequeueEventResult(delay, eventType);
    }
}
