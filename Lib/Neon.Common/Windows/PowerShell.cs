//-----------------------------------------------------------------------------
// FILE:	    PowerShell.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Csv;
using Neon.IO;

using Newtonsoft.Json.Linq;

namespace Neon.Windows
{
    /// <summary>
    /// <para>
    /// Internal proxy for executing PowerShell commands on Windows machines.
    /// </para>
    /// <note>
    /// This class requires elevated administrative rights.
    /// </note>
    /// </summary>
    public class PowerShell : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        private const int PowershellBufferWidth = 16192;

        /// <summary>
        /// Optional path to the Powershell Core <b>pwsh</b> executable.  The <b>PATH</b>
        /// environment variable will be searched by default.
        /// </summary>
        public static string PwshPath { get; set; }

        /// <summary>
        /// Returns the path to the Powershell Core <b>pwsh</b> executable.
        /// </summary>
        private static string GetPwshPath()
        {
            return PwshPath ?? "pwsh.exe";
        }

        //---------------------------------------------------------------------
        // Instance members

        private Action<string>  outputAction;
        private Action<string>  errorAction;

        /// <summary>
        /// Default constructor to be used to execute local PowerShell commands.
        /// </summary>
        /// <param name="outputAction">Optionally specifies an action to receive logged output.</param>
        /// <param name="errorAction">Optionally specifies an action to receive logged error output.</param>
        /// <exception cref="NotSupportedException">Thrown if we're not running on Windows.</exception>
        /// <remarks>
        /// You can pass callbacks to the <paramref name="outputAction"/> and/or <paramref name="errorAction"/>
        /// parameters to be receive logged output and errors.  Note that <paramref name="outputAction"/> will receive
        /// both STDERR and STDOUT text if <paramref name="errorAction"/> isn't specified.
        /// </remarks>
        public PowerShell(Action<string> outputAction = null, Action<string> errorAction = null)
        {
            if (!NeonHelper.IsWindows)
            {
                throw new NotSupportedException("PowerShell is only supported on Windows.");
            }

            this.outputAction = outputAction;
            this.errorAction  = errorAction;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~PowerShell()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);

                // Nothing to dispose for the current implementation.
            }
        }

        /// <summary>
        /// Expands any environment variables of the form <b>${NAME}</b> in the input
        /// string and returns the expanded result.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The expanded output string.</returns>
        private string ExpandEnvironmentVars(string input)
        {
            Covenant.Requires<ArgumentNullException>(input != null);

            using (var reader = new PreprocessReader(input))
            {
                reader.VariableExpansionRegex = PreprocessReader.CurlyVariableExpansionRegex;

                // Load the environment variables.

                foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
                {
                    // $hack(jeff.lill):
                    //
                    // Some common Windows environment variables names include characters
                    // like parens that are not compatible with [PreprocessReader].  We're
                    // just going to catch the exceptions and ignore them.

                    var key = (string)item.Key;

                    if (PreprocessReader.VariableValidationRegex.IsMatch(key))
                    {
                        reader.Set(key, (string)item.Value);
                    }
                }

                // Perform the substitutions.

                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Executes a PowerShell command that returns a simple string result.
        /// </summary>
        /// <param name="command">The command string.</param>
        /// <param name="noEnvironmentVars">
        /// Optionally disables that environment variable subsitution (defaults to <c>false</c>).
        /// </param>
        /// <returns>The command response.</returns>
        /// <exception cref="PowerShellException">Thrown if the command failed.</exception>
        public string Execute(string command, bool noEnvironmentVars = false)
        {
            using (var file = new TempFile(suffix: ".ps1"))
            {
                File.WriteAllText(file.Path, 
$@"
try {{
    {command} | Out-String -Width {PowershellBufferWidth}
}}
catch [Exception] {{
    write-host $_Exception.Message
    exit 1
}}
");
                var result = NeonHelper.ExecuteCapture(GetPwshPath(), $"-file \"{file.Path}\"", outputAction: outputAction, errorAction: errorAction);

                // $hack(jeff.lill):
                //
                // Powershell is returning [exitcode=0] even if there was an error and
                // we called the [exit 1] statement.  I'm going to work around this for
                // now by checking the error output stream as well.

                if (result.ExitCode != 0 || result.ErrorText.Length > 0)
                {
                    throw new PowerShellException(result.AllText);
                }

                return result.AllText;
            }
        }

        /// <summary>
        /// Executes a PowerShell command that returns result JSON, subsituting any
        /// environment variable references of the form <b>${NAME}</b> and returning a list 
        /// of <c>dynamic</c> objects parsed from the table with the object property
        /// names set to the table column names and the values parsed as strings.
        /// </summary>
        /// <param name="command">The command string.</param>
        /// <param name="noEnvironmentVars">
        /// Optionally disables that environment variable subsitution (defaults to <c>false</c>).
        /// </param>
        /// <returns>The list of <c>dynamic</c> objects parsed from the command response.</returns>
        /// <exception cref="PowerShellException">Thrown if the command failed.</exception>
        public List<dynamic> ExecuteJson(string command, bool noEnvironmentVars = false)
        {
            Covenant.Requires<ArgumentNullException>(command != null);

            if (!noEnvironmentVars)
            {
                command = ExpandEnvironmentVars(command);

                // $hack(jeff.lill):
                //
                // ExpandEnvironmentVars() appends a CRLF to the end of the 
                // string, so we'll remove that here.

                command = command.TrimEnd();
            }

            using (var file = new TempFile(suffix: ".ps1"))
            {
                // Note that we're hardcoding DEPTH=4.  This seems like a
                // reasonable limit.  We don't want this to be too large
                // because often times the objects returned have cycles.

                File.WriteAllText(file.Path,
$@"
try {{
    {command} | ConvertTo-Json -Depth 4 -EnumsAsStrings -AsArray
}}
catch [Exception] {{
    write-host $_Exception.Message
    exit 1
}}
");
                var result = NeonHelper.ExecuteCapture(GetPwshPath(), $"-file \"{file.Path}\"", outputAction: outputAction, errorAction: errorAction);

                // $hack(jeff.lill):
                //
                // Powershell is returning [exitcode=0] even if there was an error and
                // we called the [exit 1] statement.  I'm going to work around this for
                // now by checking the error outport stream as well.

                if (result.ExitCode != 0 || result.ErrorText.Length > 0)
                {
                    throw new PowerShellException(result.AllText);
                }

                // Even though we specified [-AsArray] we still get a empty string
                // for operations that return an empty list (like Get-VM when there
                // are no VMs).  I'm not sure this happens for all such commands but
                // we'll handle that here.

                var json = result.OutputText;

                if (string.IsNullOrEmpty(json))
                {
                    json = "[]";
                }

                return NeonHelper.JsonDeserialize<List<dynamic>>(json);
            }
        }
    }
}
