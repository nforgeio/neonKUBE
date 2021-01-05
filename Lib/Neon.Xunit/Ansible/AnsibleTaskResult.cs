//-----------------------------------------------------------------------------
// FILE:	    AnsibleTaskResult.cs
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
using System.Text;

using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Xunit
{
    /// <summary>
    /// Holds the results for a specific task executed in an Ansible playbook.
    /// </summary>
    public class AnsibleTaskResult
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="rawTaskResults">The raw Ansible task results.</param>
        /// <remarks>
        /// <para>
        /// We're expecting <paramref name="rawTaskResults"/> to include the lines
        /// starting with:
        /// </para>
        /// <code>
        /// TASK [name] ******
        /// </code>
        /// <para>
        /// and then continuing up to the next task start line or the play recap line:
        /// </para>
        /// <code>
        /// PLAY RECAP ******
        /// </code>
        /// </remarks>
        internal AnsibleTaskResult(string rawTaskResults)
        {
            // NOTE: Task results are expected to look something like this:
            //
            //  TASK [create secret] ***********************************************************************************************************************************
            //  task path: /cwd/secret.yaml:4                                                                                                                           
            //  Using module file /usr/share/ansible/plugins/modules/neon_docker_secret.sh                                                                              
            //  <127.0.0.1> ESTABLISH LOCAL CONNECTION FOR USER: root                                                                                                   
            //  <127.0.0.1> EXEC /bin/sh -c 'echo ~ && sleep 0'                                                                                                         
            //  <127.0.0.1> EXEC /bin/sh -c '( umask 77 && mkdir -p "` echo /root/.ansible/tmp/ansible-tmp-1524955763.6-244901464863242 `" && echo ansible-tmp-1524955763.6-244901464863242="` echo /root/.ansible/tmp/ansible-tmp-1524955763.6-244901464863242 `" ) && sleep 0'                                                
            //  <127.0.0.1> PUT /root/.ansible/tmp/ansible-local-27iDx6Ny/tmpinn6zA TO /root/.ansible/tmp/ansible-tmp-1524955763.6-244901464863242/neon_docker_secret.sh
            //  <127.0.0.1> PUT /root/.ansible/tmp/ansible-local-27iDx6Ny/tmpccEkJK TO /root/.ansible/tmp/ansible-tmp-1524955763.6-244901464863242/args                 
            //  <127.0.0.1> EXEC /bin/sh -c 'chmod u+x /root/.ansible/tmp/ansible-tmp-1524955763.6-244901464863242/ /root/.ansible/tmp/ansible-tmp-1524955763.6-244901464863242/neon_docker_secret.sh /root/.ansible/tmp/ansible-tmp-1524955763.6-244901464863242/args && sleep 0'                                              
            //  <127.0.0.1> EXEC /bin/sh -c '/bin/bash /root/.ansible/tmp/ansible-tmp-1524955763.6-244901464863242/neon_docker_secret.sh /root/.ansible/tmp/ansible-tmp-1524955763.6-244901464863242/args && sleep 0'                                                                                                           
            //  <127.0.0.1> EXEC /bin/sh -c 'rm -f -r /root/.ansible/tmp/ansible-tmp-1524955763.6-244901464863242/ > /dev/null 2>&1 && sleep 0'                         
            //  fatal: [localhost]: FAILED! => {                                                                                                                        
            //      "HasErrors": true,                                                                                                                                                                                                                                                              
            //      "changed": false,                                                                                                                                   
            //      "msg": "One of the [text] or [bytes] module parameters is required.",                                                                               
            //      "stderr_lines": [                                                                                                                                   
            //          "One of the [text] or [bytes] module parameters is required."                                                                                   
            //      ],                                                                                                                                                  
            //      "stdout_lines": [                                                                                                                                   
            //          "Parsing [name]",                                                                                                                               
            //           "Parsing [state]",                                                                                                                              
            //          "Parsing [text]",                                                                                                                               
            //          "Inspecting [my-secret] secret.",                                                                                                               
            //          "my-secret] secret exists."                                                                                                                     
            //      ]                                                                                                                                                   
            //  }                                                                                                                                                       
            //        to retry, use: --limit @/cwd/secret.retry                                                                                                       

            this.RawResults = rawTaskResults;

            using (var reader = new StringReader(rawTaskResults))
            {
                // Extract the task name from the first line.

                var line = reader.ReadLine();

                if (!line.StartsWith("TASK ["))
                {
                    throw new FormatException();
                }

                var p    = "TASK [".Length;
                var pEnd = line.IndexOf(']');

                if (pEnd == -1)
                {
                    throw new FormatException();
                }

                this.TaskName = line.Substring(p, pEnd - p);

                // Ansible logs some uninteresting output lines next.  We're going to
                // skip over these until we see a line starting with "changed: [",
                // "ok: [", or "fatal: [".  Any of these indicate the start of the
                // JSON formatted results.

                this.Success = true;

                for (line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    if (line.StartsWith("fatal: ["))
                    {
                        this.Success = false;
                        break;
                    }

                    if (line.StartsWith("ok: [") ||
                        line.StartsWith("changed: ["))
                    {
                        break;
                    }
                }

                if (line == null)
                {
                    throw new FormatException();
                }

                // Don't try to parse JSON for tasks whose status lines don't end with "{".

                if (!line.EndsWith("{"))
                {
                    return;
                }

                // Build up a JSON string by reading the lines up to a line
                // beginning with "}".

                var sbJson = new StringBuilder();

                sbJson.AppendLine("{");

                for (line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    sbJson.AppendLine(line);

                    if (line.StartsWith("}"))
                    {
                        break;
                    }
                }

                // Parse the JSON results.

                this.HasStatus = true;

                var jObject = NeonHelper.JsonDeserialize<JObject>(sbJson.ToString());

                if (jObject.TryGetValue<bool>("changed", out var changed))
                {
                    this.Changed = changed;
                }

                if (jObject.TryGetValue<string>("msg", out var message))
                {
                    this.Message = message;
                }

                var sbOutput = new StringBuilder();

                if (jObject.TryGetValue<JArray>("stdout_lines", out var stdOutLines))
                {
                    foreach (var item in stdOutLines)
                    {
                        sbOutput.AppendLine(item.ToString());
                    }
                }

                this.OutputText = sbOutput.ToString();

                var sbError = new StringBuilder();

                if (jObject.TryGetValue<JArray>("stderr_lines", out var stdErrLines))
                {
                    foreach (var item in stdErrLines)
                    {
                        sbError.AppendLine(item.ToString());
                    }
                }

                this.ErrorText = sbError.ToString();
            }
        }

        /// <summary>
        /// Returns the raw task results.
        /// </summary>
        public string RawResults { get; private set; }

        /// <summary>
        /// Returns <c>true</c> for tasks that returned JSON status.
        /// </summary>
        public bool HasStatus { get; private set; }

        /// <summary>
        /// Returns the task name.
        /// </summary>
        public string TaskName { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the task succeeded.
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the task made any changes.
        /// </summary>
        public bool Changed { get; private set; }

        /// <summary>
        /// Returns the standard output text from the task.
        /// </summary>
        public string OutputText { get; private set; }

        /// <summary>
        /// Returns the standard error text from the task.
        /// </summary>
        public string ErrorText { get; private set; }

        /// <summary>
        /// Returns the error message (if any).
        /// </summary>
        public string Message { get; private set; }
    }
}
