//-----------------------------------------------------------------------------
// FILE:        IStaticFile.cs
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
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.IO
{
    /// <summary>
    /// <para>
    /// Describes a logical file in a static file system.  This is used to abstract access
    /// to static files read from an assembly's embedded resources or potentially from other
    /// sources using Linux style paths.
    /// </para>
    /// <para>
    /// This is currently used to emulate a tree of <see cref="IStaticDirectory"/> and 
    /// <see cref="IStaticFile"/> instances loaded from an assembly's embedded resources
    /// via the <see cref="AssemblyExtensions.GetStaticDirectory(Assembly, string)"/> extension method.
    /// </para>
    /// </summary>
    public interface IStaticFile
    {
        /// <summary>
        /// Returns the Linux style fully qualified path for the static file relative to
        /// the static root directory.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Reads the file contents as a UTF-8 encoded string.
        /// </summary>
        /// <returns>The file contents.</returns>
        public string ReadAllText();

        /// <summary>
        /// Asynchronously reads the file contents as a UTF-8 encoded string.
        /// </summary>
        /// <returns>The file contents.</returns>
        public Task<string> ReadAllTextAsync();

        /// <summary>
        /// Reads the file contents as bytes.
        /// </summary>
        /// <returns>The file contents.</returns>
        public byte[] ReadAllBytes();

        /// <summary>
        /// Asynchronously reads the file contents as bytes.
        /// </summary>
        /// <returns>The file contents.</returns>
        public Task<byte[]> ReadAllBytesAsync();
    }
}
