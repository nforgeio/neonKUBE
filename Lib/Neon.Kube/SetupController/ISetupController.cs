//-----------------------------------------------------------------------------
// FILE:	    ISetupController.cs
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
        /// Specifies the operation title.
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
        /// Optionally displays the elapsed time for each step as well as the overall
        /// operation when setup completes (or fails).
        /// </summary>
        bool ShowRuntime { get; set; }

        /// <summary>
        /// <para>
        /// Raised periodically when the overall status changes during cluster setup.
        /// </para>
        /// <note>
        /// This event will be raised on a background thread and that you <b>MUST NOT</b>
        /// modify any event parameters.
        /// </note>
        /// </summary>
        event SetupStatusChangedDelegate StatusChangedEvent;

        /// <summary>
        /// Raised when the next setup step is started.
        /// </summary>
        event EventHandler<SetupStepDetails> StepStarted;

        /// <summary>
        /// Returns the console updater used internally to write the setup status to the
        /// <see cref="Console"/> without flickering.  This will be <c>null</c> for non-console
        /// applications.
        /// </summary>
        SetupConsoleWriter ConsoleWriter { get; }

        /// <summary>
        /// <para>
        /// Optional event which is raised when the setup operation completes.  The <b>sender</b> argument
        /// will be passed as the <see cref="ISetupController"/> instance and the <see cref="Exception"/>
        /// argument will be <c>null</c> when the setup operation completed successfully or an exception
        /// detailing the failure.
        /// </para>
        /// <para>
        /// This presents a good opportunity for setup controller users to capture additional information 
        /// about failed operations, etc. in common code.
        /// </para>
        /// <note>
        /// Setup controller implementions are <b>not required</b> to set this.
        /// </note>
        /// </summary>
        event EventHandler<Exception> Finished;

        /// <summary>
        /// <para>
        /// Raised when individual progress/error messages are logged during
        /// base image setup where where only limited status needs to be
        /// displayed or logged.
        /// </para>
        /// <note>
        /// This event is not raised during normal cluster prepare or setup
        /// because the node image will have already gone through the base
        /// preparation.  This will be raised though when setting up using
        /// <b>debug mode</b>.
        /// </note>
        /// <note>
        /// This event will be raised on the same thread that logged the progress,
        /// typically the thread executing the step and that you <b>MUST NOT</b>
        /// modify any event parameters.
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
        /// <param name="node">Identifies the node</param>
        /// <param name="message">The message.</param>
        void LogProgress(ILinuxSshProxy node, string message);

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
        void LogProgressError(string message);

        /// <summary>
        /// Returns the last error message logged by <see cref="LogProgressError(string)"/>.
        /// </summary>
        string LastProgressError { get; }

        /// <summary>
        /// Logs a progress for a specific node with a verb and message.  
        /// This will be formatted like <b>VERB MESSAGE</b>.
        /// </summary>
        /// <param name="node">Identifies the node</param>
        /// <param name="verb">The message verb.</param>
        /// <param name="message">The message.</param>
        void LogProgress(SSH.ILinuxSshProxy node, string verb, string message);

        /// <summary>
        /// <para>
        /// Logs an error message for a specific node.
        /// </para>
        /// <note>
        /// Setup will terminate after any step that reports an error
        /// via this method.
        /// </note>
        /// </summary>
        /// <param name="node">Identifies the node</param>
        /// <param name="message">The message.</param>
        void LogProgressError(SSH.ILinuxSshProxy node, string message);

        /// <summary>
        /// <para>
        /// Writes a line to the global cluster log file.  This is used to log information
        /// that pertains to a global operation rather than a specific node.
        /// </para>
        /// <note>
        /// This does not raise the <see cref="ProgressEvent"/>.
        /// </note>
        /// </summary>
        /// <param name="message">Optionally specifies the message to be logged.</param>
        void LogGlobal(string message = null);

        /// <summary>
        /// <para>
        /// Writes an error line to the global cluster log file.  This is used to log errors
        /// that pertain to a global operation rather than a specific node.
        /// </para>
        /// <note>
        /// This does not raise the <see cref="ProgressEvent"/>.
        /// </note>
        /// </summary>
        /// <param name="message">Optionally specifies the message to be logged.</param>
        void LogGlobalError(string message = null);

        /// <summary>
        /// <para>
        /// Writes information about an exception to the global cluster log file.
        /// </para>
        /// <note>
        /// This does not raise the <see cref="ProgressEvent"/>.
        /// </note>
        /// </summary>
        /// <param name="e">The exception.</param>
        void LogGlobalException(Exception e);

        /// <summary>
        /// Indicates whether cluster setup is faulted due to a global problem or when
        /// any node is faulted.
        /// </summary>
        bool IsFaulted { get; }

        /// <summary>
        /// Performs the setup operation steps in the in the order they were added to the controller.
        /// </summary>
        /// <param name="maxStackSize">
        /// <para>
        /// Optionally specifies the maximum stack size, in bytes, to be used by the threads
        /// created by this method, or 0 to use the default maximum stack size specified in 
        /// the header for the executable.  Important for partially trusted code, <paramref name="maxStackSize"/> 
        /// is ignored if it is greater than the default stack size.  No exception is thrown 
        /// in this case.
        /// </para>
        /// <para>
        /// This <b>defaults to 250 KiB</b> to reduce the memory footprint when deploying large clusters.
        /// </para>
        /// </param>
        /// <returns>The final disposition of the setup run.</returns>
        Task<SetupDisposition> RunAsync(int maxStackSize = 250 * (int)ByteUnits.KibiBytes);

        /// <summary>
        /// Adds an <see cref="IDisposable"/> instance to the controller so that they
        /// can be properly disposed when <see cref="RunAsync(int)"/> exits.
        /// </summary>
        /// <param name="disposable"></param>
        void AddDisposable(IDisposable disposable);

        /// <summary>
        /// Returns setup related log information for each of the nodes.
        /// </summary>
        /// <returns>An the <see cref="NodeLog"/> values.</returns>
        IEnumerable<NodeLog> GetNodeLogs();

        /// <summary>
        /// Returns setup related log information for any host proxies.
        /// </summary>
        /// <returns>An the <see cref="NodeLog"/> values.</returns>
        IEnumerable<NodeLog> GetHostLogs();

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
        /// Sets the operation status text.
        /// </summary>
        /// <param name="status">The optional operation status text.</param>
        void SetGlobalStepStatus(string status = null);

        /// <summary>
        /// Attempts to cancel the setup operation.  This will cause <see cref="IsCancelPending"/> 
        /// to return <c>true</c> and calls to <see cref="ThrowIfCancelled()"/> to throw a
        /// <see cref="OperationCanceledException"/>.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Throws a <see cref="OperationCanceledException"/> after <see cref="Cancel()"/> has been called.
        /// </summary>
        void ThrowIfCancelled();

        /// <summary>
        /// Returns the <see cref="CancellationToken"/> that will be signalled when setup is cancelled.
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// Indicates that setup is being cancelled.
        /// </summary>
        bool IsCancelPending { get; }

        /// <summary>
        /// This controls whether <see cref="AddPendingTaskAsync(string, Task, string, string, INodeSshProxy)"/> actually
        /// holds pending tasks to be awaited by a future step (the default) or whether the <see cref="AddPendingTaskAsync(string, Task, string, string, INodeSshProxy)"/>
        /// awaits the task itself.  This is useful for debugging because executing a bunch of tasks in parallel is
        /// likely to mess with the node and global logs since those are not really structured to handle parallel
        /// operations at this time.
        /// </summary>
        bool DisablePendingTasks { get; set; }

        /// <summary>
        /// <para>
        /// Adds a pending task to a group of related tasks, creating the group when necessary.  This is used
        /// as an aid to parallelizing setup operations to improve cluster setup times.
        /// </para>
        /// <note>
        /// If <see cref="DisablePendingTasks"/> is <c>true</c>, then this method will await the task immediately,
        /// creating any empty group if necessary.  This is useful for debugging because executing a bunch of tasks in 
        /// parallel is likely to mess with the node and global logs since those are not really structured to handle
        /// parallel operations at this time.
        /// </note>
        /// </summary>
        /// <param name="groupName">The task group name.</param>
        /// <param name="task">The pending task.</param>
        /// <param name="verb">The progress verb.</param>
        /// <param name="message">The progress message.</param>
        /// <param name="node">
        /// Optionally specifies the node where the operation is happening.  The operation will
        /// be considered to be cluster global when this is <c>null</c>.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="WaitForPendingTasksAsync(string)"/> has already been called for this group.</exception>
        Task AddPendingTaskAsync(string groupName, Task task, string verb, string message, INodeSshProxy node = null);

        /// <summary>
        /// Waits for the pending tasks in a group to complete.
        /// </summary>
        /// <param name="groupName">The task group name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="WaitForPendingTasksAsync(string)"/> has already been called for this group.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when there's no group named <paramref name="groupName"/>.</exception>
        Task WaitForPendingTasksAsync(string groupName);

        /// <summary>
        /// Returns the names of any pending task groups that have not been awaited via <see cref="WaitForPendingTasksAsync(string)"/>.
        /// This can be used by setup implementations to verify that all pending tasks have completed.
        /// </summary>
        /// <returns>The list of pending task group names.</returns>
        List<string> GetPendingGroups();
    }
}
