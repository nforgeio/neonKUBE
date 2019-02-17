//-----------------------------------------------------------------------------
// FILE:	    TempFolder.cs
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
using System.IO;

namespace Neon.IO
{
    /// <summary>
    /// Manages a temporary file system folder to be used for the duration of a unit test.
    /// </summary>
    public sealed class TempFolder : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Optionally specifies the root directory where the temporary folders will
        /// be created.  This defaults to <see cref="System.IO.Path.GetTempPath()"/>
        /// when this is <c>null</c> or empty.
        /// </summary>
        public static string Root { get; set; }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Creates a temporary folder.
        /// </summary>
        public TempFolder()
        {
            if (string.IsNullOrEmpty(Root))
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            }
            else
            {
                Directory.CreateDirectory(Root);
                Path = System.IO.Path.Combine(Root, Guid.NewGuid().ToString());
            }

            Directory.CreateDirectory(Path);
        }

        /// <summary>
        /// Returns the fully qualifed path to the temporary folder.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Deletes the temporary folder and all of its contents.
        /// </summary>
        public void Dispose()
        {
            if (Path != null && Directory.Exists(Path))
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                    Path = null;
                }
                catch (IOException)
                {
                    // We're going to ignore any I/O errors.
                }
            }
        }
    }
}
