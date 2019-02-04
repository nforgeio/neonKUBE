//-----------------------------------------------------------------------------
// FILE:	    DockerShimability.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Enumerates the possible shimming options for a command.
    /// </summary>
    public enum DockerShimability
    {
        /// <summary>
        /// Indicates that the command cannot be shimmed.
        /// </summary>
        None,

        /// <summary>
        /// Indicates that the command may be shimmed but that shimming is not required.
        /// </summary>
        Optional,

        /// <summary>
        /// Indicates the command must be shimmed.
        /// </summary>
        Required
    }
}
