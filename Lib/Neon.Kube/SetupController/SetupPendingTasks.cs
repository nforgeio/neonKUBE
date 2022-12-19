//-----------------------------------------------------------------------------
// FILE:	    SetupPendingTasks.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Data;
using Neon.Kube.Proxy;
using Neon.Tasks;

namespace Neon.Kube
{
    /// <summary>
    /// Used to track and wait for previously initiated setup tasks to complete by
    /// a subsequent setup step managed by a <see cref="ISetupController"/>"/>.
    /// </summary>
    internal class SetupPendingTasks
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Holds state for a dependency task.
        /// </summary>
        private struct PendingTask
        {
            public Task             Task { get; set; }
            public string           Verb { get; set; }
            public string           Message { get; set; }
            public INodeSshProxy    Node { get; set;}
        }

        //---------------------------------------------------------------------
        // Implementation

        private List<PendingTask>   pendingTasks    = new List<PendingTask>();
        private bool                waitAsyncCalled = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SetupPendingTasks()
        {
        }

        /// <summary>
        /// Returns <c>true</c> when <see cref="WaitAsync"/> has been called and all dependency
        /// tasks have completed.
        /// </summary>
        public bool IsComplete { get; private set; } = false;

        /// <summary>
        /// Adds a dependency task to the collection along with information that will be used
        /// to update the setup progress user experience.
        /// </summary>
        /// <param name="task">The dependency task.</param>
        /// <param name="verb">The verb to be used when updating setup progress.</param>
        /// <param name="message">The message to be used when updating setup progress.</param>
        /// <param name="node">
        /// Optionally specifies the node where the operation is happening.  The operation will
        /// be considered to be cluster global when this is <c>null</c>.
        /// </param>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="WaitAsync"/> has already been called.</exception>
        /// <remarks>
        /// <note>
        /// This method assumes that the calling <see cref="ISetupController"/> is protecting calls
        /// with a mutex of some kind.
        /// </note>
        /// </remarks>
        public void Add(Task task, string verb, string message, INodeSshProxy node = null)
        {
            Covenant.Requires<ArgumentNullException>(task != null, nameof(task));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(verb), nameof(verb));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            if (waitAsyncCalled)
            {
                throw new NotSupportedException($"Cannot call [{nameof(Add)}()] after [{nameof(WaitAsync)}()] has been called.");
            }

            pendingTasks.Add(
                new PendingTask()
                {
                    Task    = task,
                    Verb    = verb,
                    Message = message,
                    Node    = node
                });
        }

        /// <summary>
        /// Waits for all of the dependency tasks to complete.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>Thre tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="WaitAsync"/> has already been called.</exception>
        public async Task WaitAsync(ISetupController controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            if (waitAsyncCalled)
            {
                throw new NotSupportedException($"Cannot call [{nameof(Add)}()] after [{nameof(WaitAsync)}()] has been called.");
            }

            foreach (var pendingTask in pendingTasks)
            {
                if (pendingTask.Node != null)
                {
                    controller.LogProgress(pendingTask.Node, verb: pendingTask.Verb, message: pendingTask.Message);
                }
                else
                {
                    controller.LogProgress(verb: pendingTask.Verb, message: pendingTask.Message);
                }

                if (controller.IsCancelPending)
                {
                    return;
                }

                await pendingTask.Task;

                if (pendingTask.Node != null)
                {
                    controller.LogProgress(pendingTask.Node, verb: pendingTask.Verb, message: pendingTask.Message);
                }
                else
                {
                    controller.LogProgress(verb: pendingTask.Verb, message: pendingTask.Message);
                }
            }

            IsComplete = true;
        }
    }
}
