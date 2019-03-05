//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.IO.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neon.Common
{
    public static partial class NeonHelper
    {
        /// <summary>
        /// Reads a password from the <see cref="Console"/> terminated by <b>Enter</b>
        /// without echoing the typed characters.
        /// </summary>
        /// <param name="prompt">Optional prompt.</param>
        /// <returns>The password entered.</returns>
        public static string ReadConsolePassword(string prompt = null)
        {
            if (!string.IsNullOrEmpty(prompt))
            {
                Console.Write(prompt);
            }

            var password = string.Empty;

            while (true)
            {
                var key = Console.ReadKey(true);
                var ch  = key.KeyChar;

                if (ch == '\r' || ch == '\n')
                {
                    Console.WriteLine();

                    return password.Trim();
                }
                else if (ch == '\b' && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                }
                else
                {
                    password += ch;
                }
            }
        }

        /// <summary>
        /// Recursively deletes a file system folder, ignoring any errors.
        /// </summary>
        /// <param name="folder">The folder path.</param>
        public static void DeleteFolder(string folder)
        {
            if (Directory.Exists(folder))
            {
                try
                {
                    DeleteFolderContents(folder);
                }
                catch
                {
                    // Intentionally ignoring errors.
                }
            }
        }

        /// <summary>
        /// Recursively deletes the contents of a file folder, ignoring any errors.
        /// </summary>
        /// <param name="folder">The folder path.</param>
        public static void DeleteFolderContents(string folder)
        {
            if (Directory.Exists(folder))
            {
                try
                {
                    foreach (var path in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var attributes = File.GetAttributes(path);

                            if (attributes.HasFlag(FileAttributes.ReadOnly))
                            {
                                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                            }

                            File.Delete(path);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // We see this exception when trying to delete read-only files
                            // so we'll clear the read-only flag and try again.

                            try
                            {
                                File.SetAttributes(path, FileAttributes.Normal);
                                File.Delete(path);
                            }
                            catch
                            {
                                // Intentionally ignored.
                            }
                        }
                        catch
                        {
                            // Intentionally ignored.
                        }
                    }

                    DeleteFolder(folder, 0, deleteTop: false);
                }
                catch
                {
                    // Intentionally ignored.
                }
            }
        }

        /// <summary>
        /// <para>
        /// Recursively deletes a directory.  Note that this assumes that any files
        /// have already been deleted.
        /// </para>
        /// <note>
        /// This method intentially ignores any errors.
        /// </note>
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <param name="level">The nesting level (top == 0).</param>
        /// <param name="deleteTop">Optionally deletes the top directory.</param>
        private static void DeleteFolder(string path, int level, bool deleteTop = false)
        {
            foreach (var subFolder in Directory.GetDirectories(path, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (subFolder == "." || subFolder == "..")
                {
                    continue;
                }

                DeleteFolder(subFolder, level + 1);
            }

            if (level > 0 || deleteTop)
            {
                try
                {
                    Directory.Delete(path);
                }
                catch
                {
                    // Intentionally ignored.
                }
            }
        }

        /// <summary>
        /// Recursively copies the files within one directory to another, creating
        /// target folders as required.
        /// </summary>
        /// <param name="sourceFolder">The source folder.</param>
        /// <param name="targetFolder">The target folder.</param>
        /// <remarks>
        /// <note>
        /// This method does not currently copy empty folders.
        /// </note>
        /// </remarks>
        public static void CopyFolder(string sourceFolder, string targetFolder)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sourceFolder));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetFolder));

            sourceFolder = Path.GetFullPath(sourceFolder);
            targetFolder = Path.GetFullPath(targetFolder);

            Directory.CreateDirectory(targetFolder);

            foreach (var sourceFile in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                var relativePath = sourceFile.Substring(sourceFolder.Length + 1);
                var targetFile   = Path.Combine(targetFolder, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(sourceFile, targetFile);
            }
        }

        /// <summary>
        /// Opens the current process standard inout stream.
        /// </summary>
        /// <returns>The open <see cref="Stream"/>.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method integrates with <see cref="ProgramRunner"/> such that
        /// program executions simulated by calls to <see cref="ProgramRunner.ExecuteWithInput(ProgramEntrypoint, string, string[])"/>
        /// or <see cref="ProgramRunner.ExecuteWithInput(ProgramEntrypoint, byte[], string[])"/>
        /// can read the simulated input.
        /// </para>
        /// <para>
        /// Should generally call this instead of calling <see cref="Console.OpenStandardInput()"/>
        /// directly.
        /// </para>
        /// </note>
        /// </remarks>
        public static Stream OpenStandardInput()
        {
            if (ProgramRunner.Current != null)
            {
                return ProgramRunner.Current.OpenStandardInput();
            }
            else
            {
                return Console.OpenStandardInput();
            }
        }

        /// <summary>
        /// Reads the <b>standard input</b> file to the end and returns the
        /// result as a string.
        /// </summary>
        /// <returns>The standard input.</returns>
        public static string ReadStandardInputText()
        {
            using (var input = OpenStandardInput())
            {
                using (var reader = new StreamReader(input, detectEncodingFromByteOrderMarks: true))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Reads the <b>standard input</b> file to the end and returns the
        /// result as bytes.
        /// </summary>
        /// <returns>The standard input.</returns>
        public static byte[] ReadStandardInputBytes()
        {
            using (var stdin = OpenStandardInput())
            {
                using (var ms = new MemoryStream())
                {
                    var buffer = new byte[8192];

                    while (true)
                    {
                        var cb = stdin.Read(buffer, 0, buffer.Length);

                        if (cb == 0)
                        {
                            break;
                        }

                        ms.Write(buffer, 0, cb);
                    }

                    return ms.ToArray();
                }
            }
        }
    }
}
