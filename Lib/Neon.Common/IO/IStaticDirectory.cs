//-----------------------------------------------------------------------------
// FILE:        IStaticDirectory.cs
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
using System.Collections;
using System.Collections.Generic;
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
    /// via the <see cref="AssemblyExtensions.GetResourceFileSystem(Assembly, string)"/> 
    /// extension method.
    /// </para>
    /// <note>
    /// <b>IMPORTANT: </b>Implementations need to be thread-safe.
    /// </note>
    /// <note>
    /// In general, file and directory name lookup case sensitivity should probably be
    /// case insensitive for most purposes but this is an implementation specific detail. 
    /// </note>
    /// </summary>
    /// <threadsafety instance="true"/>
    public interface IStaticDirectory
    {
        /// <summary>
        /// Returns the directory name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns the fully qualified Linux style path for the static directory relative to
        /// the static root directory.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Returns a reference to the parent directory or <c>null</c> if this is the root directory
        /// for a static file system.
        /// </summary>
        public IStaticDirectory Parent { get; }

        /// <summary>
        /// Returns the directories beneath the current directory, optionally matching directories by
        /// name as well as optionally searching for directories recursively.
        /// </summary>
        /// <param name="searchPattern">Optionally specifies a directory name pattern using standard file system wild cards like <b>[*]</b> and <b>[?]</b></param>
        /// <param name="options">Optionally requires a recursive search.</param>
        /// <returns>The set of matching directories.</returns>
        public IEnumerable<IStaticDirectory> GetDirectories(string searchPattern = null, SearchOption options = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Returns the files beneath the current directory, optionally matching files by
        /// name as well as optionally searching recursively searching subdirectories..
        /// </summary>
        /// <param name="searchPattern">Optionally specifies a directory name pattern using standard file system wild cards like <b>[*]</b> and <b>[?]</b></param>
        /// <param name="options">Optionally requires a recursive search.</param>
        /// <returns>The set of matching files.</returns>
        public IEnumerable<IStaticFile> GetFiles(string searchPattern = null, SearchOption options = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// <para>
        /// Gets a file via a Linux style path.  This path can be absolute relative to the 
        /// root directory or it can be relative to the current directory.
        /// </para>
        /// <note>
        /// Relative paths including <b>/../</b> notation to move up a directory or <b>./</b>
        /// to specify the current directory are not supported.
        /// </note>
        /// </summary>
        /// <param name="path">The file path (absolute or relative).</param>
        /// <returns>The file.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
        public IStaticFile GetFile(string path);

        /// <summary>
        /// <para>
        /// Gets a directory via a Linux style path.  This path can be absolute relative to the 
        /// root directory or it can be relative to the current directory.
        /// </para>
        /// <note>
        /// Relative paths including <b>/../</b> notation to move up a directory or <b>./</b>
        /// to specify the current directory are not supported.
        /// </note>
        /// </summary>
        /// <param name="path">The file path (absolute or relative).</param>
        /// <returns>The directory.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the directory doesn't exist.</exception>
        public IStaticDirectory GetDirectory(string path);
    }
}
