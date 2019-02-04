//-----------------------------------------------------------------------------
// FILE:	    CsvTableWriter.cs
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
    /// Used to generate a CSV table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used to generate a CSV table with column headers with the class
    /// handling the mapping of column names to columns.  The class is easy to use.  Simply
    /// construct an instance, passing an array of case insensitive table column header names
    /// and then write table rows by calling <b>Set()</b> methods to set row cell values
    /// and then <see cref="WriteRow" /> to write each row to the output.
    /// </para>
    /// </remarks>
    public class CsvTableWriter : IDisposable
    {
        private CsvWriter                   writer;     // The CSV writer
        private Dictionary<string, int>     columnMap;  // Maps case insensitive column names into column indicies
        private string[]                    row;        // The current row

        /// <summary>
        /// Constructs an instance to write to a <see cref="CsvWriter" />.
        /// </summary>
        /// <param name="columnHeaders">The table column names.</param>
        /// <param name="writer">The <see cref="CsvWriter" /> to write to.</param>
        /// <remarks>
        /// <note>
        /// This method writes the column headers passed to the writer so the
        /// application can begin writing rows of data.
        /// </note>
        /// </remarks>
        public CsvTableWriter(string[] columnHeaders, CsvWriter writer)
        {
            this.writer    = writer;
            this.columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            this.row       = new string[columnHeaders.Length];

            // Initialize the column map and then write the column headers.

            for (int i = 0; i < columnHeaders.Length; i++)
            {
                if (columnHeaders[i] != null && !columnMap.ContainsKey(columnHeaders[i]))
                {
                    columnMap.Add(columnHeaders[i], i);
                    Set(columnHeaders[i], columnHeaders[i]);
                }
            }

            WriteRow();
        }

        /// <summary>
        /// Constructs an instance to write to a <see cref="TextWriter" />.
        /// </summary>
        /// <param name="columnHeaders">The table column names.</param>
        /// <param name="writer">The writer.</param>
        /// <remarks>
        /// <note>
        /// This method writes the column headers passed to the writer so the
        /// application can begin writing rows of data.
        /// </note>
        /// </remarks>
        public CsvTableWriter(string[] columnHeaders, TextWriter writer)
            : this(columnHeaders, new CsvWriter(writer))
        {
        }

        /// <summary>
        /// Constructs an instance to write to a stream.
        /// </summary>
        /// <param name="columnHeaders">The table column names.</param>
        /// <param name="stream">The output stream.</param>
        /// <param name="encoding">The file's character <see cref="Encoding" />.</param>
        /// <remarks>
        /// <note>
        /// This method writes the column headers passed to the writer so the
        /// application can begin writing rows of data.
        /// </note>
        /// </remarks>
        public CsvTableWriter(string[] columnHeaders, Stream stream, Encoding encoding)
            : this(columnHeaders, new CsvWriter(stream, encoding))
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
            if (writer != null)
            {
                writer.Close();
                writer = null;
            }
        }

        /// <summary>
        /// Returns the underlying <see cref="CsvWriter" /> or <c>null</c> if the writer is closed.
        /// </summary>
        public CsvWriter Writer
        {
            get { return writer; }
        }

        /// <summary>
        /// Returns the dictionary that case insensitvely maps a column name to 
        /// the zero base index of the column.
        /// </summary>
        public Dictionary<string, int> ColumnMap
        {
            get { return columnMap; }
        }

        /// <summary>
        /// Returns the zero-based index of the specified column.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <returns>The index of the column or <b>-1</b> if the column does not exist.</returns>
        public int GetColumnIndex(string columnName)
        {
            int index;

            if (columnMap.TryGetValue(columnName, out index))
            {
                return index;
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// Returns the current row array.
        /// </summary>
        public string[] Row
        {
            get { return row; }
        }

        /// <summary>
        /// Writes the current row of data to the output and then clears the row
        /// so the application can begin setting the next row. 
        /// </summary>
        public void WriteRow()
        {
            writer.WriteLine(row);

            for (int i = 0; i < row.Length; i++)
            {
                row[i] = null;
            }
        }

        /// <summary>
        /// Sets the value of a named table column in the current row.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="value">The column value.</param>
        /// <remarks>
        /// <note>
        /// This method will do nothing if the <paramref name="columnName" /> 
        /// passed does not map to a table column.
        /// </note>
        /// </remarks>
        public void Set(string columnName, string value)
        {
            int index;

            if (columnMap.TryGetValue(columnName, out index))
            {
                row[index] = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Sets the value of a named table column in the current row.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="value">The column value.</param>
        /// <remarks>
        /// <note>
        /// This method will do nothing if the <paramref name="columnName" /> 
        /// passed does not map to a table column.
        /// </note>
        /// </remarks>
        public void Set(string columnName, object value)
        {
            Set(columnName, value != null ? value.ToString() : string.Empty);
        }
    }
}
