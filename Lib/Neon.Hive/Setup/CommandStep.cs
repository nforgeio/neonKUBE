//-----------------------------------------------------------------------------
// FILE:	    CommandStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// Runs a Linux command on a Docker host, optionally uploading some command related files first.
    /// Commands are executed with root privileges.
    /// </summary>
    public class CommandStep : ConfigStep
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates a configuration step that executes a command under <b>sudo</b>
        /// on a specific Docker node.
        /// </summary>
        /// <param name="nodeName">The Docker node name.</param>
        /// <param name="command">The Linux command.</param>
        /// <param name="args">The command arguments.</param>
        public static CommandStep CreateSudo(string nodeName, string command, params object[] args)
        {
            return new CommandStep(nodeName, command, args)
            {
                Sudo = true
            };
        }

        /// <summary>
        /// Creates an idempotent configuration step that executes a command under <b>sudo</b>
        /// on a specific Docker node.
        /// </summary>
        /// <param name="nodeName">The Docker node name.</param>
        /// <param name="operationName">The idempotent operation name.</param>
        /// <param name="command">The Linux command.</param>
        /// <param name="args">The command arguments.</param>
        public static CommandStep CreateIdempotentSudo(string nodeName, string operationName, string command, params object[] args)
        {
            return new CommandStep(nodeName, command, args)
            {
                Sudo          = true,
                operationName = operationName
            };
        }

        /// <summary>
        /// Creates a configuration step that executes a Docker command under <b>sudo</b>
        /// on a specific Docker node while attempting to handle transient errors.
        /// </summary>
        /// <param name="nodeName">The Docker node name.</param>
        /// <param name="command">The Linux command.</param>
        /// <param name="args">The command arguments.</param>
        /// <remarks>
        /// <para>
        /// This method attempts to retry transient Docker client errors (e.g. when an
        /// image pull fails for some reason).  Using this will be more reliable than
        /// executing the command directly, especially on large hives.
        /// </para>
        /// <note>
        /// You'll need to passes the full Docker command, including the leading
        /// <b>docker</b> client program name.
        /// </note>
        /// <para>
        /// The <paramref name="args"/> parameter optionally specifies an array of
        /// command argument objects.  With a few exceptions, these arguments will 
        /// be passed to the command by rendering the object into a <c>string</c>
        /// by calling its <see cref="Object.ToString()"/> method.  <c>null</c>
        /// and empty string arguments will be ignored and <see cref="IEnumerable{strring}"/>
        /// arguments will be expanded.
        /// </para>
        /// <para>
        /// <c>bool</c> and <c>double</c> arguments get special treatment.  <c>bool</c>
        /// values will be rendered as <c>true</c> or <c>false</c> and <c>double</c>
        /// arguments will be rendered using <c>double.ToString("#.0")</c>.  If you
        /// need something different, you can convert your arguments to strings first.
        /// </para>
        /// </remarks>
        public static CommandStep CreateDocker(string nodeName, string command, params object[] args)
        {
            return new CommandStep(nodeName, command, args)
            {
                Sudo     = true,
                isDocker = true
            };
        }

        /// <summary>
        /// Creates an idempotent configuration step that executes a Docker command under <b>sudo</b>
        /// on a specific Docker node while attempting to handle transient errors.
        /// </summary>
        /// <param name="nodeName">The Docker node name.</param>
        /// <param name="operationName">The idempotent operation name.</param>
        /// <param name="command">The Linux command.</param>
        /// <param name="args">The command arguments.</param>
        /// <remarks>
        /// <para>
        /// This method attempts to retry transient Docker client errors (e.g. when an
        /// image pull fails for some reason).  Using this will be more reliable than
        /// executing the command directly, especially on large hives.
        /// </para>
        /// <note>
        /// You'll need to passes the full Docker command, including the leading
        /// <b>docker</b> client program name.
        /// </note>
        /// <para>
        /// The <paramref name="args"/> parameter optionally specifies an array of
        /// command argument objects.  With a few exceptions, these arguments will 
        /// be passed to the command by rendering the object into a <c>string</c>
        /// by calling its <see cref="Object.ToString()"/> method.  <c>null</c>
        /// and empty string arguments will be ignored and <see cref="IEnumerable{strring}"/>
        /// arguments will be expanded.
        /// </para>
        /// <para>
        /// <c>bool</c> and <c>double</c> arguments get special treatment.  <c>bool</c>
        /// values will be rendered as <c>true</c> or <c>false</c> and <c>double</c>
        /// arguments will be rendered using <c>double.ToString("#.0")</c>.  If you
        /// need something different, you can convert your arguments to strings first.
        /// </para>
        /// </remarks>
        public static CommandStep CreateIdempotentDocker(string nodeName, string operationName, string command, params object[] args)
        {
            return new CommandStep(nodeName, command, args)
            {
                Sudo          = true,
                isDocker      = true,
                operationName = operationName
            };
        }

        //---------------------------------------------------------------------
        // Instance members

        private string          nodeName;
        private CommandBundle   commandBundle;
        private bool            isDocker;
        private string          operationName;

        /// <summary>
        /// Constructs a configuration step that executes a command under <b>sudo</b>
        /// on a specific Docker node.
        /// </summary>
        /// <param name="nodeName">The Docker node name.</param>
        /// <param name="command">The Linux command.</param>
        /// <param name="args">The command arguments.</param>
        /// <remarks>
        /// <note>
        /// You can add <see cref="CommandBundle.ArgBreak"/> as one of the arguments.  This is
        /// a meta argument that indicates that the following non-command line option
        /// is not to be considered to be the value for the previous command line option.
        /// This is a formatting hint for <see cref="ToBash(string)"/> and will
        /// not be included in the command itself.
        /// </note>
        /// </remarks>
        private CommandStep(string nodeName, string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command));
            Covenant.Requires<ArgumentNullException>(args != null);

            this.nodeName      = nodeName;
            this.commandBundle = new CommandBundle(command, args);
        }

        /// <summary>
        /// Indicates whether the command is to be executed with <b>sudo</b> privileges.
        /// </summary>
        public bool Sudo { get; private set; }

        /// <summary>
        /// Adds a text file to be uploaded before executing the command.
        /// </summary>
        /// <param name="path">The file path relative to the directory where the command will be executed.</param>
        /// <param name="text">The file text.</param>
        /// <param name="isExecutable">Optionally specifies that the file is to be marked as executable.</param>
        /// <param name="linuxCompatible">
        /// Optionally controls whether the text is made Linux compatible by removing carriage returns
        /// and expanding TABs into spaces.  This defaults to <c>true</c>.
        /// </param>
        public void AddFile(string path, string text, bool isExecutable = false, bool linuxCompatible = true)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            commandBundle.AddFile(path, text, isExecutable, linuxCompatible);
        }

        /// <summary>
        /// Adds a binary file to be uploaded before executing the command.
        /// </summary>
        /// <param name="path">The file path relative to the directory where the command will be executed.</param>
        /// <param name="data">The file data.</param>
        /// <param name="isExecutable">Optionally specifies that the file is to be marked as executable.</param>
        public void AddFile(string path, byte[] data, bool isExecutable = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            commandBundle.AddFile(path, data, isExecutable);
        }

        /// <inheritdoc/>
        public override void Run(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            var node = hive.GetNode(nodeName);

            if (operationName == null)
            {
                Execute(node);
            }
            else
            {
                node.InvokeIdempotentAction(operationName, () => Execute(node));
            }
        }

        /// <summary>
        /// Actually executes the command on the node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void Execute(SshProxy<NodeDefinition> node)
        {
            var status = this.ToString();

            // Limit the node status to a maximum of 80 characters.  For strings
            // longer than this, we're going to scan backwards from character 80
            // until we find a space and then truncate the string at the space
            // so the status will look nice.

            if (status.Length > 80)
            {
                var pos = 80 - "...".Length;    // Leave space for "..."

                for (; pos > 0; pos--)
                {
                    if (status[pos] == ' ')
                    {
                        break;
                    }
                }

                if (pos > 0)
                {
                    status = status.Substring(0, pos) + "...";
                }
                else
                {
                    // Fallback on the chance that a long status has no spaces
                    // before the break.

                    status = status.Substring(0, 77) + "...";
                }
            }

            node.Status = status;

            if (commandBundle.Count == 0)
            {
                // We can execute the command directly if we're
                // not uploading any files.

                if (isDocker)
                {
                    node.DockerCommand(commandBundle.Command, commandBundle.Args);
                }
                else if (Sudo)
                {
                    node.SudoCommand(commandBundle.Command, commandBundle.Args);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                if (isDocker)
                {
                    throw new NotImplementedException();
                }
                else if (Sudo)
                {
                    node.SudoCommand(commandBundle);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            StatusPause();

            node.Status = string.Empty;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append($"{commandBundle.Command}");

            foreach (var arg in CommandBundle.NormalizeArgs(commandBundle.Args))
            {
                sb.AppendWithSeparator(arg);
            }

            if (commandBundle.Count > 0)
            {
                sb.Append($" [files={commandBundle.Count}]");
            }

            return sb.ToString();
        }

        /// <summary>
        /// <para>
        /// Formats the command such that it could be added to a Bash script.
        /// </para>
        /// <note>
        /// This doesn't work if the command has attached files.
        /// </note>
        /// </summary>
        /// <param name="comment">Optional comment text (without a leading <b>#</b>).</param>
        /// <returns>The command formatted for Bash.</returns>
        /// <exception cref="NotSupportedException">
        /// <see cref="ToBash"/> does not support commands with attached files.
        /// </exception>
        /// <remarks>
        /// This can be useful for making copies of hive configuration commands
        /// on the server as scripts for situations where system operators need
        /// to manually tweak things.
        /// </remarks>
        public string ToBash(string comment = null)
        {
            return commandBundle.ToBash();
        }
    }
}
