//-----------------------------------------------------------------------------
// FILE:	    StaticBytesDataSource.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ICSharpCode.SharpZipLib.Zip
{
    /// <summary>
    /// Implements a <see cref="IStaticDataSource"/> that wraps an in-memory byte array
    /// into a form suitable for adding to a <see cref="ZipFile"/>.
    /// </summary>
    public class StaticBytesDataSource : IStaticDataSource
    {
        private byte[] data;

        /// <summary>
        /// Constructs a source from raw bytes.
        /// </summary>
        /// <param name="data">The data array or <c>null</c>.</param>
        public StaticBytesDataSource(byte[] data)
        {
            this.data = data;
        }

        /// <summary>
        /// Constructs a source from a UTG-8 encopded string.
        /// </summary>
        /// <param name="data">The data string</param>
        public StaticBytesDataSource(string data)
        {
            this.data = Encoding.UTF8.GetBytes(data);
        }

        /// <inheritdoc/>
        public Stream GetSource()
        {
            if (data == null)
            {
                return new MemoryStream();
            }
            else
            {
                return new MemoryStream(data);
            }
        }
    }
}
