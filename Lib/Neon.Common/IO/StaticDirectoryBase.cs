//-----------------------------------------------------------------------------
// FILE:        StaticDirectoryBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
        private object                              syncLock = new object();
        private StaticDirectoryBase                 root;
        private Dictionary<string, StaticFileBase>  pathToFile;     // Maintained by the root directory

        /// <summary>
        /// Protected constructor.
        /// </summary>
        /// <param name="root">The root directory or <c>null</c> if this is the root.</param>
        /// <param name="parent">The parent directory or <c>null</c> for the root directory.</param>
        /// <param name="name">The directory name (this must be <c>null</c> for the root directory.</param>
        protected StaticDirectoryBase(StaticDirectoryBase root, StaticDirectoryBase parent, string name)
        {

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

            this.root   = root ?? this;
            this.Parent = parent;
            this.Name   = name;
            this.Path   = GetPath();
        }

        /// <summary>
        /// Returns the list of subdirectories present in this directory.
        /// </summary>
        protected virtual List<StaticDirectoryBase> Directories { get; private set; } = new List<StaticDirectoryBase>();

        /// <summary>
        /// Returns the list of files present in this directory.
        /// </summary>
        protected virtual List<StaticFileBase> Files { get; private set; } = new List<StaticFileBase>();

        /// <inheritdoc/>
        public virtual IStaticDirectory Parent { get; private set; }

        /// <inheritdoc/>
        public virtual string Name { get; private set; }

        /// <inheritdoc/>
        public virtual string Path { get; private set;}

        /// <inheritdoc/>
        public virtual IStaticFile GetFile(string path)
        {
            // We're going to do accomplish this with three steps:
            //
            // 1. Convert relative paths to absolute, leaving any ".."
            //    segments in place.



            // 2. Process any ".." segments.


            // 3. Walk the tree of directories, looking for the file.
            
            throw new NotImplementedException();
        }

        /// <summary>
        /// Implemented by the root directory in a file system to quickly search
        /// for a file by fully qualified path.
        /// </summary>
        /// <param name="path">The target file path.</param>
        /// <returns>The <see cref="StaticFileBase"/> for the file if present, otherwise <c>null</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this is not the root node.</exception>
        private StaticFileBase FindFile(string path)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));
            Covenant.Requires<InvalidOperationException>(Parent != null, "This is not the root node.");

            // Note this this is a static file system, so we can rely on the
            // fact that set of files present can no longer be changed when
            // it's possible for this method to be called.
            //
            // The first time this method is called, we'll initialize the 
            // the [pathToFile] dictionary before performing the lookup.

            lock (syncLock)
            {
                if (pathToFile == null)
                {
                    pathToFile = new Dictionary<string, StaticFileBase>(StringComparer.InvariantCultureIgnoreCase);

                    foreach (var file in GetFiles())
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
            var items = new List<StaticFileBase>();

            AddFiles(items, regex, options);

            return items.OrderBy(item => item.Name, StringComparer.InvariantCultureIgnoreCase).ToList();
        }

        /// <inheritdoc/>
        public virtual IEnumerable<IStaticDirectory> GetDirectories(string searchPattern, SearchOption options)
        {
            var regex = NeonHelper.FileWildcardRegex(searchPattern ?? "*.*");
            var items = new List<StaticDirectoryBase>();

            AddDirectories(items, regex, options);

            return items.OrderBy(item => item.Name, StringComparer.InvariantCultureIgnoreCase).ToList();
        }

        /// <summary>
        /// Adds files whose names match the search pattern to the list passed, recursively walking
        /// subdirectories when requested.
        /// </summary>
        /// <param name="items">The output directory list.</param>
        /// <param name="searchPattern">Specifies the search pattern.</param>
        /// <param name="options">Specifies the recursion option.</param>
        private void AddFiles(List<StaticFileBase> items, Regex searchPattern, SearchOption options)
        {
            foreach (var file in Files)
            {
                if (searchPattern.IsMatch(file.Name))
                {
                    items.Add(file);
                }

                if (options == SearchOption.AllDirectories)
                {
                    foreach (var directory in Directories)
                    {
                        directory.AddFiles(items, searchPattern, options);
                    }
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
        private void AddDirectories(List<StaticDirectoryBase> items, Regex searchPattern, SearchOption options)
        {
            foreach (var directory in Directories)
            {
                if (searchPattern.IsMatch(directory.Name))
                {
                    items.Add(directory);
                }

                if (options == SearchOption.AllDirectories)
                {
                    AddDirectories(items, searchPattern, options);
                }
            }
        }
    }
}
