//-----------------------------------------------------------------------------
// FILE:	    XenVmPowerState.cs
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
using System.Runtime.Serialization;

using Neon.Common;

namespace Neon.XenServer
{
    /// <summary>
    /// Enumerates the possible virtual machine states.
    /// </summary>
    public enum XenVmPowerState
    {
        /// <summary>
        /// Could not determine the virtual machine state.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// The virtual machine is turned off.
        /// </summary>
        [EnumMember(Value = "halted")]
        Halted,

        /// <summary>
        /// The virtual machine is either paused with its memory still loaded in RAM
        /// or suspended with its memory persisted to disk.
        /// </summary>
        [EnumMember(Value = "paused")]
        Paused,

        /// <summary>
        /// The virtual machine is running.
        /// </summary>
        [EnumMember(Value = "running")]
        Running
    }
}
