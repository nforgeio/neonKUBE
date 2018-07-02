//-----------------------------------------------------------------------------
// FILE:	    ICommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Hive;

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
        /// options vs. obtaining them from the currently logged in hive secrets or
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
        void Run(CommandLine commandLine);

        /// <summary>
        /// Called when the tool shim is being executed on the operator's workstation to
        /// convert the command line into something that can be passed into the internal Docker
        /// container and then executed there.
        /// </summary>
        /// <returns>
        /// A <see cref="DockerShimInfo"/> that indicates whether the command should be shimmed
        /// and also whether the hive should be connected before executing the command
        /// in the <b>neon-cli</b> container.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The command is responsible for modifying the shimmed command line as required
        /// as well as adding any files or standard input that will need to be passed
        /// into the container.
        /// </para>
        /// <para>
        /// Commands that need no special handling may simply leave the shim unchanged.
        /// </para>
        /// </remarks>
        DockerShimInfo Shim(DockerShim shim);
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
            Covenant.Requires<ArgumentNullException>(commandLine != null);
            return false;
        }

        public void Run(CommandLine commandLine)
        {
            Covenant.Requires<ArgumentNullException>(commandLine != null);
        }

        public DockerShimInfo Shim(DockerShim shim)
        {
            Covenant.Requires<ArgumentNullException>(shim != null);
            return null;
        }
    }
}
