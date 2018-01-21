//-----------------------------------------------------------------------------
// FILE:	    CsvWriter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// $todo(jeff.lill):
//
// I'm going to keep this simple for now and only deal with quoting
// commas and double quote characters.  I believe there are other 
// advanced cases where quoting is required (e.g string with CRLFs).

namespace Neon.Csv
{
    /// <summary>
    /// Writes lines of CSV encoded columns to a file or <see cref="TextWriter" />.
    /// </summary>
    /// <threadsafety instance="false" />
    public sealed class CsvWriter : IDisposable
    {
        private static char[]   escaped = new char[] { ',', '"' };
        private TextWriter      writer;

        /// <summary>
        /// Constructs a writer to a stream.
        /// </summary>
        /// <param name="stream">The output stream.</param>
        /// <param name="encoding">The stream's character <see cref="Encoding" />.</param>
        public CsvWriter(Stream stream, Encoding encoding)
            : this(new StreamWriter(stream, encoding))
        {
        }

        /// <summary>
        /// Constructs a writer to a <see cref="TextWriter" />.
        /// </summary>
        /// <param name="writer">The <see cref="TextWriter" />.</param>
        public CsvWriter(TextWriter writer)
        {
            this.writer = writer;
        }

        /// <summary>
        /// Closes the writer if it is still open.
        /// </summary>
        public void Close()
        {
            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }
        }

        /// <summary>
        /// Releases any resources associated with the reader.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Writes the arguments passed to the output, separating each argument
        /// with a comma and adding escape characters as necessary.
        /// </summary>
        /// <param name="args">The arguments.</param>
        public void WriteLine(params object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string value;

                if (i > 0)
                {
                    writer.Write(',');
                }

                value = args[i] != null ? args[i].ToString() : string.Empty;

                if (value.IndexOfAny(escaped) == -1)
                {
                    writer.Write(value);
                }
                else
                {
                    writer.Write(string.Format("\"{0}\"", value.Replace("\"", "\"\"")));
                }
            }

            writer.WriteLine();
        }
    }
}
