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
using System.Reflection;
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
        /// <param name="path">Specifies the logical Lunix-style path to directory.</param>
        protected StaticDirectoryBase(StaticDirectoryBase root, StaticDirectoryBase parent, string path)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            this.root   = root ?? this;
            this.Parent = parent;
            this.Path   = path;
        }

        /// <summary>
        /// Returns the parent directory or <c>null</c> when this is the root directory.
        /// </summary>
        public virtual IStaticDirectory Parent { get; private set; }

        /// <summary>
        /// Returns the logical Linux style path to this directory relative to the
        /// root directory,
        /// </summary>
        public virtual string Path { get; private set;}

        /// <summary>
        /// Returns the list of subdirectories present in this directory.
        /// </summary>
        public virtual List<StaticDirectoryBase> Directories { get; private set; } = new List<StaticDirectoryBase>();

        /// <summary>
        /// Returns the list of files present in this directory.
        /// </summary>
        public virtual List<StaticFileBase> Files { get; private set; } = new List<StaticFileBase>();

        /// <inheritdoc/>
        public IEnumerable<IStaticDirectory> GetDirectories(string searchPattern = null, SearchOption options = SearchOption.TopDirectoryOnly)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IStaticFile GetFile(string path)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IEnumerable<IStaticFile> GetFiles(string searchPattern = null, SearchOption options = SearchOption.TopDirectoryOnly)
        {
            throw new NotImplementedException();
        }
    }
}
