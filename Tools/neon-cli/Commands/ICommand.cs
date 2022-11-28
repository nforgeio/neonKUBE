//-----------------------------------------------------------------------------
// FILE:	    ICommand.cs
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
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements a command.
    /// </summary>
    [ContractClass(typeof(ICommandContract))]
    public interface ICommand
    {
        /// <summary>
        /// Returns the command words.
        /// </summary>
        /// <remarks>
        /// This property is used to map the command line arguments to a command
        /// implemention.  In the simple case, this will be a single word.  You 
        /// may also specify multiple words.
        /// </remarks>
        string[] Words { get; }

        /// <summary>
        /// Returns optional alternative command words like: "ls" for "list", or "rm" for "remove".
        /// </summary>
        /// <remarks>
        /// <note>
        /// This should return <c>null</c> if there are no alternate words and if there
        /// is an alternate, the number of words must be the same as returned be <see cref="Words"/>
        /// for the command.
        /// </note>
        /// </remarks>
        string[] AltWords { get; }

        /// <summary>
        /// Returns the array of extended command line options beyond the common options
        /// supported by the command or an empty array if none.  The option names must
        /// include the leading dash(es).
        /// </summary>
        string[] ExtendedOptions { get; }

        /// <summary>
        /// Indicates that unknown command options should be checked against <see cref="ExtendedOptions"/>.
        /// </summary>
        bool CheckOptions { get; }

        /// <summary>
        /// Returns <c>true</c> if the command requires server SSH credentials to be
        /// specified on the command line via the <b>-u/--user</b> and <b>-p/--password</b>
        /// options vs. obtaining them from the currently logged in cluster secrets or
        /// not needing credentials at all.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        bool NeedsSshCredentials(CommandLine commandLine);

        /// <summary>
        /// Returns the item used to split a command line into two parts with
        /// the left part having standard <b>neon-cli</b> options and the right
        /// part being a command that will be executed remotely.  This returns as
        /// <c>null</c> for commands that don't split.
        /// </summary>
        string SplitItem { get; }

        /// <summary>
        /// Displays help for the command.
        /// </summary>
        void Help();

        /// <summary>
        /// Runs the command.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        Task RunAsync(CommandLine commandLine);
    }

    [ContractClassFor(typeof(ICommand))]
    internal abstract class ICommandContract : ICommand
    {
        public string[]     Words { get; }
        public string[]     AltWords { get; }
        public string[]     ExtendedOptions { get; }
        public bool         CheckOptions { get; }
        public string       SplitItem { get; }

        public void Help()
        {
        }

        public bool NeedsSshCredentials(CommandLine commandLine)
        {
            Covenant.Requires<ArgumentNullException>(commandLine != null, nameof(commandLine));
            return false;
        }

        public async Task RunAsync(CommandLine commandLine)
        {
            Covenant.Requires<ArgumentNullException>(commandLine != null, nameof(commandLine));

            await Task.CompletedTask;
        }
    }
}
