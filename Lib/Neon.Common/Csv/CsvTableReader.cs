//-----------------------------------------------------------------------------
// FILE:	    CsvTableReader.cs
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
using System.Text;

namespace Neon.Csv
{
    /// <summary>
    /// Used to read a CSV table that includes row headers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class makes it easy to process tabular data loaded from a CSV file,
    /// where the first row of the file contains the row header strings that
    /// identify the table columns.
    /// </para>
    /// <para>
    /// Initialize an instance by passing a <see cref="CsvReader" />, stream, string, or file path to the 
    /// constructor.  The constructor will read the first row of the file and initialize the <see cref="ColumnMap" />
    /// dictionary which maps the case insensitive column name to the zero based index of the
    /// column in the table.
    /// </para>
    /// <para>
    /// You'll process each data row by calling <see cref="ReadRow" />.  This returns a list
    /// with the next row of data or <c>null</c> if the end of the table has been reached.  You can
    /// process the row data returned directly or use the <see cref="GetColumn" /> method to
    /// access a column value on the current row directly.
    /// </para>
    /// <note>
    /// This class is tolerant of blank or duplicate column names.  In the case of duplicates, the
    /// first column matching the requested column name will be used when parsing data.
    /// </note>
    /// <para>
    /// Applications should call the reader's <see cref="Dispose" /> or <see cref="Close" />
    /// method when they are finished with the reader so that the underlying <see cref="CsvReader" />
    /// will be closed as well, promptly releasing any system resources (such as the stream).
    /// </para>
    /// </remarks>
    public class CsvTableReader : IDisposable
    {
        private CsvReader                   reader;     // The CSV reader
        private List<string>                columns;    // List of column names in the order read from the source
        private Dictionary<string, int>     columnMap;  // Maps case insensitive column names into column indicies
        private List<string>                row;        // The current row

        /// <summary>
        /// Constructs an instance to read from a <see cref="CsvReader" />.
        /// </summary>
        /// <param name="reader">The <see cref="CsvReader" /> to read from.</param>
        public CsvTableReader(CsvReader reader)
        {

            this.reader    = reader;
            this.columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            this.columns   = new List<string>();

            row = reader.Read();
            if (row == null)
            {
                return;
            }

            for (int i = 0; i < row.Count; i++)
            {
                columns.Add(row[i]);

                if (!columnMap.ContainsKey(row[i]))
                {
                    columnMap.Add(row[i], i);
                }
            }
        }

        /// <summary>
        /// Constructs an instance to read from a <see cref="TextReader" />.
        /// </summary>
        /// <param name="reader">The reader.</param>
        public CsvTableReader(TextReader reader)
            : this(new CsvReader(reader))
        {
        }

        /// <summary>
        /// Constructs an instance to read from a stream.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="encoding">The stream's character <see cref="Encoding" />.</param>
        public CsvTableReader(Stream stream, Encoding encoding)
            : this(new CsvReader(stream, encoding))
        {
        }

        /// <summary>
        /// Constructs an instance to read from a CSV string.
        /// </summary>
        /// <param name="text"></param>
        public CsvTableReader(string text)
            : this(new CsvReader(text))
        {
        }

        /// <summary>
        /// Releases any system resources held by the instance,
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Closes the reader if it is still open.
        /// </summary>
        public void Close()
        {
            if (reader != null)
            {
                reader.Close();
                reader = null;
            }
        }

        /// <summary>
        /// Returns the underlying <see cref="CsvReader" /> or <c>null</c> if the reader is closed.
        /// </summary>
        public CsvReader Reader
        {
            get { return reader; }
        }

        /// <summary>
        /// Returns the list of table columns in the order read from the source.
        /// </summary>
        public List<string> Columns
        {
            get { return columns; }
        }

        /// <summary>
        /// Returns the dictionary that case insensitvely maps a column name to 
        /// the zero based index of the column.
        /// </summary>
        public Dictionary<string, int> ColumnMap
        {
            get { return columnMap; }
        }

        /// <summary>
        /// Reads the next row of table.
        /// </summary>
        /// <returns>The list of column values or <c>null</c> if the end of the table has been reached.</returns>
        public List<string> ReadRow()
        {
            row = reader.Read();

            return row;
        }

        /// <summary>
        /// Returns an enumerator that returns the data rows from a <see cref="CsvTableReader"/>.
        /// </summary>
        /// <returns>The next row as a <see cref="List{String}"/>.</returns>
        public IEnumerable<List<string>> Rows()
        {
            for (var row = ReadRow(); row != null; row = ReadRow())
            {
                yield return row;
            }
        }

        /// <summary>
        /// Returns the value for the named column in the current row.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <returns>The column value or <c>null</c> if the column (or row) does not exist.</returns>
        public string GetColumn(string columnName)
        {
            int index;

            if (row == null)
            {
                return null;
            }

            if (!columnMap.TryGetValue(columnName, out index))
            {
                return null;
            }

            return row[index];
        }

        /// <summary>
        /// Indexer that returns the value for the named column in the current row.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <returns>The column value or <c>null</c> if the column (or row) does not exist.</returns>
        public string this[string columnName]
        {
            get { return GetColumn(columnName); }
        }

        /// <summary>
        /// Indexer that returns the value for a column.
        /// </summary>
        /// <param name="column">The column index.</param>
        /// <returns>The column value or <c>null</c> if the column (or row) does not exist.</returns>
        public string this[int column]
        {
            get
            {
                if (row == null || row.Count <= column)
                {
                    return null;
                }
                else
                {
                    return row[column];
                }
            }
        }

        /// <summary>
        /// Determines whether a cell in a named column in the current row is empty or
        /// if the column does not exist.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <returns><c>true</c> if the cell is empty or the named column is not present.</returns>
        public bool IsEmpty(string columnName)
        {
            return string.IsNullOrWhiteSpace(GetColumn(columnName));
        }
    }
}
