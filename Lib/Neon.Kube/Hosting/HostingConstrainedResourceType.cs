//-----------------------------------------------------------------------------
// FILE:	    HostingConstrainedResourceType.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Runtime.Serialization;

using Neon.Common;

namespace Neon.Kube.Hosting
{
    /// <summary>
    /// Enumerates the types of <see cref="HostingResourceConstraint"/> instances,
    /// indicating the type of resource could not be accommodated by a hosting environment
    /// to deploy a cluster.
    /// </summary>
    public enum HostingConstrainedResourceType
    {
        /// <summary>
        /// The resource type cannot be determined.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown,

        /// <summary>
        /// Memory/RAM.
        /// </summary>
        [EnumMember(Value = "memory")]
        Memory,

        /// <summary>
        /// Disk space.
        /// </summary>
        [EnumMember(Value = "disk")]
        Disk,

        /// <summary>
        /// CPU cores.
        /// </summary>
        [EnumMember(Value = "cpu")]
        Cpu,

        /// <summary>
        /// Virtual machine host specific issue.
        /// </summary>
        [EnumMember(Value = "vm-host")]
        VmHost
    }
}
