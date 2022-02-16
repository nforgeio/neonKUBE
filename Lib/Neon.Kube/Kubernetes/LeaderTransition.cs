//-----------------------------------------------------------------------------
// FILE:	    LeaderTransition.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// Holds information about a leadership transition as reported by the
    /// <see cref="LeaderElector.StateChanged"/> event.
    /// </summary>
    public struct LeaderTransition
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="newState">The new elector state.</param>
        /// <param name="oldState">The previous elector state.</param>
        /// <param name="leaderIdentity">The identity of the current leader or <c>null</c>.</param>
        internal LeaderTransition(LeaderState newState, LeaderState oldState, string leaderIdentity)
        {
            this.NewState       = newState;
            this.OldState       = oldState;
            this.LeaderIdentity = leaderIdentity;
        }

        /// <summary>
        /// Returns the new elector state.
        /// </summary>
        public LeaderState NewState { get; private set; }

        /// <summary>
        /// Returns the previous elector state.
        /// </summary>
        public LeaderState OldState { get; private set; }

        /// <summary>
        /// Returns the current leader identity or <c>null</c>.
        /// </summary>
        public string LeaderIdentity { get; private set; }
    }
}
