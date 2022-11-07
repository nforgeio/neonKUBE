//-----------------------------------------------------------------------------
// FILE:	    INodeSshProxy.cs
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
    public interface INodeSshProxy : SSH.ILinuxSshProxy
    {
        /// <summary>
        /// Returns the node role, one of the <see cref="NodeRole"/> identifying what the node does.
        /// This may also return <c>null</c>.
        /// </summary>
        string Role { get; set; }

        /// <summary>
        /// Returns the current log for the node.
        /// </summary>
        /// <returns>A <see cref="NodeLog"/>.</returns>
        NodeLog GetLog();

        /// <summary>
        /// Indicates whether an idempotent action has been completed.
        /// </summary>
        /// <param name="actionId">The action ID.</param>
        /// <returns><c>true</c> when the action has already been completed.</returns>
        bool GetIdempotentState(string actionId);

        /// <summary>
        /// Explicitly indicates that an idempotent action has been completed
        /// on the node.
        /// </summary>
        /// <param name="actionId">The action ID.</param>
        void SetIdempotentState(string actionId);

        /// <summary>
        /// Invokes a named action on the node if it has never been been performed
        /// on the node before.
        /// </summary>
        /// <param name="actionId">The node-unique action ID.</param>
        /// <param name="action">The action to be performed.</param>
        /// <returns><c>true</c> if the action was invoked.</returns>
        /// <remarks>
        /// <para>
        /// <paramref name="actionId"/> must uniquely identify the action on the node.
        /// This may include letters, digits, dashes and periods as well as one or
        /// more forward slashes that can be used to organize idempotent status files
        /// into folders.
        /// </para>
        /// <para>
        /// This method tracks successful action completion by creating a file
        /// on the node at <see cref="KubeNodeFolder.State"/><b>/ACTION-ID</b>.
        /// To ensure idempotency, this method first checks for the existence of
        /// this file and returns immediately without invoking the action if it is 
        /// present.
        /// </para>
        /// </remarks>
        bool InvokeIdempotent(string actionId, Action action);

        /// <summary>
        /// Invokes a named action asynchronously on the node if it has never been been performed
        /// on the node before.
        /// </summary>
        /// <param name="actionId">The node-unique action ID.</param>
        /// <param name="action">The asynchronous action to be performed.</param>
        /// <returns><c>true</c> if the action was invoked.</returns>
        /// <remarks>
        /// <para>
        /// <paramref name="actionId"/> must uniquely identify the action on the node.
        /// This may include letters, digits, dashes and periods as well as one or
        /// more forward slashes that can be used to organize idempotent status files
        /// into folders.
        /// </para>
        /// <para>
        /// This method tracks successful action completion by creating a file
        /// on the node at <see cref="KubeNodeFolder.State"/><b>/ACTION-ID</b>.
        /// To ensure idempotency, this method first checks for the existence of
        /// this file and returns immediately without invoking the action if it is 
        /// present.
        /// </para>
        /// </remarks>
        Task<bool> InvokeIdempotentAsync(string actionId, Func<Task> action);
    }
}
