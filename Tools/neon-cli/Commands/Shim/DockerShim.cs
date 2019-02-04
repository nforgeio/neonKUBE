//-----------------------------------------------------------------------------
// FILE:	    DockerShim.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Manages the command line shimming required to pass the command
    /// line and any referenced files from the operator workstation in
    /// to the <b>neon-cli</b> being invoked in a Docker container.
    /// </summary>
    public sealed class DockerShim : IDisposable
    {
        private string      stdInFile = null;
        private int         fileIndex = 0;      // Used to ensure that files copied into the shim will have unique names.

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="commandLine">The command line invoked on the operator workstation.</param>
        public DockerShim(CommandLine commandLine)
        {
            Covenant.Requires<ArgumentException>(commandLine != null);

            this.CommandLine = commandLine;

            // We're going to locate the shim folder within the root [neonkube] folder within
            // the user's home directory.  The root directory is encrypted for Windows and is
            // hopefully encrypted for Linux and OSX.  Encryption is advisable because we may
            // map be passing confidential information into the container.
            //
            // Note that we're also generating a unique folder name so that multiple commands
            // may be running in parallel.

            ShimExternalFolder = Path.Combine(KubeHelper.TempFolder, $"shim-{Guid.NewGuid().ToString("D")}");

            Directory.CreateDirectory(ShimExternalFolder);

            // Write the original command line to a special shim file so the [neon-cli]
            // in the container can display the command line the operator specified.

            File.WriteAllText(Path.Combine(ShimExternalFolder, "__shim.org"), Program.CommandLine.ToString());
        }

        /// <summary>
        /// Deletes the shim folder, if one was created.
        /// </summary>
        public void Dispose()
        {
            if (ShimExternalFolder != null)
            {
                // $todo(jeff.lill):
                //
                // I've seen evidence that one or more files in the shim folder
                // sometimes still have locks on them for a brief time after
                // the container returns.  I'm going to handle this by treating;
                // any IO Exceptions as transient.

                const int retryCount = 5;

                for (int retry = 0; retry < retryCount; retry++)
                {
                    try
                    {
                        Directory.Delete(ShimExternalFolder, recursive: true);
                        break;
                    }
                    catch (IOException)
                    {
                        if (retry >= retryCount - 1)
                        {
                            throw;
                        }

                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }
                }

                ShimExternalFolder = null;
            }
        }

        /// <summary>
        /// Returns the command line invoked on the operator workstation.
        /// </summary>
        public CommandLine CommandLine { get; set; }

        /// <summary>
        /// The list of folders to be mapped from the client workstation into the container.
        /// </summary>
        public List<DockerShimFolder> MappedFolders { get; private set; } = new List<DockerShimFolder>();

        /// <summary>
        /// The list of environment variables to be passed into the container.
        /// These strings can take the form of <b>NAME</b> which will pass the
        /// current value of the environment variable on the workstation in or
        /// as <b>NAME=VALUE</b> which will pass in the environment variable
        /// with a specific value.
        /// </summary>
        public List<string> EnvironmentVariables { get; private set; } = new List<string>();

        /// <summary>
        /// Returns the shim folder path on the operator's workstation.
        /// </summary>
        public string ShimExternalFolder { get; private set; }

        /// <summary>
        /// Rerturns the shim folder path within the command container.
        /// </summary>
        public static string ShimInternalFolder
        {
            // IMPORTANT:
            //
            // Do not change this without also updating the path in the
            // [nkubeio/neon-cli] container scripts.

            get { return "/shim"; }
        }

        /// <summary>
        /// Adds a folder to be shimmed into the Docker container.
        /// </summary>
        /// <param name="folder">The folder.</param>
        public void AddMappedFolder(DockerShimFolder folder)
        {
            Covenant.Requires<ArgumentNullException>(folder != null);

            if (!Directory.Exists(folder.ClientFolderPath))
            {
                throw new FileNotFoundException($"Directory [{folder.ClientFolderPath}] does not exist.");
            }

            MappedFolders.Add(folder);
        }

        /// <summary>
        /// Adds an environment variable specification of the form <b>NAME</b> or
        /// <b>NAME=VALUE</b> to the shim to be passed on to the Docker container.
        /// </summary>
        /// <param name="variable">The variable specification.</param>
        public void AddEnvironmentVariable(string variable)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(variable));

            var fields = variable.Split(new char[] { '=' }, 2);

            if (fields.Length == 2)
            {
                var name = fields[0];

                if (string.IsNullOrWhiteSpace(name) || name.Contains(' '))
                {
                    throw new ArgumentException($"Environment variable [{variable}] specification is not valid.");
                }
            }

            EnvironmentVariables.Add(variable);
        }

        /// <summary>
        /// Indicates that the shimmed command expects Docker terminal emulation.
        /// This will be set to <c>true</c> by default or <c>false</c> when
        /// the command expects data to be piped into standard input.
        /// </summary>
        public bool Terminal
        {
            get { return stdInFile == null; }
        }

        /// <summary>
        /// Adds a file from the operator's workstation that needs to be mounted
        /// into the container so it will be available for the container tool.
        /// </summary>
        /// <param name="path">Path to the file on the operator's workstation.</param>
        /// <param name="dontReplace">Optionally specify that the file is not to be replaced in the command line.</param>
        /// <param name="noGuid">Optionally specify that we shouldn't append a GUID to the file name to ensure uniqueness.</param>
        /// <returns>Name of the file as it will appear in the mounted folder within the container.</returns>
        /// <remarks>
        /// <para>
        /// Commands that accept file references on the command line will need to call
        /// this method to add the file to the shim.  Pass <paramref name="path"/>.
        /// </para>
        /// <para>
        /// By default, this command replaces the first exact reference to the
        /// original file in the command line with shimmed file name.  This can
        /// be disabled by setting <paramref name="dontReplace"/>=<c>true</c>.
        /// </para>
        /// </remarks>
        public string AddFile(string path, bool dontReplace = false, bool noGuid = false)
        {
            if (path == null)
            {
                // Harden against invalid command lines.

                return null;
            }

            string name;

            if (noGuid)
            {
                name = Path.GetFileName(path);
            }
            else
            {
                name = $"{Path.GetFileName(path)}._{fileIndex++}";
            }

            File.Copy(path, Path.Combine(ShimExternalFolder, name));

            if (!dontReplace)
            {
                ReplaceItem(path, name);
            }

            return name;
        }

        /// <summary>
        /// Replaces the first command line item that matches a specified string.
        /// </summary>
        /// <param name="match">The item string to be matched.</param>
        /// <param name="replacment">The new item.</param>
        /// <remarks>
        /// This method is used by commands that need to modify shimmed file references.
        /// </remarks>
        public void ReplaceItem(string match, string replacment)
        {
            // $todo(jeff.lill):
            //
            // This method is overly simplistic.  The problem is that it could be
            // possible replace the wrong command line item.  For example, let's 
            // look at the hypothetical command below that uploads a file 
            // named [upload].
            //
            //      upload file upload
            //
            // Then assume that we need to shim the last [upload] argument to be
            // something like [/shm/upload] inside the container.
            //
            // The problem with this method is that it will replace the first
            // instance of [upload] with [/shm/upload]:
            //
            //      /shm/upload file upload
            //
            // when we wanted:
            //
            //      upload file /shm/upload
            //
            // Conflicts like this will be relatively rare so I'm going to put this
            // off for now, but eventually we'll want to fix this by somehow being
            // able to specify exactly which item we're munging.
            //
            //      https://github.com/jefflill/NeonForge/issues/162

            for (int i = 0; i < CommandLine.Items.Length; i++)
            {
                if (CommandLine.Items[i] == match)
                {
                    CommandLine.Items[i] = replacment;
                    break;
                }
            }
        }

        /// <summary>
        /// Adds data to be piped into the container tool as standard input.
        /// </summary>
        /// <param name="text">Indicates that the input is text as opposed to binary.</param>
        public void AddStdin(bool text = false)
        {
            if (stdInFile != null)
            {
                throw new InvalidOperationException("Cannot specify more than one standard input.");
            }

            stdInFile = $"stdin-{Guid.NewGuid().ToString("D")}";

            using (var output = new FileStream(Path.Combine(ShimExternalFolder, stdInFile), FileMode.Create, FileAccess.ReadWrite))
            {
                using (var input = Console.OpenStandardInput())
                {
                    if (text)
                    {
                        var reader = new StreamReader(input, Encoding.UTF8);

                        foreach (var line in reader.Lines())
                        {
                            output.Write(Encoding.UTF8.GetBytes(line));
                            output.WriteByte(NeonHelper.LF);
                        }
                    }
                    else
                    {
                        input.CopyTo(output);
                    }
                }
            }
        }

        /// <summary>
        /// Specifies the action to be performed on the operator's workstation after the 
        /// <b>neon-cli</b> container returns.  The exit code will be passed to the 
        /// post action.
        /// </summary>
        /// <param name="postAction">The post action.</param>
        public void SetPostAction(Action<int> postAction)
        {
            if (this.PostAction != null)
            {
                throw new InvalidOperationException("Post action already set for this shim.");
            }

            this.PostAction = postAction;
        }

        /// <summary>
        /// Generates the <b>__shim.sh</b> script within the shim folder that to be used
        /// to invoke the tool within the container.
        /// </summary>
        public void WriteScript()
        {
            var shimScriptPath = Path.Combine(ShimExternalFolder, "__shim.sh");

            if (stdInFile == null)
            {
                File.WriteAllText(shimScriptPath, $"neon {CommandLine}");
            }
            else
            {
                File.WriteAllText(shimScriptPath, $"cat {stdInFile} | neon {CommandLine}");
            }
        }

        /// <summary>
        /// Returns the optional action to be performed after the <b>neon-cli</b> container
        /// returns.  Actions expect the tool exit code to be passed.
        /// </summary>
        public Action<int> PostAction { get; private set; }
    }
}
