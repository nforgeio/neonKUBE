//-----------------------------------------------------------------------------
// FILE:	    CommandBundle.cs
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

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a collection of files to be uploaded to a Linux server along with the command to be executed 
    /// after the files have been unpacked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is intended for use with the <see cref="NodeProxy{TMetadata}.RunCommand(CommandBundle, RunOptions)"/>
    /// and  <see cref="NodeProxy{TMetadata}.SudoCommand(CommandBundle, RunOptions)"/> methods for situations where
    /// one or more files need to be uploaded to a neonCLUSTER host node and be used when a command is executed.
    /// </para>
    /// <para>
    /// A good example of this is performing a <b>docker stack</b> command on the cluster.  In this case, we need to
    /// upload the DAB file along with any files it references and then we we'll want to execute the the Docker 
    /// client.
    /// </para>
    /// <para>
    /// To use this class, construct an instance passing the command and arguments to be executed.  The command be 
    /// an absolute reference to an executable in folders such as <b>/bin</b> or <b>/usr/local/bin</b>, an executable
    /// somewhere on the current PATH, or relative to the files unpacked from the bundle.  The current working directory
    /// will be set to the folder where the bundle was unpacked, so you can reference local executables like
    /// <b>./MyExecutable</b>.
    /// </para>
    /// <para>
    /// Once a bundle is constructed, you will add <see cref="CommandFile"/> instances specifying the
    /// file data you want to include.  These include the relative path to the file to be uploaded as well
    /// as its text or binary data.  You may also indicate whether each file is to be marked as executable.
    /// </para>
    /// </remarks>
    public class CommandBundle : List<CommandFile>
    {
        /// <summary>
        /// <para>
        /// This is a meta command line argument that can be added to a command
        /// to indicate that the following non-command line option is not to be
        /// considered to be the value for the previous command line option.
        /// </para>
        /// <para>
        /// This is entirely optional but can make <see cref="ToBash(string)"/> 
        /// formatting a bit nicer.
        /// </para>
        /// </summary>
        public const string ArgBreak = "-!arg-break!-";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="args">The command arguments.</param>
        /// <remarks>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// </remarks>
        public CommandBundle(string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(command));

            this.Command = command;
            this.Args    = args ?? new object[0];
        }

        /// <summary>
        /// Returns the command to be executed after the bundle has been unpacked.
        /// </summary>
        public string Command { get; private set; }

        /// <summary>
        /// Returns the command arguments.
        /// </summary>
        public object[] Args { get; private set;}

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

            text = text ?? string.Empty;

            if (linuxCompatible)
            {
                var sb = new StringBuilder();

                using (var reader = new StringReader(text))
                {
                    foreach (var line in reader.Lines())
                    {
                        sb.Append(NeonHelper.ExpandTabs(line, 4));
                        sb.Append('\n');
                    }
                }

                text = sb.ToString();
            }

            Add(new CommandFile()
            {
                Path         = path,
                Text         = text,
                IsExecutable = isExecutable
            });
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

            Add(new CommandFile()
            {
                Path         = path,
                Data         = data ?? new byte[0],
                IsExecutable = isExecutable
            });
        }

        /// <summary>
        /// Verifies that the bundle is valid.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the bundle is not valid.</exception>
        public void Validate()
        {
        }

        /// <summary>
        /// Ensures that a Bash command argument is escaped as necessary.
        /// </summary>
        /// <param name="arg">The argument string.</param>
        /// <returns>The safe argument.</returns>
        private string SafeArg(string arg)
        {
            if (arg.IndexOfAny(new char[] { ' ', '\t', '"' }) != -1)
            {
                arg = arg.Replace('\t', ' ');
                arg = arg.Replace("\"", "\\\"");
                arg = "\"" + arg + "\"";
            }

            return arg;
        }

        /// <summary>
        /// Renders the command and arguments as a Bash compatible command line.
        /// </summary>
        /// <returns>The command line.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append(Command);

            foreach (var arg in Args)
            {
                if (arg == null)
                {
                    continue;
                }

                sb.Append(' ');

                if (arg is bool)
                {
                    sb.Append((bool)arg ? "true" : "false");
                }
                else if (arg is IEnumerable<string>)
                {
                    // Expand string arrays into multiple arguments.

                    var first = true;

                    foreach (var value in (IEnumerable<string>)arg)
                    {
                        var valueString = value.ToString();

                        if (string.IsNullOrWhiteSpace(valueString))
                        {
                            valueString = "-"; // $todo(jeff.lill): Not sure if this makes sense any more.
                        }
                        else if (valueString.Contains(' '))
                        {
                            valueString = "\"" + valueString + "\"";
                        }

                        if (first)
                        {
                            first = false;
                        }
                        else
                        {
                            sb.Append(' ');
                        }

                        sb.Append(valueString);
                    }
                }
                else
                {
                    var argString = arg.ToString();

                    if (string.IsNullOrWhiteSpace(argString))
                    {
                        argString = "-";
                    }
                    else if (argString.Contains(' '))
                    {
                        argString = "\"" + argString + "\"";
                    }

                    sb.Append(argString);
                }
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
        /// Thrown because <see cref="ToBash"/> does not support commands with attached files.
        /// </exception>
        /// <remarks>
        /// This can be useful for making copies of cluster configuration commands
        /// on the server as scripts for sutiations where system operators need
        /// to manually tweak things.
        /// </remarks>
        public string ToBash(string comment = null)
        {
            var sb = new StringBuilder();

            // We're going to make this look nice by placing any arguments on
            // separate lines and trying to pair options and values on the
            // same line.

            if (!string.IsNullOrWhiteSpace(comment))
            {
                sb.AppendLine($"# {comment}");
                sb.AppendLine();
            }

            sb.Append(Command);

            var argIndex = 0;

            while (argIndex < Args.Length)
            {
                var arg = Args[argIndex++].ToString();

                if (arg == ArgBreak)
                {
                    continue;   // Ignore these
                }

                sb.AppendLine(" \\");

                if (!arg.StartsWith("-"))
                {
                    sb.Append($"    {SafeArg(arg)}");
                    argIndex++;
                    continue;
                }

                sb.Append($"    {SafeArg(arg)}");

                // The current argument is a command line option.  If there's
                // another argument and it's not a command line option, we're
                // going format it on the same line.
                //
                // This is a decent, but not perfect, heuristic because it
                // treat the first non-option argument as belonging to the
                // last command line option without a value.
                //
                // The workaround is to add a [CommandStep.ArgBreak] string 
                // to the parameters just before any non-option arguments.

                if (argIndex < Args.Length)
                {
                    var nextArg = Args[argIndex].ToString();

                    if (nextArg.StartsWith("-") || nextArg == ArgBreak)
                    {
                        continue;
                    }

                    sb.Append($" {SafeArg(nextArg)}");
                    argIndex++;
                }
            }

            sb.AppendLine();

            return sb.ToString();
        }
    }
}
