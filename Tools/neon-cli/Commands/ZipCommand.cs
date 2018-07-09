//-----------------------------------------------------------------------------
// FILE:	    ZipCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.SharpZipLib.Zip;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>zip</b> commands.
    /// </summary>
    public class ZipCommand : CommandBase
    {
        private const string usage = @"
Implements commands to create and extract a ZIP archive.  This is required
because Windows Deflate64 compression isn't currently compatible with the 
[neon] tool.

USAGE:

    neon zip create SOURCE ARCHIVE      - Creates archive from a file/folder
    neon zip extract ARCHIVE FOLDER     - Extracts archive to a folder

ARGUMENTS:

    ARCHIVE     - Path to the ZIP archive file.
    SOURCE      - Path to the source file or folder.
    FOLDER      - Path to the output folder.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "zip" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption || commandLine.Arguments.Length == 0)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            if (commandLine.Arguments.Length != 3)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            Console.WriteLine();

            switch (commandLine.Arguments.First())
            {
                case "create":

                    Create(commandLine.Shift(1));
                    break;

                case "extract":

                    Extract(commandLine.Shift(1));
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: Unexpected [{commandLine.Arguments.First()}] command.");
                    Program.Exit(1);
                    break;
            }

            Program.Exit(0);
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }

        /// <summary>
        /// Implements the <b>create</b> command.
        /// </summary>
        /// <param name="commandLine">The command line with <b>zip</b> removed.</param>
        private void Create(CommandLine commandLine)
        {
            var sourcePath = commandLine.Arguments.ElementAtOrDefault(0);
            var zipPath    = commandLine.Arguments.ElementAtOrDefault(1);

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            var patience = string.Empty;

            if (new FileInfo(sourcePath).Length > 200 * NeonHelper.Mega)
            {
                // Compression is very slow, so add a message to be patient
                // for files >= 200MB.

                patience = " (slow)";
            }

            Console.WriteLine($"Creating: {zipPath}...{patience}");

            if ((File.GetAttributes(sourcePath) & FileAttributes.Directory) != 0)
            {
                new FastZip().CreateZip(zipPath, sourcePath, true, null);
            }
            else
            {
                using (var zip = ZipFile.Create(zipPath))
                {
                    zip.BeginUpdate();
                    zip.Add(sourcePath, Path.GetFileName(sourcePath));
                    zip.CommitUpdate();
                }
            }
        }

        /// <summary>
        /// Implements the <b>extract</b> command.
        /// </summary>
        /// <param name="commandLine">The command line with <b>zip</b> removed.</param>
        private void Extract(CommandLine commandLine)
        {
            var zipPath    = commandLine.Arguments.ElementAtOrDefault(0);
            var folderPath = commandLine.Arguments.ElementAtOrDefault(1);

            Console.WriteLine($"Extracting: {zipPath}");

            new FastZip().ExtractZip(zipPath, folderPath, null);
        }
    }
}
