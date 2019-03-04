//-----------------------------------------------------------------------------
// FILE:	    PreprocessReader.cs
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
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.IO
{
    /// <summary>
    /// Generates a globally unique temporary file name and then 
    /// ensures that the file is removed when the instance is 
    /// disposed.
    /// </summary>
    public sealed class TempFile : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Optionally specifies the root directory where the temporary files will
        /// be created.  This defaults to <see cref="System.IO.Path.GetTempPath()"/>
        /// when this is <c>null</c> or empty and can be overridden for specific
        /// instances by passing a folder path the the constructor.
        /// </summary>
        public static string Root { get; set; }

        //---------------------------------------------------------------------
        // Instance members

        private bool isDisposed = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="suffix">
        /// Optionally specifies the file suffix (including the leading period) to be
        /// appended to the generated file name.  This defaults to <b>.tmp</b>.
        /// </param>
        /// <param name="folder">
        /// Optionally specifies the target folder.  This defaults to the standard
        /// temporary directory for the current user.
        /// </param>
        public TempFile(string suffix = null, string folder = null)
        {
            if (suffix == null)
            {
                suffix = ".tmp";
            }
            else if (suffix.Length > 0 && !suffix.StartsWith("."))
            {
                throw new ArgumentException($"Non-empty [{nameof(suffix)}] arguments must be prefixed with a period.");
            }

            if (string.IsNullOrEmpty(folder))
            {
                folder = Root;
            }

            if (string.IsNullOrEmpty(folder))
            {
                folder = System.IO.Path.GetTempPath();
            }

            Directory.CreateDirectory(folder);

            Path = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, Guid.NewGuid().ToString("D") + suffix));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            try
            {
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }
            }
            catch
            {
                // We're going to ignore any errors deleting the file.
            }

            isDisposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns the fully qualified path to the temporary file.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Returns the file name only.
        /// </summary>
        public string Name => System.IO.Path.GetFileName(Path);
    }
}
