//-----------------------------------------------------------------------------
// FILE:        StaticDirectoryBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.IO
{
    /// <summary>
    /// <para>
    /// Helper class that can be used by <see cref="IStaticDirectory"/> implementations.
    /// </para>
    /// <note>
    /// Implementations derived from this class will use case insensitive file and
    /// directory name mapping.
    /// </note>
    /// </summary>
    public abstract class StaticDirectoryBase : IStaticDirectory
    {
        private object                                  syncLock = new object();
        private StaticDirectoryBase                     root;
        private Dictionary<string, StaticFileBase>      pathToFile;         // Used by the root directory only
        private Dictionary<string, StaticDirectoryBase> pathToDirectory;    // Used by the root directory only
        private Dictionary<string, StaticDirectoryBase> nameToDirectory;
        private Dictionary<string, StaticFileBase>      nameToFile;

        /// <summary>
        /// Protected constructor.
        /// </summary>
        /// <param name="root">The root directory or <c>null</c> if this is the root.</param>
        /// <param name="parent">The parent directory or <c>null</c> for the root directory.</param>
        /// <param name="name">The directory name (this must be <c>null</c> for the root directory.</param>
        protected StaticDirectoryBase(StaticDirectoryBase root, StaticDirectoryBase parent, string name)
        {
            Covenant.Requires<ArgumentException>(name.IndexOf('/') == -1, nameof(name));
            Covenant.Requires<ArgumentException>(name.IndexOf('\\') == -1, nameof(name));

            if (name.StartsWith("/"))
            {
                name = name.Substring(1);
            }

            if (root == null)
            {
                if (parent != null)
                {
                    throw new ArgumentNullException($"[{nameof(parent)}] must be NULL when [{nameof(root)}] is NULL.");
                }

                if (!string.IsNullOrEmpty(name))
                {
                    throw new ArgumentNullException($"[{nameof(name)}] must be NULL or empty when [{nameof(root)}] is NULL.");
                }
            }
            else
            {
                Covenant.Requires<ArgumentNullException>(parent != null);
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            }

            this.root            = root ?? this;
            this.Parent          = parent;
            this.Name            = name;
            this.Path            = GetPath();
            this.nameToDirectory = new Dictionary<string, StaticDirectoryBase>(StringComparer.InvariantCultureIgnoreCase);
            this.nameToFile      = new Dictionary<string, StaticFileBase>(StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Returns the subdirectories present in this directory.
        /// </summary>
        protected virtual IEnumerable<StaticDirectoryBase> Directories => nameToDirectory.Values;

        /// <summary>
        /// Returns the list of files present in this directory.
        /// </summary>
        protected virtual IEnumerable<StaticFileBase> Files => nameToFile.Values;

        /// <inheritdoc/>
        public virtual IStaticDirectory Parent { get; private set; }

        /// <inheritdoc/>
        public virtual string Name { get; private set; }

        /// <inheritdoc/>
        public virtual string Path { get; private set;}

        /// <summary>
        /// Adds a file.
        /// </summary>
        /// <param name="file">The subdirectory.</param>
        public void AddFile(StaticFileBase file)
        {
            nameToFile.Add(file.Name, file);
        }

        /// <summary>
        /// Adds a subdirectory if it doesn't already exist.
        /// </summary>
        /// <param name="directory">The child resource directory.</param>
        /// <returns>The existing <see cref="StaticResourceDirectory"/> or <paramref name="directory"/> if it was added</returns>
        public StaticDirectoryBase AddDirectory(StaticDirectoryBase directory)
        {
            lock (syncLock)
            {
                if (nameToDirectory.TryGetValue(directory.Name, out var existingDirectory))
                {
                    return existingDirectory;
                }

                nameToDirectory.Add(directory.Name, directory);

                return directory;
            }
        }

        /// <inheritdoc/>
        public virtual IStaticFile GetFile(string path)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));
            Covenant.Requires<ArgumentException>(!path.Contains("/../"), $"{nameof(path)}: Relative path segments like \"/../\" are not supported.");
            Covenant.Requires<ArgumentException>(!path.StartsWith("./"), $"{nameof(path)}: Relative path segments like \"./\" are not supported.");

            if (!LinuxPath.IsPathRooted(path))
            {
                path = LinuxPath.Combine(this.Path, path);
            }

            var file = FindFile(path);

            if (file == null)
            {
                throw new FileNotFoundException($"File [{path}] not found.");
            }

            return file;
        }

        /// <summary>
        /// Implemented by the root directory in a file system to quickly search
        /// for a file via a fully qualified path.
        /// </summary>
        /// <param name="path">The target file path.</param>
        /// <returns>The <see cref="StaticFileBase"/> for the file if present, otherwise <c>null</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this is not the root node.</exception>
        private StaticFileBase FindFile(string path)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));

            // Forward to the root directory when this isn't the root.

            if (this.Parent != null)
            {
                return this.root.FindFile(path);
            }

            // Note this this is a static file system, so we can rely on the
            // fact that set of files present can no longer be changed ater
            // it's possible for this method to be called.
            //
            // The first time this method is called, we'll initialize the 
            // the [pathToFile] dictionary before performing the lookup.

            lock (syncLock)
            {
                if (pathToFile == null)
                {
                    pathToFile = new Dictionary<string, StaticFileBase>(StringComparer.InvariantCultureIgnoreCase);

                    foreach (var file in GetFiles(options: SearchOption.AllDirectories))
                    {
                        pathToFile[file.Path] = (StaticFileBase)file;
                    }
                }
            }

            if (pathToFile.TryGetValue(path, out var targetFile))
            {
                return targetFile;
            }
            else
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public IStaticDirectory GetDirectory(string path)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));
            Covenant.Requires<ArgumentException>(!path.Contains("/../"), $"{nameof(path)}: Relative path segments like \"/../\" are not supported.");
            Covenant.Requires<ArgumentException>(!path.StartsWith("./"), $"{nameof(path)}: Relative path segments like \"./\" are not supported.");

            // Special case the root directory.

            if (path == "/")
            {
                return root ?? this;
            }

            if (path.Length > 1 && path.EndsWith("/"))
            {
                path = path.Substring(0, path.Length - 1);
            }

            if (!LinuxPath.IsPathRooted(path))
            {
                path = LinuxPath.Combine(this.Path, path);
            }

            var directory = FindDirectory(path);

            if (directory == null)
            {
                throw new FileNotFoundException($"Directory [{path}] not found.");
            }

            return directory;
        }

        /// <summary>
        /// Implemented by the root directory in a file system to quickly search
        /// for a directory via a fully qualified path.
        /// </summary>
        /// <param name="path">The target directory path.</param>
        /// <returns>The <see cref="StaticDirectoryBase"/> for the file if present, otherwise <c>null</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this is not the root node.</exception>
        private StaticDirectoryBase FindDirectory(string path)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));

            // Forward to the root directory when this isn't the root.

            if (this.Parent != null)
            {
                return this.root.FindDirectory(path);
            }

            // Note this this is a static file system, so we can rely on the
            // fact that set of files present can no longer be changed after
            // it's possible for this method to be called.
            //
            // The first time this method is called, we'll initialize the 
            // the [pathToDirectory] dictionary before performing the lookup.

            lock (syncLock)
            {
                if (pathToDirectory == null)
                {
                    pathToDirectory = new Dictionary<string, StaticDirectoryBase>(StringComparer.InvariantCultureIgnoreCase);

                    foreach (var directory in GetDirectories(options: SearchOption.AllDirectories))
                    {
                        pathToDirectory[directory.Path] = (StaticDirectoryBase)directory;
                    }
                }
            }

            if (pathToDirectory.TryGetValue(path, out var targetDirectory))
            {
                return targetDirectory;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the fully qualified path to the directory.
        /// </summary>
        /// <returns>The Linux style path.</returns>
        internal string GetPath()
        {
            var directoryNames   = new List<string>();
            var currentDirectory = this;

            while (currentDirectory.Parent != null)
            {
                directoryNames.Add(currentDirectory.Name);
                currentDirectory = (StaticDirectoryBase)currentDirectory.Parent;
            }

            directoryNames.Reverse();

            var path = string.Empty;

            foreach (var directoryName in directoryNames)
            {
                path += "/" + directoryName;
            }

            return path;
        }

        /// <inheritdoc/>
        public virtual IEnumerable<IStaticFile> GetFiles(string searchPattern = null, SearchOption options = SearchOption.TopDirectoryOnly)
        {
            var regex = NeonHelper.FileWildcardRegex(searchPattern ?? "*.*");
            var items = new Dictionary<string, StaticFileBase>(StringComparer.InvariantCultureIgnoreCase);

            AddFiles(items, regex, options);

            if (options == SearchOption.AllDirectories)
            {
                foreach (var directory in Directories)
                {
                    directory.AddFiles(items, regex, options);
                }
            }

            return items.OrderBy(item => item.Key, StringComparer.InvariantCultureIgnoreCase).Select(item => item.Value).ToList();
        }

        /// <inheritdoc/>
        public virtual IEnumerable<IStaticDirectory> GetDirectories(string searchPattern = null, SearchOption options = SearchOption.TopDirectoryOnly)
        {
            var regex = NeonHelper.FileWildcardRegex(searchPattern ?? "*.*");
            var items = new Dictionary<string, StaticDirectoryBase>(StringComparer.InvariantCultureIgnoreCase);

            AddDirectories(items, regex, options);

            if (options == SearchOption.AllDirectories)
            {
                foreach (var directory in Directories)
                {
                    directory.AddDirectories(items, regex, options);
                }
            }

            return items.Values.OrderBy(item => item.Path, StringComparer.InvariantCultureIgnoreCase).ToList();
        }

        /// <summary>
        /// Adds files whose names match the search pattern to the list passed, recursively walking
        /// subdirectories when requested.
        /// </summary>
        /// <param name="items">The output directory list.</param>
        /// <param name="searchPattern">Specifies the search pattern.</param>
        /// <param name="options">Specifies the recursion option.</param>
        private void AddFiles(Dictionary<string, StaticFileBase> items, Regex searchPattern, SearchOption options)
        {
            foreach (var file in Files)
            {
                if (searchPattern.IsMatch(file.Name) && !items.ContainsKey(file.Path))
                {
                    items.Add(file.Path, file);
                }
            }

            if (options == SearchOption.AllDirectories)
            {
                foreach (var directory in Directories)
                {
                    directory.AddFiles(items, searchPattern, options);
                }
            }
        }

        /// <summary>
        /// Adds directories whose names match the search pattern to the list passed, recursively walking
        /// subdirectories when requested.
        /// </summary>
        /// <param name="items">The output directory list.</param>
        /// <param name="searchPattern">Specifies the search pattern.</param>
        /// <param name="options">Specifies the recursion option.</param>
        private void AddDirectories(Dictionary<string, StaticDirectoryBase> items, Regex searchPattern, SearchOption options)
        {
            foreach (var directory in Directories)
            {
                if (searchPattern.IsMatch(directory.Name) && !items.ContainsKey(directory.Path))
                {
                    items.Add(directory.Path, directory);
                }
            }

            if (options == SearchOption.AllDirectories)
            {
                foreach (var directory in Directories)
                {
                    directory.AddDirectories(items, searchPattern, options);
                }
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Parent == null ? "/" : Path;
        }
    }
}
