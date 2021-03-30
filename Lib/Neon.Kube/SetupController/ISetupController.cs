//-----------------------------------------------------------------------------
// FILE:	    ISetupController.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.SSH;

namespace Neon.Kube
{
    /// <summary>
    /// Describes some common methods exposed by all <see cref="SetupController{NodeMetadata}"/> implementations.
    /// </summary>
    public interface ISetupController : IObjectDictionary
    {
        /// <summary>
        /// Optionally specifies the line written as the first line of log files.
        /// </summary>
        string LogBeginMarker { get; set; }

        /// <summary>
        /// Optionally specifies the line written as the last line of log files when the operation succeeded.
        /// </summary>
        string LogEndMarker { get; set; }

        /// <summary>
        /// Optionally specifies the line written as the last line of log files when the operation failed.
        /// </summary>
        string LogFailedMarker { get; set; }

        /// <summary>
        /// Logs a progress message.
        /// </summary>
        /// <param name="message">The message.</param>
        void LogProgress(string message);

        /// <summary>
        /// Logs a progress message with a verb.  This will be formatted
        /// like <b>VERB: MESSAGE</b>.
        /// </summary>
        /// <param name="verb">The message verb.</param>
        /// <param name="message">The message.</param>
        void LogProgress(string verb, string message);

        /// <summary>
        /// Logs a progress message for a specific node.  This sets the <b>status</b>
        /// text for the node.
        /// </summary>
        /// <param name="node">
        /// The node reference as a <see cref="LinuxSshProxy"/> so we can 
        /// avoid dealing with the node generic parameter here.
        /// </param>
        /// <param name="message">The message.</param>
        void LogProgress(LinuxSshProxy node, string message);

        /// <summary>
        /// Logs a progress for a specific node with a verb and message.  
        /// This will be formatted like <b>VERB: MESSAGE</b>.
        /// </summary>
        /// <param name="node">
        /// The node reference as a <see cref="LinuxSshProxy"/> so we can 
        /// avoid dealing with the node generic parameter here.
        /// </param>
        /// <param name="verb">The message verb.</param>
        /// <param name="message">The message.</param>
        void LogProgress(LinuxSshProxy node, string verb, string message);

        /// <summary>
        /// <para>
        /// Logs an error message.
        /// </para>
        /// <note>
        /// Setup will terminate after any step that reports an error
        /// via this method.
        /// </note>
        /// </summary>
        /// <param name="message">The message.</param>
        void LogError(string message);

        /// <summary>
        /// <para>
        /// Logs an error message for a specific node.
        /// </para>
        /// <note>
        /// Setup will terminate after any step that reports an error
        /// via this method.
        /// </note>
        /// </summary>
        /// <param name="node">
        /// The node reference as a <see cref="LinuxSshProxy"/> so we can 
        /// avoid dealing with the node generic parameter here.
        /// </param>
        /// <param name="message">The message.</param>
        void LogError(LinuxSshProxy node, string message);

        /// <summary>
        /// Indicates whether cluster setup is faulted due to a global problem or when
        /// any node is faulted.
        /// </summary>
        bool IsFaulted { get; }

        /// <summary>
        /// Returns the last error message logged by <see cref="LogError(string)"/>.
        /// </summary>
        string LastError { get; }

        /// <summary>
        /// Performs the setup operation steps in the in the order they were added to the controller.
        /// </summary>
        /// <param name="leaveNodesConnected">Pass <c>true</c> leave the node proxies connected.</param>
        /// <returns><c>true</c> if all steps completed successfully.</returns>
        bool Run(bool leaveNodesConnected = false);

        /// <summary>
        /// Adds an <see cref="IDisposable"/> instance to the controller so that they
        /// can be properly disposed when <see cref="Run(bool)"/> exits.
        /// </summary>
        /// <param name="disposable"></param>
        void AddDisposable(IDisposable disposable);

        /// <summary>
        /// Returns setup related log information for each of the nodes.
        /// </summary>
        /// <returns>An the <see cref="NodeLog"/> values.</returns>
        IEnumerable<NodeLog> GetNodeLogs();
    }
}
