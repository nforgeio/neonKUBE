//-----------------------------------------------------------------------------
// FILE:        StopMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.Hosting
{
    /// <summary>
    /// Used to control how cluster nodes are shutdown.
    /// </summary>
    public enum StopMode
    {
        /// <summary>
        /// Performs a graceful shutdown of the nodes such that all services
        /// have a chance to persist their state before the node stops.
        /// </summary>
        Graceful,

        /// <summary>
        /// <para>
        /// Pauses the nodes when supported by the hosting environment.  This
        /// quickly persists the node state to disk such that it can be restarted
        /// relatively quickly where it left off.
        /// </para>
        /// <note>
        /// This is not supported by some hosting environments.  Those environments
        /// will treat this as <see cref="Graceful"/>.
        /// </note>
        /// </summary>
        Pause,

        /// <summary>
        /// Immediately turns the the nodes off without shutting them down gracefully.
        /// This is equivalent to pulling the power plug on a physical machine and
        /// may result in data loss.
        /// </summary>
        TurnOff
    }
}
