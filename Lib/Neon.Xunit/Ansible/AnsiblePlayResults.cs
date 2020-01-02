//-----------------------------------------------------------------------------
// FILE:	    AnsiblePlayResults.cs
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

using Neon.Common;

namespace Neon.Xunit
{
    /// <summary>
    /// Holds the results from an <see cref="AnsiblePlayer"/> play operation.
    /// </summary>
    public class AnsiblePlayResults
    {
        /// <summary>
        /// Constructs an instance from the execution results of an
        /// <see cref="AnsiblePlayer"/> play operation.
        /// </summary>
        /// <param name="rawResults">The execution results.</param>
        internal AnsiblePlayResults(ExecuteResponse rawResults)
        {
            Covenant.Requires<ArgumentNullException>(rawResults != null, nameof(rawResults));

            RawResults = rawResults;

            if (rawResults.ExitCode != 0 && rawResults.ExitCode != 2)
            {
                // Must be a command line argument or playbook syntax error
                // as opposed to a valid playbook that had one or more failed
                // tasks.

                throw new Exception(rawResults.ErrorText);
            }

            using (var reader = new StringReader(rawResults.OutputText))
            {
                string line;

                // Skip over all lines until we see the first task line.

                for (line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    if (line.StartsWith("TASK [") && line.EndsWith("**********"))
                    {
                        break;
                    }
                }

                var sbTask   = new StringBuilder();
                var lastTask = false;

                while (!lastTask)
                {
                    // Capture the current line and any subsequent lines up to but not
                    // including the next task marker or the PLAY RECAP line and then
                    // use this to create the next task result.

                    sbTask.AppendLine(line);

                    for (line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        if (line.StartsWith("TASK [") && line.EndsWith("**********"))
                        {
                            break;
                        }
                        else if (line.StartsWith("PLAY RECAP **********") && line.EndsWith("**********"))
                        {
                            lastTask = true;
                            break;
                        }

                        sbTask.AppendLine(line);
                    }

                    var taskResult = new AnsibleTaskResult(sbTask.ToString());

                    if (taskResult.HasStatus)
                    {
                        TaskResults.Add(taskResult);
                    }

                    if (!lastTask)
                    {
                        sbTask.Clear();
                        sbTask.AppendLine(line);    // This is the first line of the next task
                    }
                }
            }
        }

        /// <summary>
        /// Returns the raw execution results.
        /// </summary>
        public ExecuteResponse RawResults { get; private set; }

        /// <summary>
        /// Returns the list of <see cref="AnsibleTaskResult"/> instance in the order
        /// of execution.
        /// </summary>
        public List<AnsibleTaskResult> TaskResults { get; private set; } = new List<AnsibleTaskResult>();

        /// <summary>
        /// Returns the first <see cref="AnsibleTaskResult"/> for a named task.
        /// </summary>
        /// <param name="taskName">The task name.</param>
        /// <returns>The <see cref="AnsibleTaskResult"/> or <c>null</c> if the named task was not found.</returns>
        /// <remarks>
        /// <note>
        /// Ansible does not enforce task name uniqueness, so it's possible
        /// to have more than one task sharing the same name.
        /// </note>
        /// </remarks>
        public AnsibleTaskResult GetTaskResult(string taskName)
        {
            return TaskResults.SingleOrDefault(tr => tr.TaskName == taskName);
        }
    }
}
