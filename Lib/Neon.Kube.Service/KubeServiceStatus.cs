//-----------------------------------------------------------------------------
// FILE:	    KubeServiceStatus.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Service;

namespace Neon.Kube.Service
{
    /// <summary>
    /// Enumerates the possible <see cref="KubeService"/> running states.
    /// </summary>
    public enum KubeServiceStatus
    {
        /// <summary>
        /// The service has not been started.
        /// </summary>
        NotStarted = 0,

        /// <summary>
        /// The service is in the process of starting but is not yet 
        /// fully initialized.
        /// </summary>
        Starting,

        /// <summary>
        /// The service is running and ready for traffic.
        /// </summary>
        Running,

        /// <summary>
        /// The service has terminated.
        /// </summary>
        Terminated
    }
}
