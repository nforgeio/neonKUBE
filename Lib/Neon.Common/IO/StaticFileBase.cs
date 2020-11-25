//-----------------------------------------------------------------------------
// FILE:        StaticFileBase.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.IO
{
    /// <summary>
    /// Helper class that can be used by <see cref="IStaticFile"/> implementations.
    /// </summary>
    public abstract class StaticFileBase : IStaticFile
    {
        /// <summary>
        /// Protected constructor.
        /// </summary>
        /// <param name="path">The logical path to this file.</param>
        protected StaticFileBase(string path)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            this.Path = path;
        }

        /// <inheritdoc/>
        public virtual string Path { get; private set; }

        /// <inheritdoc/>
        public abstract byte[] ReadAllBytes();

        /// <inheritdoc/>
        public abstract Task<byte[]> ReadAllBytesAsync();

        /// <inheritdoc/>
        public abstract string ReadAllText();

        /// <inheritdoc/>
        public abstract Task<string> ReadAllTextAsync();
    }
}
