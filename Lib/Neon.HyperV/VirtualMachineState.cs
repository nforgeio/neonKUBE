//-----------------------------------------------------------------------------
// FILE:	    VirtualMachineState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.HyperV
{
    /// <summary>
    /// Enumerates the known Hyper-V virtual machine states.
    /// </summary>
    public enum VirtualMachineState
    {
        /// <summary>
        /// The current state cannot be determined or is not one of
        /// the known states below.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The machine is turned off.
        /// </summary>
        Off,

        /// <summary>
        /// The machine is starting.
        /// </summary>
        Starting,

        /// <summary>
        /// The machine is running.
        /// </summary>
        Running,

        /// <summary>
        /// The machine is paused.
        /// </summary>
        Paused,

        /// <summary>
        /// The machine saved.
        /// </summary>
        Saved
    }
}
