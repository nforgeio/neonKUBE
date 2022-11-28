//-----------------------------------------------------------------------------
// FILE:	    SetupConsoleWriter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Used internally to update .NET console window without flickering.
    /// </summary>
    public class SetupConsoleWriter
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Set to <c>true</c> when the current process has a console.
        /// </summary>
        private static readonly bool HasConsole;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SetupConsoleWriter()
        {
            // Detect whether the current process has a console.  It looks like
            // the only way to do this is to perform a console operation and 
            // catch the exception thrown when there's no console.

            try
            {
                var pos = Console.GetCursorPosition();

                Console.SetCursorPosition(pos.Left, pos.Top);
                
                HasConsole = true;
            }
            catch
            {
                HasConsole = false;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private object          syncLock      = new object();
        private string          previousText  = null;
        private List<string>    previousLines = new List<string>();
        private bool            stopped       = false;

        /// <summary>
        /// Writes the text passed to the <see cref="Console"/> without flickering.
        /// </summary>
        /// <param name="text">The text to be written.</param>
        public void Update(string text)
        {
            if (!HasConsole)
            {
                return;
            }

            // Hide the cursor while updating.

            Console.CursorVisible = false;

            lock (syncLock)
            {
                if (stopped)
                {
                    return;
                }

                text ??= string.Empty;

                var newLines = text.Split('\n')
                    .Select(line => line.TrimEnd())
                    .ToList();

                if (previousText == null)
                {
                    // This is the first Update() has been called so we need to
                    // clear the console.

                    Console.Clear();
                }

                if (text == previousText)
                {
                    return;     // The text hasn't changed
                }

                // We're going to write the new lines by comparing them against the previous lines and rewriting
                // only the lines that are different. 

                for (int lineIndex = 0; lineIndex < Math.Max(previousLines.Count, newLines.Count); lineIndex++)
                {
                    var previousLine = lineIndex < previousLines.Count ? previousLines[lineIndex] : string.Empty;
                    var newLine      = lineIndex < newLines.Count ? newLines[lineIndex] : string.Empty;

                    // When the new line is shorter than the previous one, we need to append enough spaces
                    // to the new line such that the previous line will be completely overwritten.

                    if (newLine.Length < previousLine.Length)
                    {
                        newLine += new string(' ', previousLine.Length - newLine.Length);
                    }

                    if (newLine != previousLine)
                    {
                        Console.SetCursorPosition(0, lineIndex);
                        Console.Write(newLine);
                    }
                }

                previousLines = newLines;
                previousText  = text;
            }
        }

        /// <summary>
        /// Disables <see cref="Update(string)"/> from writing any more updates to the console
        /// and restores the console for normal write operations.
        /// </summary>
        public void Stop()
        {
            lock (syncLock)
            {
                stopped = true;

                if (HasConsole)
                {
                    // Move the cursor to the beginning of the second line after the last
                    // non-blank line written by Update() and then re-enable the cursor
                    // such that the next Console write will happen there.

                    for (int lineIndex = previousLines.Count - 1; lineIndex >= 1; lineIndex--)
                    {
                        if (string.IsNullOrWhiteSpace(previousLines[lineIndex]))
                        {
                            previousLines.RemoveAt(lineIndex);
                        }
                        else
                        {
                            break;
                        }
                    }

                    Console.SetCursorPosition(0, previousLines.Count + 1);
                    Console.CursorVisible = true;
                }
            }
        }
    }
}
