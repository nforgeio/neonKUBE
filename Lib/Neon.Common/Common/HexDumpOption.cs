//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Hex.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Common
{
    /// <summary>
    /// Enumerates the option flags for the <see cref="NeonHelper.HexDump(byte[], int, int, int, HexDumpOption)"/> 
    /// and <see cref="NeonHelper.HexDump(byte[], int, HexDumpOption)"/> > methods.
    /// </summary>
    [Flags]
    public enum HexDumpOption
    {
        /// <summary>
        /// Enable no special formatting options.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Enables all formatting options.
        /// </summary>
        ShowAll = 0x7F,

        /// <summary>
        /// Include ANSI characters after the HEX bytes on each line.
        /// </summary>
        ShowAnsi = 0x01,

        /// <summary>
        /// Include the byte offset of the first byte of each line.
        /// </summary>
        ShowOffsets = 0x02,
    }
}
