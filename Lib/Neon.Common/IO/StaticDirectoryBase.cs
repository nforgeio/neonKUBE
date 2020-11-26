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
    /// Helper class that can be used by <see cref="IStaticDirectory"/> implementations.
    /// </summary>
    public abstract class StaticDirectoryBase : IStaticDirectory
    {
        private StaticDirectoryBase     root;

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
            throw new NotImplementedException();
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
