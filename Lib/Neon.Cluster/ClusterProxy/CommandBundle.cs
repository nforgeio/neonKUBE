//-----------------------------------------------------------------------------
// FILE:	    CommandBundle.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
    /// one or more files need to be uploaded to a NeonCluster host node and be used when a command is executed.
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

                var argString = arg.ToString();

                if (argString.Contains(' '))
                {
                    sb.Append($"\"{argString}\"");
                }
                else
                {
                    sb.Append(argString);
                }
            }

            return sb.ToString();
        }
    }
}
