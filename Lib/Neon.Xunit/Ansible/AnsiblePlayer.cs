//-----------------------------------------------------------------------------
// FILE:	    AnsiblePlayer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Text;

using Neon.Common;
using Neon.IO;

namespace Neon.Xunit
{
    /// <summary>
    /// Used for running Ansible playbooks within unit tests.
    /// </summary>
    public static class AnsiblePlayer
    {
        /// <summary>
        /// <para>
        /// Plays a playbook within a specific working directory using <b>neon ansible play -- [args] playbook</b>.
        /// </para>
        /// <note>
        /// This method will have Ansible gather facts by default which can be quite slow.
        /// Consider using <see cref="PlayInFolderNoGather(string, string, string[])"/> instead
        /// for unit tests that don't required the facts.
        /// </note>
        /// </summary>
        /// <param name="workDir">The playbook working directory (or <c>null</c> to use a temporary folder).</param>
        /// <param name="playbook">The playbook text.</param>
        /// <param name="args">Optional command line arguments to be included in the command.</param>
        /// <returns>An <see cref="AnsiblePlayResults"/> describing what happened.</returns>
        /// <remarks>
        /// <note>
        /// Use this method for playbooks that need to read or write files.
        /// </note>
        /// </remarks>
        public static AnsiblePlayResults PlayInFolder(string workDir, string playbook, params string[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(playbook), nameof(playbook));

            if (!string.IsNullOrEmpty(workDir))
            {
                Directory.CreateDirectory(workDir);

                Environment.CurrentDirectory = workDir;
                File.WriteAllText(Path.Combine(workDir, "play.yaml"), playbook);

                var response = NeonHelper.ExecuteCapture("neon", new object[] { "ansible", "play", "--noterminal", "--", args, "-vvvv", "play.yaml" });

                return new AnsiblePlayResults(response);
            }
            else
            {
                using (var folder = new TempFolder())
                {
                    var orgDirectory = Environment.CurrentDirectory;

                    try
                    {
                        Environment.CurrentDirectory = folder.Path;
                        File.WriteAllText(Path.Combine(folder.Path, "play.yaml"), playbook);

                        var response = NeonHelper.ExecuteCapture("neon", new object[] { "ansible", "play", "--noterminal", "--", args, "-vvvv", "play.yaml" });

                        return new AnsiblePlayResults(response);
                    }
                    finally
                    {
                        Environment.CurrentDirectory = orgDirectory;
                    }
                }
            }
        }

        /// <summary>
        /// <para>
        /// Plays a playbook within a temporary directory using <b>neon ansible play -- [args] playbook</b>.
        /// </para>
        /// <note>
        /// This method will have Ansible gather facts by default which can be quite slow.
        /// Consider using <see cref="PlayNoGather(string, string[])"/> instead
        /// for unit tests that don't required the facts.
        /// </note>
        /// </summary>
        /// <param name="playbook">The playbook text.</param>
        /// <param name="args">Optional command line arguments to be included in the command.</param>
        /// <returns>An <see cref="AnsiblePlayResults"/> describing what happened.</returns>
        /// <remarks>
        /// <note>
        /// Use this method for playbooks that need to read or write files.
        /// </note>
        /// </remarks>
        public static AnsiblePlayResults Play(string playbook, params string[] args)
        {
            return PlayInFolder(null, playbook, args);
        }

        /// <summary>
        /// Plays a playbook without gathering facts by default within a specific working directory using 
        /// <b>neon ansible play -- [args] playbook</b>.
        /// </summary>
        /// <param name="workDir">The playbook working directory (or <c>null</c> to use a temporary folder).</param>
        /// <param name="playbook">The playbook text.</param>
        /// <param name="args">Optional command line arguments to be included in the command.</param>
        /// <returns>An <see cref="AnsiblePlayResults"/> describing what happened.</returns>
        /// <remarks>
        /// <note>
        /// Use this method for playbooks that need to read or write files.
        /// </note>
        /// <para>
        /// This method will add <b>gather_facts: no</b> to the playbook when
        /// this argument isn't already present.
        /// </para>
        /// </remarks>
        public static AnsiblePlayResults PlayInFolderNoGather(string workDir, string playbook, params string[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(playbook), nameof(playbook));

            // Add "gather_facts: no" to the playbook if this argument
            // isn't already present.

            // $hack(jefflill): 
            //
            // I'm just doing string operations here for simplicitly.  I suppose
            // I could parse the YAML to be somewhat more robust.

            if (!playbook.Contains("gather_facts:"))
            {
                var sbPlaybook     = new StringBuilder();
                var processedHosts = false;

                using (var reader = new StringReader(playbook))
                {
                    foreach (var line in reader.Lines())
                    {
                        if (processedHosts)
                        {
                            sbPlaybook.AppendLine(line);
                            continue;
                        }

                        if (line.TrimStart().StartsWith("hosts:"))
                        {
                            sbPlaybook.AppendLine(line);

                            // We need to use the same indent level.

                            var indent = 0;

                            while (indent < line.Length && line[indent] == ' ')
                            {
                                indent++;
                            }

                            sbPlaybook.AppendLine(new string(' ', indent) + "gather_facts: no");
                            processedHosts = true;
                        }
                        else
                        {
                            sbPlaybook.AppendLine(line);
                        }
                    }

                    playbook = sbPlaybook.ToString();
                }
            }

            return PlayInFolder(workDir, playbook, args);
        }

        /// <summary>
        /// Plays a playbook without gathering facts by default within a temporary directory using 
        /// <b>neon ansible play -- [args] playbook</b>.
        /// </summary>
        /// <param name="playbook">The playbook text.</param>
        /// <param name="args">Optional command line arguments to be included in the command.</param>
        /// <returns>An <see cref="AnsiblePlayResults"/> describing what happened.</returns>
        /// <remarks>
        /// <note>
        /// Use this method for playbooks that need to read or write files.
        /// </note>
        /// <para>
        /// This method will add <b>gather_facts: no</b> to the playbook when
        /// this argument isnt already present.
        /// </para>
        /// </remarks>
        public static AnsiblePlayResults PlayNoGather(string playbook, params string[] args)
        {
            return PlayInFolderNoGather(null, playbook, args);
        }
    }
}
