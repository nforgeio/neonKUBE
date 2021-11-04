//-----------------------------------------------------------------------------
// FILE:	    INodeSshProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Used to reference node proxy common properties.
    /// </summary>
    public interface INodeSshProxy
    {
        /// <summary>
        /// Returns the name of the proxy instance.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns the node role, one of the <see cref="NodeRole"/> identifying what the node does.
        /// This may also return <c>null</c>.
        /// </summary>
        string Role { get; set; }

        /// <summary>
        /// Indicates the current remote machine status.
        /// </summary>
        string Status { get; set; }

        /// <summary>
        /// Set to <c>true</c> when the current setup step has been completed not the node.
        /// </summary>
        bool IsReady { get; set; }

        /// <summary>
        /// Returns <c>true</c> when the proxy is faulted.
        /// </summary>
        bool IsFaulted { get; }

        /// <summary>
        /// Returns the current log for the node.
        /// </summary>
        /// <returns>A <see cref="NodeLog"/>.</returns>
        NodeLog GetLog();
    }
}
