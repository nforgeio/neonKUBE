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
        /// Returns the operation title.
        /// </summary>
        string OperationTitle { get; }

        /// <summary>
        /// Specifies whether the class should print setup status to the console.
        /// This defaults to <c>false</c>.
        /// </summary>
        bool ShowStatus { get; set; }

        /// <summary>
        /// Specifies whether that node status will be displayed.  This
        /// defaults to <c>true</c>.
        ///</summary>
        bool ShowNodeStatus { get; set; }

        /// <summary>
        /// Specifies the maximum number of setup steps to be displayed.
        /// This defaults to <b>5</b>.  You can set <b>0</b> to allow an 
        /// unlimited number of steps may be displayed.
        /// </summary>
        int MaxDisplayedSteps { get; set; }

        /// <summary>
        /// The maximum number of nodes that will execute setup steps in parallel.  This
        /// defaults to effectively unconstrained.
        /// </summary>
        int MaxParallel { get; set; }

        /// <summary>
        /// Returns the number of setup steps.
        /// </summary>
        int StepCount { get; }

        /// <summary>
        /// Returns the current step number or -1 for quiet steps or when setup hasn't started yet.
        /// </summary>
        int CurrentStepNumber { get; }

        /// <summary>
        /// Returns the time spent performing setup after setup has completed (or failed).
        /// </summary>
        TimeSpan Runtime { get; }

        /// <summary>
        /// Optionally displays the elapsed time for each step as well as the overall
        /// operation when setup completes (or fails).
        /// </summary>
        bool ShowRuntime { get; set; }

        /// <summary>
        /// <para>
        /// Raised periodically when the overall status changes during cluster setup.
        /// </para>
        /// <note>
        /// This event will be raised on a background thread.
        /// </note>
        /// </summary>
        event SetupStatusChangedDelegate StatusChangedEvent;

        /// <summary>
        /// <para>
        /// Raised when individual progress/error messages are received from setup steps.
        /// This is used in situations where only limited status needs to be
        /// displayed or logged.
        /// </para>
        /// <note>
        /// This event will be raised on the same thread that logged the progress, typically
        /// the thread executing the step.
        /// </note>
        /// </summary>
        event SetupProgressDelegate ProgressEvent;

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
        /// <param name="leaveNodesConnected">Optionally leave the node proxies connected after setup completed.</param>
        /// <returns>The final disposition of the setup run.</returns>
        Task<SetupDisposition> RunAsync(bool leaveNodesConnected = false);

        /// <summary>
        /// Adds an <see cref="IDisposable"/> instance to the controller so that they
        /// can be properly disposed when <see cref="RunAsync(bool)"/> exits.
        /// </summary>
        /// <param name="disposable"></param>
        void AddDisposable(IDisposable disposable);

        /// <summary>
        /// Returns setup related log information for each of the nodes.
        /// </summary>
        /// <returns>An the <see cref="NodeLog"/> values.</returns>
        IEnumerable<NodeLog> GetNodeLogs();

        /// <summary>
        /// Returns <c>true</c> if the controller has an nodes with setup steps.
        /// </summary>
        bool HasNodeSteps { get; }

        /// <summary>
        /// Returns the <see cref="SetupController{NodeMetadata}"/>'s node metadata type.
        /// </summary>
        Type NodeMetadataType { get; }

        /// <summary>
        /// Returns a <see cref="HashSet{T}"/> with the names of the cluster nodes participating
        /// in the internal node step passed.  This step is available as <see cref="SetupStepStatus.InternalStep"/>.
        /// </summary>
        /// <param name="internalStep">The internal node step.</param>
        /// <returns>The set of names affected by the setup sstep.</returns>
        HashSet<string> GetStepNodeNames(object internalStep);

        /// <summary>
        /// Returns any status for the overall setup operation.
        /// </summary>
        public string GlobalStatus { get; }

        /// <summary>
        /// Returns the status for all of the setup steps in order of execution.
        /// </summary>
        /// <returns>The step status items.</returns>
        IEnumerable<SetupStepStatus> GetStepStatus();

        /// <summary>
        /// Returns the status for any nodes being managed by the controller.
        /// </summary>
        /// <returns>The status information for any nodes.</returns>
        IEnumerable<SetupNodeStatus> GetNodeStatus();

        /// <summary>
        /// Returns the status for any VM host machines being managed by executing
        /// subcontroller steps.
        /// </summary>
        /// <returns>The status information for any host nodes.</returns>
        IEnumerable<SetupNodeStatus> GetHostStatus();

        /// <summary>
        /// Indicates that setup should be cancelled.  Setting this will request
        /// cancellation.  Note that once this has been set to <c>true</c>, subsequent
        /// <c>false</c> assignments will be ignored.
        /// </summary>
        bool CancelPending { get; set; }
    }
}
