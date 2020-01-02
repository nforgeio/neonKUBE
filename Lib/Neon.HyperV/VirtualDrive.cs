//-----------------------------------------------------------------------------
// FILE:	    VirtualDrive.cs
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
    /// Specifies virtual drive creation parameters.
    /// </summary>
    public class VirtualDrive
    {
        /// <summary>
        /// Specifies the path where the drive will be located.  The drive format
        /// is indicated by the file type, either <b>.vhd</b> or <b>.vhdx</b>.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The drive size in bytes.
        /// </summary>
        public decimal Size { get; set; }

        /// <summary>
        /// Indicates whether a dynamic drive will be created as opposed to a
        /// pre-allocated fixed drive.  This defaults to <b>true</b>.
        /// </summary>
        public bool IsDynamic { get; set; } = true;
    }
}
