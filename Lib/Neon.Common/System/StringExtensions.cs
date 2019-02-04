//-----------------------------------------------------------------------------
// FILE:	    StringExtensions.cs
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
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    /// <summary>
    /// String extension methods.
    /// </summary>
    public static class StringExtensions
    {
        private static readonly string[] empty = new string[0];

        /// <summary>
        /// Splits the string into lines of text.
        /// </summary>
        /// <param name="value">The string value.</param>
        /// <returns>
        /// An <see cref="IEnumerable{String}"/> with the extracted lines.  Note
        /// that an empty string will return a single empty line and a <c>null</c>
        /// string will return no lines.
        /// </returns>
        public static IEnumerable<string> ToLines(this string value)
        {
            if (value == null)
            {
                return empty;
            }
            else if (value.Length == 0)
            {
                return new string[] { string.Empty };
            }
            else
            {
                var lineCount = 1;

                foreach (var ch in value)
                {
                    if (ch == '\n')
                    {
                        lineCount++;
                    }
                }

                var list = new List<string>(lineCount);

                using (var reader = new StringReader(value))
                {
                    foreach (var line in reader.Lines())
                    {
                        list.Add(line);
                    }
                }

                return list;
            }
        }
    }
}
