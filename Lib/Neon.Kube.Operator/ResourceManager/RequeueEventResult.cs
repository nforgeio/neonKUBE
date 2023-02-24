//-----------------------------------------------------------------------------
// FILE:	    RequeueEventResult.cs
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
    /// Used to indicate that a reconcile event should be requeue.
    /// </summary>
    internal class RequeueEventResult : ResourceControllerResult
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="requeueDelay">Specifies the requeue delay.</param>
        public RequeueEventResult(TimeSpan requeueDelay)
            : base(requeueDelay)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="requeueDelay">Specifies the requeue delay.</param>
        /// <param name="eventType">Specifies the watch event type.</param>
        public RequeueEventResult(TimeSpan requeueDelay, WatchEventType eventType)
            : base(requeueDelay, eventType)
        {
        }
    }
}
