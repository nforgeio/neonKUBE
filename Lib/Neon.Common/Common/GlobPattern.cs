//-----------------------------------------------------------------------------
// FILE:        GlobPattern.cs
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
using Neon.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Common
{
    /// <summary>
    /// Implements a very simple glob matcher inspired by the GitHub <c>.gitignore</c> patterns
    /// described <a href="https://git-scm.com/docs/gitignore">here</a>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The current implementation is somewhat limited compared to that for <c>.gitignore</c>:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// Only <b>"*"</b> and <b>"**"</b> wildcard chacacters are allowed.
    /// <b>"!"</b> and <b>"[..]"</b> are not recognized.
    /// </item>
    /// <item>
    /// <b>"*"</b> matches anything except for <b>"/"</b>.
    /// </item>
    /// <item>
    /// <b>"**"</b> matches zero or more directories.
    /// </item>
    /// </list>
    /// </remarks>
    public class GlobPattern
    {
        //---------------------------------------------------------------------
        // Private types

        private struct Segment
        {
            public string LeadingSlash;
            public string Text;

            public Segment(string leadingSlash, string text)
            {
                this.LeadingSlash = leadingSlash ?? string.Empty;
                this.Text         = text;
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses a <see cref="GlobPattern"/> from a pattern string.
        /// </summary>
        /// <param name="pattern">The pattern.</param>
        /// <returns>The created <see cref="GlobPattern"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the pattern is <c>null</c> or empty.</exception>
        /// <exception cref="FormatException">Thrown if the pattern is invalid.</exception>
        public static GlobPattern Parse(string pattern)
        {
            Covenant.Requires<ArgumentNullException>(pattern != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(pattern.Trim()));
            Covenant.Requires<FormatException>(!pattern.Contains("//"));

            return new GlobPattern(pattern);
        }

        /// <summary>
        /// Attempts to parse a <see cref="GlobPattern"/>.
        /// </summary>
        /// <param name="pattern">The pattern string.</param>
        /// <param name="globPattern">Returns as the parsed <see cref="GlobPattern"/>.</param>
        /// <returns><c>true</c> if the pattern was parsed successfully.</returns>
        public static bool TryParse(string pattern, out GlobPattern globPattern)
        {
            // $todo(jeff.lill):
            //
            // Catching the exceptions here is a bit of a hack.  I should reverse
            // the Create() and TryParse() implementations so that Create()
            // depends on TryParse().

            try
            {
                globPattern = Parse(pattern);

                return true;
            }
            catch
            {
                globPattern = null;
                return false;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private string  pattern;
        private string  regexPattern;
        private Regex   regex;

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="pattern">The glob pattern.</param>
        private GlobPattern(string pattern)
        {
            this.pattern = pattern = pattern.Trim();

            // Split the pattern into segments separated by forward slashes.

            var segments = new List<Segment>();
            var pos      = 0;

            while (pos < pattern.Length)
            {
                int     posNext = pattern.IndexOf('/', pos + 1);
                string  slash   = string.Empty;
                string  text;

                if (pattern[pos] == '/')
                {
                    slash = "/";

                    if (posNext == -1)
                    {
                        text = pattern.Substring(pos + 1);
                        pos  = pattern.Length;
                    }
                    else
                    {
                        text = pattern.Substring(pos + 1, posNext - (pos + 1));
                        pos  = posNext;
                    }
                }
                else
                {
                    if (posNext == -1)
                    {
                        text = pattern.Substring(pos);
                        pos  = pattern.Length;
                    }
                    else
                    {
                        text = pattern.Substring(pos, posNext - pos);
                        pos  = posNext;
                    }
                }

                segments.Add(new Segment(slash, text));
            }

            // Convert the glob into a regular expression.

            var sbRegex  = new StringBuilder();

            // Ensure that segments don't include the "**" wildcard
            // along with other content.

            foreach (var segment in segments)
            {
                if (segment.Text.Contains("**") && segment.Text.Length > 2)
                {
                    throw new FormatException($"Glob [{pattern}] is invalid because it includes a segment with [**] along with other text.");
                }
            }

            sbRegex.Append('^');

            for (int i = 0; i < segments.Count;i++)
            {
                var segment = segments[i];
                var isFirst = i == 0;
                var isLast  = i == segments.Count - 1;

                if (segment.Text == "**")
                {
                    if (isFirst)
                    {
                        sbRegex.Append(segment.LeadingSlash);
                    }

                    if (isLast)
                    {
                        sbRegex.Append("(([^/]*)/?)*");
                    }
                    else
                    {
                        sbRegex.Append("(([^/]*)/)*");
                    }
                }
                else
                {
                    // Append the leading slash unless the previous
                    // segment was a "**".

                    if (isFirst || segments[i - 1].Text != "**")
                    {
                        sbRegex.Append(segment.LeadingSlash);
                    }

                    foreach (var ch in segment.Text)
                    {
                        switch (ch)
                        {
                            case '*':

                                sbRegex.Append("[^/]*");
                                break;

                            case '?':
                            case '[':
                            case '^':
                            case '$':
                            case '|':
                            case '+':
                            case '(':
                            case ')':
                            case '.':

                                // These characters need to be escaped.

                                sbRegex.Append('\\');
                                sbRegex.Append(ch);
                                break;

                            default:

                                sbRegex.Append(ch);
                                break;
                        }
                    }
                }
            }

            sbRegex.Append('$');

            regexPattern = sbRegex.ToString();
        }

        /// <summary>
        /// Returns the glob as a regular expression string.
        /// </summary>
        public string RegexPattern
        {
            get { return regexPattern; }
        }

        /// <summary>
        /// Returns the <see cref="Regex"/> that can be used to match strings against the glob.
        /// </summary>
        public Regex Regex
        {
            get
            {
                if (regex != null)
                {
                    return regex;
                }

                return regex = new Regex(regexPattern);
            }
        }

        /// <summary>
        /// Matches a string against the glob.
        /// </summary>
        /// <param name="input">The value to be matched.</param>
        /// <returns><c>true</c> if the parameter matches the glob.</returns>
        public bool IsMatch(string input)
        {
            Covenant.Requires<ArgumentNullException>(input != null);

            return Regex.IsMatch(input);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return pattern;
        }
    }
}
