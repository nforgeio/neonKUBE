//-----------------------------------------------------------------------------
// FILE:        SetupDisposition.cs
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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Common;

namespace Neon.Kube.Setup
{
    /// <summary>
    /// Enumerates the final disposition of a <see cref="SetupController{NodeMetadata}"/> run.
    /// </summary>
    public enum SetupDisposition
    {
        /// <summary>
        /// The setup run has not been executed.
        /// </summary>
        NotExecuted = 0,

        /// <summary>
        /// The setup run completed successfully.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The setup run was cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The setup run failed.
        /// </summary>
        Failed
    }
}
