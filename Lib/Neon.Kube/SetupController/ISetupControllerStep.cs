//-----------------------------------------------------------------------------
// FILE:	    ISetupControllerStep.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Net;
using Neon.SSH;

namespace Neon.Kube.Setup
{
    /// <summary>
    /// Interface implemented by internal <see cref="SetupController{NodeMetadata}.Step"/>
    /// implementations.
    /// </summary>
    public interface ISetupControllerStep
    {
        /// <summary>
        /// Returns <c>true</c> for global (non-node) steps or <c>false</c> for node related steps.
        /// </summary>
        bool IsGlobalStep { get; }

        /// <summary>
        /// Returns the elapsed time executing the step.
        /// </summary>
        TimeSpan RunTime { get; set; }
    }
}
