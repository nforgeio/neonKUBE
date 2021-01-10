//-----------------------------------------------------------------------------
// FILE:        IStaticDirectoryExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Common;

namespace Neon.IO
{
    /// <summary>
    /// Extension methods for <see cref="IStaticDirectory"/>.
    /// </summary>
    public static class IStaticDirectoryExtensions
    {
        /// <summary>
        /// Creates a ZIP file, including the selected files from the static directory.
        /// </summary>
        /// <param name="directory">The static directory instance.</param>
        /// <param name="zipPath">Path to the output ZIP file.</param>
        /// <param name="searchPattern">
        /// Optionally specifies a file name pattern using standard file system wildcards
        /// like <b>[*]</b> and <b>[?]</b>.  This defaults to including all files.
        /// </param>
        /// <param name="options">Optionally requires a recursive search.  This defaults to 
        /// <see cref="SearchOption.TopDirectoryOnly"/>.
        /// </param>
        public static void Zip(this IStaticDirectory directory, string zipPath, string searchPattern = null, SearchOption options = SearchOption.TopDirectoryOnly)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(zipPath), nameof(zipPath));

            using (var stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.ReadWrite))
            {
                Zip(directory, stream, searchPattern, options);
            }
        }

        /// <summary>
        /// Writes a ZIP file to a stream, including the selected files from the static directory.
        /// </summary>
        /// <param name="directory">The static directory instance.</param>
        /// <param name="zipStream">The output stream.</param>
        /// <param name="searchPattern">
        /// Optionally specifies a file name pattern using standard file system wildcards
        /// like <b>[*]</b> and <b>[?]</b>.  This defaults to including all files.
        /// </param>
        /// <param name="options">Optionally requires a recursive search.  This defaults to 
        /// <see cref="SearchOption.TopDirectoryOnly"/>.
        /// </param>
        /// <remarks>
        /// <note>
        /// The current implementation loads the files into memory so this isn't really suitable
        /// for zipping very large files.
        /// </note>
        /// </remarks>
        public static void Zip(this IStaticDirectory directory, Stream zipStream, string searchPattern = null, SearchOption options = SearchOption.TopDirectoryOnly)
        {
            Covenant.Requires<ArgumentNullException>(zipStream != null, nameof(zipStream));

            using (var zip = ZipFile.Create(zipStream))
            {
                zip.BeginUpdate();

                foreach (var file in directory.GetFiles(searchPattern, options))
                {
                    var relativePath = file.Path.Substring(directory.Path.Length + 1);
                    var data         = file.ReadAllBytes();

                    zip.Add(new StaticBytesDataSource(data), relativePath);
                }

                zip.CommitUpdate();
            }
        }
    }
}
