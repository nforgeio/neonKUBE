//-----------------------------------------------------------------------------
// FILE:	    TempFolder.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
        /// <summary>
        /// Creates a temporary folder.
        /// </summary>
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());

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
