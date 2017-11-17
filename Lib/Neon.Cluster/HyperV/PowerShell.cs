//-----------------------------------------------------------------------------
// FILE:	    PowerShell.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;

namespace Neon.Cluster.HyperV
{
    /// <summary>
    /// <para>
    /// Internal proxy for executing PowerShell commands on Windows machines.
    /// </para>
    /// <note>
    /// This class requires elevated administrative rights.
    /// </note>
    /// </summary>
    internal class PowerShell
    {
        //---------------------------------------------------------------------
        // Private types

        private class Column
        {
            /// <summary>
            /// Column title.
            /// </summary>
            public string Title;

            /// <summary>
            /// Index of the starting position for the column data.
            /// </summary>
            public int Start;

            /// <summary>
            /// Column width (number of characters).
            /// </summary>
            public int Width;
        }

        //---------------------------------------------------------------------
        // Implementation

        private const int PowershellBufferWidth = 16192;

        /// <summary>
        /// Default constructor to be used to execute local PowerShell commands.
        /// </summary>
        public PowerShell()
        {
            if (!NeonHelper.IsWindows)
            {
                throw new NotSupportedException($"{nameof(HyperVClient)} is only supported on Windows.");
            }
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            // Nothing to dispose for the current implementation.
        }

        /// <summary>
        /// Expands any environment variables of the form <b>${NAME}</b> in the input
        /// string and returns the expanded result.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The expanded output string.</returns>
        private string ExpandEnvironmentVars(string input)
        {
            Covenant.Requires<ArgumentNullException>(input != null);

            using (var reader = new PreprocessReader(input))
            {
                reader.VariableExpansionRegex = PreprocessReader.CurlyVariableExpansionRegex;

                // Load the environment variables.

                foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
                {
                    // $hack(jeff.lill):
                    //
                    // Some common Windows enmvironment variables names include characters
                    // like parens that are not compatible with PreprocessReader.  We're
                    // just going to catch the exceptions and ignore these.

                    var key = (string)item.Key;

                    if (PreprocessReader.VariableValidationRegex.IsMatch(key))
                    {
                        reader.Set(key, (string)item.Value);
                    }
                }

                // Perform the substitutions.

                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Executes a PowerShell command that returns a simple string result.
        /// </summary>
        /// <param name="command">The command string.</param>
        /// <param name="noEnvironmentVars">
        /// Optionally disables that environment variable subsitution (defaults to <c>false</c>).
        /// </param>
        /// <returns>The list of <c>dynamic</c> objects parsed from the command response.</returns>
        /// <exception cref="HyperVException">Thrown if the command failed.</exception>
        public string Execute(string command, bool noEnvironmentVars = false)
        {
            var result = NeonHelper.ExecuteCaptureStreams("powershell.exe", $"{command} | Out-String -Width {PowershellBufferWidth}");

            if (result.ExitCode != 0)
            {
                throw new HyperVException(result.AllText);
            }

            return result.AllText;
        }

        /// <summary>
        /// Executes a PowerShell command that returns a result table, subsituting any
        /// environment variable references of the form <b>${NAME}</b> and returning a list 
        /// of <c>dynamic</c> objects parsed from the table with the object property
        /// names set to the table column names and the values parsed as strings.
        /// </summary>
        /// <param name="command">The command string.</param>
        /// <param name="noEnvironmentVars">
        /// Optionally disables that environment variable subsitution (defaults to <c>false</c>).
        /// </param>
        /// <returns>The list of <c>dynamic</c> objects parsed from the command response.</returns>
        /// <exception cref="HyperVException">Thrown if the command failed.</exception>
        public List<dynamic> ExecuteTable(string command, bool noEnvironmentVars = false)
        {
            Covenant.Requires<ArgumentNullException>(command != null);

            if (!noEnvironmentVars)
            {
                command = ExpandEnvironmentVars(command);

                // $hack(jeff.lill):
                //
                // ExpandEnvironmentVars() appends a CRLF to the end of the 
                // string, so we'll remove that here.

                command = command.TrimEnd();
            }

            var result = NeonHelper.ExecuteCaptureStreams("powershell.exe", $"{command} | Out-String -Width {PowershellBufferWidth} | Format-Table");

            if (result.ExitCode != 0)
            {
                throw new HyperVException(result.AllText);
            }

            // Parse the output text as a table.

            using (var reader = new StringReader(result.OutputText))
            {
                var columnTitles     = reader.ReadLine();   // Line with the column titles
                var columnUnderlines = reader.ReadLine();   // Line with the "----" underlines
                var columns          = new List<Column>();

                // Parse the underlines to determine the column positions.

                var pos = 0;

                while (pos < columnUnderlines.Length)
                {
                    var column = new Column();

                    column.Start = pos;

                    // Skip over the underlines for the current column.

                    while (pos < columnUnderlines.Length && columnUnderlines[pos] == '-')
                    {
                        pos++;
                    }

                    // Skip over the whitespace between the underlines.

                    while (pos < columnUnderlines.Length && columnUnderlines[pos] == ' ')
                    {
                        pos++;
                    }

                    column.Width = pos - column.Start;

                    if (pos < columnUnderlines.Length)
                    {
                        // Reduce the column width by one if this isn't the last column
                        // to ignore the space seperating columns.

                        column.Width--;
                    }

                    columns.Add(column);
                }

                // Extract the column titles.

                foreach (var column in columns)
                {
                    column.Title = columnTitles.Substring(column.Start, column.Width).Trim();
                }

                // Parse the data lines.

                var items = new List<dynamic>();

                for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    if (line.Length == 0)
                    {
                        continue;   // Ignore blank lines.
                    }

                    var item           = new ExpandoObject();
                    var itemDictionary = (IDictionary<string, object>)item;

                    foreach (var column in columns)
                    {
                        itemDictionary.Add(column.Title, line.Substring(column.Start, column.Width).TrimEnd());
                    }

                    items.Add(item);
                }

                return items;
            }
        }
    }
}
