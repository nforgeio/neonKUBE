//-----------------------------------------------------------------------------
// FILE:	    HostingCapabilities.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Runtime.Serialization;

namespace Neon.Kube.Hosting
{
    /// <summary>
    /// Flags that describe any optional capabilities of a <see cref="IHostingManager"/> implementation.
    /// </summary>
    [Flags]
    public enum HostingCapabilities
    {
        /// <summary>
        /// The cluster has no special capabilities.
        /// </summary>
        None = 0,

        /// <summary>
        /// The cluster can be stopped and restarted.
        /// </summary>
        Stoppable = 0x00000001,
        
        /// <summary>
        /// The cluster can be paused and resumed, saving and restoring memory such that
        /// the cluster restarts exactly where it left off.
        /// </summary>
        Pausable = 0x00000002,

        /// <summary>
        /// The cluster can be removed.
        /// </summary>
        Removable = 0x00000004
    }
}
