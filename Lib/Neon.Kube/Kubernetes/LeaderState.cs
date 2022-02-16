//-----------------------------------------------------------------------------
// FILE:	    LeaderState.cs
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
    /// Enumerates the leadership states for <see cref="LeaderElector"/>.
    /// </summary>
    public enum LeaderState
    {
        /// <summary>
        /// The current leader is unknown at this time.
        /// </summary>
        Unknown,

        /// <summary>
        /// Another entity is the leader and the entity associated with the
        /// <see cref="LeaderElector"/> is a follower.
        /// </summary>
        Follower,

        /// <summary>
        /// The entity associated with the <see cref="LeaderElector"/> is
        /// the leader.
        /// </summary>
        Leader,

        /// <summary>
        /// The associated <see cref="LeaderElector"/> has been stopped.
        /// </summary>
        Stopped
    }
}
