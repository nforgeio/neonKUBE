//-----------------------------------------------------------------------------
// FILE:	    ResourceFiles.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Provides a nice abstraction over the files and folders from the
    /// <b>Linux</b> folder that are embedded into the application.
    /// </summary>
    /// <remarks>
    /// This class must be manually modified to remain in sync with changes 
    /// to the <b>Linux</b> source folder.
    /// </remarks>
    public static class ResourceFiles
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Simulates a file.
        /// </summary>
        public class File
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="name">The local file name.</param>
            /// <param name="hasVariables">
            /// Indicates whether the file references variables from a <see cref="ClusterDefinition"/>
            /// that need to be expanded.
            /// </param>
            public File(string name, bool hasVariables = false)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

                this.Name         = name;
                this.HasVariables = hasVariables;
            }

            /// <summary>
            /// Returns the local file name.
            /// </summary>
            public string Name { get; private set; }

            /// <summary>
            /// The fully qualified path to the file.
            /// </summary>
            public string Path { get; set; }

            /// <summary>
            /// Returns the file contents.
            /// </summary>
            public string Contents
            {
                get { return System.IO.File.ReadAllText(Path); }
            }

            /// <summary>
            /// Creates a stream over the file contents.
            /// </summary>
            /// <returns>The new <see cref="Stream"/>.</returns>
            public Stream ToStream()
            {
                return new MemoryStream(Encoding.UTF8.GetBytes(Contents));
            }

            /// <summary>
            /// Indicates whether the file references variables from a <see cref="ClusterDefinition"/>
            /// that need to be expanded.
            /// </summary>
            public bool HasVariables { get; private set; }
        }

        /// <summary>
        /// Simulates a file folder.
        /// </summary>
        public class Folder
        {
            private Dictionary<string, File>    files;
            private Dictionary<string, Folder>  folders;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="name">The folder name.</param>
            /// <param name="files">Optional files to be added.</param>
            /// <param name="folders">Optional folders to be added.</param>
            public Folder(string name, IEnumerable<File> files = null, IEnumerable<Folder> folders = null)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

                this.Name    = name;
                this.files   = new Dictionary<string, File>();
                this.folders = new Dictionary<string, Folder>();

                if (files != null)
                {
                    foreach (var file in files)
                    {
                        this.files.Add(file.Name, file);
                    }
                }

                if (folders != null)
                {
                    foreach (var folder in folders)
                    {
                        this.folders.Add(folder.Name, folder);
                    }
                }
            }

            /// <summary>
            /// Returns the folder name.
            /// </summary>
            public string Name { get; private set; }

            /// <summary>
            /// Returns the fully qualified path to the folder.
            /// </summary>
            public string Path { get; private set; }

            /// <summary>
            /// Adds a file to the folder.
            /// </summary>
            /// <param name="name">The local file name.</param>
            /// <param name="file">The file.</param>
            public void AddFile(string name, File file)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
                Covenant.Requires<ArgumentNullException>(file != null, nameof(file));

                file.Path = System.IO.Path.Combine(this.Path, file.Name);

                files.Add(name, file);
            }

            /// <summary>
            /// Enumerates the files in the folder.
            /// </summary>
            /// <returns>An <see cref="IEnumerable{File}"/>.</returns>
            public IEnumerable<File> Files()
            {
                return files.Values;
            }

            /// <summary>
            /// Enumerates the sub folders.
            /// </summary>
            /// <returns>An <see cref="IEnumerable{Folder}"/>.</returns>
            public IEnumerable<Folder> Folders()
            {
                return folders.Values;
            }

            /// <summary>
            /// Recursively initializes the <see cref="Path"/> properties for this folder as
            /// well as any subfiles and subfolders.
            /// </summary>
            /// <param name="parentPath">The parent folder's path.</param>
            public void SetPath(string parentPath)
            {
                this.Path = System.IO.Path.Combine(parentPath, this.Name);

                foreach (var file in Files())
                {
                    file.Path = System.IO.Path.Combine(this.Path, file.Name);
                }

                foreach (var folder in Folders())
                {
                    folder.SetPath(this.Path);
                }
            }

            /// <summary>
            /// Returns the local file with the specified name.
            /// </summary>
            /// <param name="name">The local file name.</param>
            /// <returns>The <see cref="File"/>.</returns>
            /// <exception cref="FileNotFoundException">Thrown if the file is not present.</exception>
            public File GetFile(string name)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

                File file;

                if (files.TryGetValue(name, out file))
                {
                    return file;
                }

                throw new FileNotFoundException($"File [{name}] is not present.", name);
            }

            /// <summary>
            /// Returns the local folder with the specified name.
            /// </summary>
            /// <param name="name">The local folder name.</param>
            /// <returns>The <see cref="File"/>.</returns>
            /// <exception cref="FileNotFoundException">Thrown if the folder is not present.</exception>
            public Folder GetFolder(string name)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

                Folder folder;

                if (folders.TryGetValue(name, out folder))
                {
                    return folder;
                }

                throw new FileNotFoundException($"Folder [{name}] is not present.", name);
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the root resource folder.
        /// </summary>
        public static Folder Root { get; private set; }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static ResourceFiles()
        {
            Root = new Folder("Resources", 
                folders: new List<Folder>()
                {
                    new Folder("Elasticsearch",
                        files: new List<File>()
                        {
                            new File("logstash-index-pattern.json", hasVariables: false),
                            new File("logstash-template.json", hasVariables: false)
                        }),
                    new Folder("Ubuntu-18.04",
                        folders: new List<Folder>()
                        {
                            new Folder("binary",
                                files: new List<File>()
                                {
                                    new File("safe-apt-get.sh", hasVariables: true)
                                }),
                            new Folder("conf",
                                files: new List<File>()
                                {
                                    new File("cluster.conf.sh", hasVariables: true),
                                }),
                            new Folder("setup",
                                files: new List<File>()
                                {
                                    new File("setup-disk.sh", hasVariables: true),
                                    new File("setup-docker.sh", hasVariables: true),
                                    new File("setup-environment.sh", hasVariables: true),
                                    new File("setup-exists.sh", hasVariables: true),
                                    new File("setup-node.sh", hasVariables: true),
                                    new File("setup-ntp.sh", hasVariables: true),
                                    new File("setup-package-proxy.sh", hasVariables: true),
                                    new File("setup-prep.sh", hasVariables: true),
                                    new File("setup-ssd.sh", hasVariables: true),
                                    new File("setup-utility.sh", hasVariables: true),
                                }),
                            new Folder("updates",
                                files: new List<File>()
                                {
                                })
                        })
                });

            // We need to wire up the folder paths.  Note that we need to strip
            // off the leading "file:" for Linux/OSX or "file:///" for Windows.

            string prefix;

            if (NeonHelper.IsWindows)
            {
                prefix = "file:///";
            }
            else
            {
                prefix = "file:";
            }

            var appFolderPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase.Substring(prefix.Length));

            Root.SetPath(appFolderPath);
        }
    }
}
