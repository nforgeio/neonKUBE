//-----------------------------------------------------------------------------
// FILE:	    StaticStringDataSource.cs
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
    /// Implements a <see cref="IStaticDataSource"/> that wraps a string to be
    /// returned as URF-8 encoded bytes into a form suitable for adding to a 
    /// <see cref="ZipFile"/>.
    /// </summary>
    public class StaticStringDataSource : IStaticDataSource
    {
        private byte[] data;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="text">The text or <c>null</c>.</param>
        public StaticStringDataSource(string text)
        {
            if (text == null)
            {
                data = null;
            }
            else
            {
                data = Encoding.UTF8.GetBytes(text);
            }
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
