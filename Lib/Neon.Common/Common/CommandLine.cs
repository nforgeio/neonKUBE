//-----------------------------------------------------------------------------
// FILE:	    CommandLine.cs
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
using System.Text;
using System.Threading.Tasks;

// $todo(jeff.lill):
//
// Modify this to work more like the Linux standard, where:
//
//      * Options can have a short form (-f) and a long form (--file)
//
//      * Declare options and their default values in advance so the
//        class can detect unknown options.

namespace Neon.Common
{
    /// <summary>
    /// Performs common operations on application a DOS or Linux command line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Command lines may consist of zero or more items.  Items that begin with
    /// the dash (-) character are considered to be command line options.  Items that
    /// begin with an at sign (@) can be considered to be response files, and all
    /// other items are considered to be command line arguments.
    /// </para>
    /// <note>
    /// The "<b>-</b>" and "<b>--</b>" items (without an option name are considered 
    /// arguments, not options.
    /// </note>
    /// <para>
    /// The static <see cref="ExpandFiles" /> method can be used to process
    /// response files specified in a command line.  Response files
    /// are specified by prepending a '@' character to the name of a text
    /// file and then treating each line of the file as a command line item.
    /// </para>
    /// <para>
    /// The static <see cref="ExpandWildcards" /> method can be used to 
    /// expand file names with wildcard characters into the set of actual 
    /// files that match the pattern.
    /// </para>
    /// <para>
    /// The <see cref="CommandLine" /> class can also handles parsing of items
    /// as command line options.
    /// </para>
    /// <code language="none">
    /// 
    ///     -&lt;option name&gt;[=&lt;value&gt;]
    /// 
    /// </code>
    /// <para>
    /// will be parsed into name/value pairs and will be available for
    /// lookup via the string keyed indexer.  Options that specify no
    /// value will be assigned an empty string value.
    /// </para>
    /// <note>
    /// Command line option names are case sensitive.
    /// </note>
    /// <para>
    /// The class will also make all command line items available via the
    /// integer keyed indexer which will return items based on
    /// their position on the command line and also via the <see cref="Items" />
    /// property.  Command line items that are not command, are available via
    /// the <see cref="Arguments" /> property.  Options can be looked up via 
    /// the <see cref="GetOption(string, string)"/> and <see cref="GetOptionValues(string)"/>
    /// overrides.
    /// </para>
    /// <para>
    /// <see cref="CommandLine"/> also supports the definition of long and
    /// short forms of options with optional default values using the
    /// <see cref="DefineOption"/> method.  This associates one or more
    /// option names with an optional default value.
    /// </para>
    /// <para>
    /// You can use this easily implement the short and long forms 
    /// of options as well as to centralize the specification of 
    /// option default values.
    /// </para>
    /// <code language="C#">
    /// var commandLine = new CommandLine(args);
    /// 
    /// commandLine.DefineOption("-q", "--quiet");
    /// commandLine.DefineOption("-k", "--key").Default = "none";
    /// 
    /// // These calls both return the option value for "-q" or "--quiet".
    /// // Since no default value was set, the default value will be the
    /// // empty string.
    /// 
    /// commandLine.GetOption("-q");
    /// commandLine.GetOption("--quiet");
    /// 
    /// // These calls both return the option value for "-k" or "--key".
    /// // The default value will be "none".
    /// 
    /// commandLine.GetOption("-k");
    /// commandLine.GetOption("--key");
    /// </code>
    /// <note>
    /// This class assumes that the position of command line options doesn't
    /// matter, which is somewhat simplistic.  In particular, the <see cref="Shift(int, string)"/> 
    /// method actually relocates all of the options to the beginning of the 
    /// shifted command line.
    /// </note>
    /// </remarks>
    public sealed class CommandLine
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Associates one or more option names with a default value.
        /// </summary>
        public class OptionDefinition
        {
            private string def;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="names">The associated option names.</param>
            public OptionDefinition(IEnumerable<string> names)
            {
                Covenant.Requires<ArgumentNullException>(names != null);

                Names = names.ToArray();

                if (Names.Length == 0)
                {
                    throw new ArgumentException("At least one name must be specified.", nameof(names));
                }

                Default = string.Empty;
            }

            /// <summary>
            /// Returns the array of associated option names.
            /// </summary>
            public string[] Names { get; private set; }

            /// <summary>
            /// The option's default value or the empty string.
            /// </summary>
            public string Default
            {
                get { return def; }

                set
                {
                    Covenant.Requires<ArgumentNullException>(value != null);

                    def = value;
                }
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses the argument string passed into a <see cref="CommandLine" />
        /// instance, dealing with quoted parameters, etc.
        /// </summary>
        /// <param name="input">The argument string.</param>
        public static CommandLine Parse(string input)
        {
            Covenant.Requires<ArgumentNullException>(input != null);

            List<string>    items   = new List<string>();
            char[]          wsChars = new char[] { ' ', '\t' };
            int             p, pEnd;

            p = 0;
            while (true)
            {
                // Advance past any whitespace

                while (p < input.Length && Char.IsWhiteSpace(input[p]))
                {
                    p++;
                }

                if (p == input.Length)
                {
                    break;
                }

                if (input[p] == '"')
                {
                    pEnd = input.IndexOf('"', p + 1);
                    if (pEnd == -1)
                    {
                        // Unbalanced quote

                        items.Add(input.Substring(p + 1).Trim());
                        break;
                    }

                    p++;
                    items.Add(input.Substring(p, pEnd - p));
                    p = pEnd + 1;
                }
                else
                {
                    pEnd = input.IndexOfAny(wsChars, p);
                    if (pEnd == -1)
                    {

                        items.Add(input.Substring(p).Trim());
                        break;
                    }

                    items.Add(input.Substring(p, pEnd - p).Trim());
                    p = pEnd + 1;
                }
            }

            return new CommandLine(items.ToArray());
        }

        /// <summary>
        /// Expands the command line by processing items beginning with '@' as input files.
        /// </summary>
        /// <returns>The set of expanded items.</returns>
        /// <remarks>
        /// <para>
        /// Command line items will be assumed to specify a
        /// text file name after the '@'.  This file will be read
        /// and each non-empty line of text will be inserted as a
        /// command line parameter.
        /// </para>
        /// <para>
        /// Lines of text whose first non-whitespace character is a
        /// pound sign (#) will be ignored as comments.
        /// </para>
        /// <para>
        /// Command line parameters may also span multiple lines by
        /// beginning the parameter with a line of text begininning with
        /// "{{" and finishing it with a line of text containing "}}".
        /// In this case, the command line parameter will be set to the
        /// text between the {{...}} with any CRLF sequences replaced by
        /// a single space.
        /// </para>
        /// <para>
        /// Here's an example:
        /// </para>
        /// <code language="none">
        /// # This is a comment and will be ignored
        /// 
        /// -param1=aaa
        /// -param2=bbb
        /// {{
        /// -param3=hello
        /// world
        /// }}
        /// </code>
        /// <para>
        /// This will be parsed as three command line parameters:
        /// <b>-param1=aaa</b>, <b>-param2=bbb</b>, and <b>-param3=hello world</b>
        /// </para>
        /// </remarks>
        /// <exception cref="IOException">Thrown if there's a problem opening an "@" input file.</exception>
        /// <exception cref="FormatException">Thrown if there's an error parsing an "@" input file.</exception>
        public static string[] ExpandFiles(string[] args)
        {
            Covenant.Requires<ArgumentNullException>(args != null);

            var list = new List<string>();

            foreach (string arg in args)
            {
                if (!arg.StartsWith("@"))
                {
                    list.Add(arg);
                    continue;
                }

                string          path   = arg.Substring(1);
                StreamReader    reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read));
                string          line;

                try
                {
                    for (line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        line = line.Trim();
                        if (line == string.Empty || line[0] == '#')
                        {
                            continue;   // Ignore empty lines and comments
                        }

                        if (line.StartsWith("{{"))
                        {

                            var sb = new StringBuilder(256);

                            // The text up to the next line beginning with "}}" is the next parameter.

                            while (true)
                            {
                                line = reader.ReadLine();
                                if (line == null)
                                    throw new FormatException(string.Format("Command line file [{0}] has an unclosed \"{{{{\" section.", path));

                                line = line.Trim();
                                if (line.StartsWith("}}"))
                                    break;

                                sb.Append(line);
                                sb.Append(' ');
                            }

                            line = sb.ToString().Trim();

                            if (line == string.Empty)
                            {
                                continue;
                            }
                        }

                        list.Add(line);
                    }
                }
                finally
                {
                    reader.Dispose();
                }
            }

            args = new String[list.Count];
            list.CopyTo(0, args, 0, list.Count);

            return args;
        }

        /// <summary>
        /// Checks the argument passed for wildcards and expands them into the
        /// appopriate set of matching file names.
        /// </summary>
        /// <param name="path">The file path potentially including wildcards.</param>
        /// <returns>The set of matching file names.</returns>
        public static string[] ExpandWildcards(string path)
        {
            Covenant.Requires<ArgumentNullException>(path != null);

            int         pos;
            string      dir;
            string      pattern;

            if (path.IndexOfAny(NeonHelper.FileWildcards) == -1)
            {
                return new string[] { path };
            }

            pos = path.LastIndexOfAny(new char[] { '\\', '/', ':' });
            if (pos == -1)
            {
                return Directory.GetFiles(".", path);
            }

            dir     = path.Substring(0, pos);
            pattern = path.Substring(pos + 1);

            return Directory.GetFiles(dir, pattern);
        }

        /// <summary>
        /// Formats an array of objects into a form suitable for passing to a 
        /// process on the command line by adding double quotes around any values
        /// with embedded spaces.
        /// </summary>
        /// <param name="args">The arguments to be formatted.</param>
        /// <returns>the formatted string.</returns>
        /// <exception cref="FormatException">Thrown if any of the arguments contain double quote or any other invalid characters.</exception>
        public static string Format(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(args != null);

            var sb = new StringBuilder();

            for (int i = 0; i < args.Length; i++)
            {
                string  v     = args[i].ToString();
                bool    space = false;

                foreach (char ch in v)
                {
                    if (ch == ' ')
                    {
                        space = true;
                    }
                    else if (ch < ' ' || ch == '"')
                    {
                        throw new FormatException(string.Format("Illegal character [code={0}] in command line argument.", (int)ch));
                    }
                }

                if (i > 0)
                {
                    sb.Append(' ');
                }

                if (space)
                {
                    sb.Append('"');
                    sb.Append(v);
                    sb.Append('"');
                }
                else
                {
                    sb.Append(v);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts an array of objects to an array of strings.
        /// </summary>
        private static string[] ToStrings(object[] args)
        {
            Covenant.Requires<ArgumentNullException>(args != null);

            string[] output = new string[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                output[i] = args[i].ToString();
            }

            return output;
        }

        //---------------------------------------------------------------------
        // Instance members

        private Dictionary<string, OptionDefinition>    optionDefinitions = new Dictionary<string, OptionDefinition>();
        private Dictionary<string, string>              options;
        private string[]                                items;
        private string[]                                arguments;

        /// <summary>
        /// Constructs an instance optionally expanding any response file specified
        /// in the arguments passed.
        /// </summary>
        /// <param name="args">The optional command line arguments.</param>
        public CommandLine(object[] args = null)
        {
            if (args == null)
            {
                args = new object[0];
            }

            List<string>    valueList = new List<string>();
            string          name;
            string          value;
            int             p;

            items   = ToStrings(args);
            options = new Dictionary<string, string>();

            for (int i = 0; i < this.items.Length; i++)
            {
                var arg = this.items[i];

                if (!arg.StartsWith("-") || arg == "-" || arg == "--")
                {
                    valueList.Add(arg);
                    continue;
                }

                p = arg.IndexOf('=');
                if (p == -1)
                {
                    name  = arg;
                    value = string.Empty;
                }
                else
                {
                    name  = arg.Substring(0, p);
                    value = arg.Substring(p + 1);
                }

                name = name.Trim();
                if (name == string.Empty)
                {
                    continue;
                }

                options[name] = value;
            }

            arguments = valueList.ToArray();
        }

        /// <summary>
        /// Adds an option definition to the command line and returns the
        /// definition so its default value may be set if desired.
        /// </summary>
        /// <param name="names">The option names (e.g. the short and long form).</param>
        /// <returns>The <see cref="OptionDefinition"/>.</returns>
        /// <remarks>
        /// <para>
        /// You can use this easily implement the short and long forms 
        /// of options as well as to centralize the specification of 
        /// option default values.
        /// </para>
        /// <code language="C#">
        /// var commandLine = new CommandLine(args);
        /// 
        /// commandLine.DefineOption("-q", "--quiet");
        /// commandLine.DefineOption("-k", "--key").Default = "none";
        /// 
        /// // These calls both return the option value for "-q" or "--quiet".
        /// // Since no default value was set, the default value will be the
        /// // empty string.
        /// 
        /// commandLine.GetOption("-q");
        /// commandLine.GetOption("--quiet");
        /// 
        /// // These calls both return the option value for "-k" or "--key".
        /// // The default value will be "none".
        /// 
        /// commandLine.GetOption("-k");
        /// commandLine.GetOption("--key");
        /// </code>
        /// </remarks>
        public OptionDefinition DefineOption(params string[] names)
        {
            Covenant.Requires<ArgumentNullException>(names != null);
            Covenant.Requires<ArgumentException>(names.Length > 0);

            var definition = new OptionDefinition(names);

            foreach (var name in names)
            {
                optionDefinitions.Add(name, definition);
            }

            return definition;
        }

        /// <summary>
        /// Returns the array of command line arguments (including both
        /// command line options and values).
        /// </summary>
        public string[] Items
        {
            get { return items; }
        }

        /// <summary>
        /// Returns the array of command line values (items that are not
        /// command line options).
        /// </summary>
        public string[] Arguments
        {
            get { return arguments; }
        }

        /// <summary>
        /// Enumerates the command line arguments beginning at the specified index.
        /// </summary>
        /// <param name="startIndex">The index of the first argument to be returned.</param>
        /// <returns>The enumerated arguments.</returns>
        public IEnumerable<string> GetArguments(int startIndex = 0)
        {
            Covenant.Requires<ArgumentException>(startIndex >= 0);

            for (int i = startIndex; i < Arguments.Length; i++)
            {
                yield return Arguments[i];
            }
        }

        /// <summary>
        /// Returns an item from the command line based on its position.
        /// </summary>
        /// <param name="index">The zero-based position of the desired argument.</param>
        /// <returns>The argument string.</returns>
        public string this[int index]
        {
            get { return this.items[index]; }
        }

        /// <summary>
        /// Returns the value associated with a command line option if the option was present 
        /// on the command line otherwise, the specified default value will be returned.
        /// </summary>
        /// <param name="optionName">The case sensitive option name (including the leading dashes (<b>-</b>).</param>
        /// <param name="def">The default value.</param>
        /// <returns>The option value if present, the specified default value otherwise.</returns>
        /// <remarks>
        /// <para>
        /// If the <paramref name="optionName"/> was included in a previous <see cref="DefineOption"/>
        /// call, then all aliases for the option will be searched.  If the option is not
        /// present on the command line and <paramref name="def"/> is <c>null</c>, then the default
        /// defined default value will be returned otherwise <paramref name="def"/> will override
        /// the definition.
        /// </para>
        /// </remarks>
        public string GetOption(string optionName, string def = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(optionName));

            OptionDefinition    definition;
            string              value;

            if (optionDefinitions.TryGetValue(optionName, out definition))
            {
                foreach (var name in definition.Names)
                {
                    if (options.TryGetValue(name, out value) && !string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }

                if (def != null)
                {
                    return def;
                }
                else
                {
                    return definition.Default;
                }
            }
            else
            {
                if (options.TryGetValue(optionName, out value))
                {
                    return value;
                }

                return def;
            }
        }

        /// <summary>
        /// Determines whether an option is present on the command line.
        /// </summary>
        /// <param name="optionName">The case sensitive option name (including the leading dashes (<b>-</b>).</param>
        /// <returns>The option value if present, the specified default value otherwise.</returns>
        public bool GetFlag(string optionName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(optionName));

            OptionDefinition    definition;
            string              value;

            if (options.TryGetValue(optionName, out value))
            {
                return true;
            }

            if (optionDefinitions.TryGetValue(optionName, out definition))
            {
                foreach (var name in definition.Names)
                {
                    if (options.TryGetValue(name, out value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the command line options as a dictionary of option name/value tuples.
        /// </summary>
        public Dictionary<string, string> Options
        {
            get { return options; }
        }

        /// <summary>
        /// Determines if an option was present on the command line.
        /// </summary>
        /// <param name="optionName">The case sensitive option name (including the leading dashes (<b>-</b>).</param>
        /// <returns><c>true</c> if the option is present.</returns>
        /// <remarks>
        /// <para>
        /// If the <paramref name="optionName"/> was included in a previous <see cref="DefineOption"/>
        /// call, then all aliases for the option will be searched.
        /// </para>
        /// </remarks>
        public bool HasOption(string optionName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(optionName));

            OptionDefinition    definition;

            if (optionDefinitions.TryGetValue(optionName, out definition))
            {
                foreach (var name in definition.Names)
                {
                    if (options.ContainsKey(name))
                    {
                        return true;
                    }
                }

                return false;
            }
            else
            {
                return options.ContainsKey(optionName);
            }
        }

        /// <summary>
        /// Returns all of the values a command line option that appears multiple
        /// times in the command.
        /// </summary>
        /// <param name="optionName">The case sensitive option name (including the leading dashes (<b>-</b>).</param>
        /// <returns>The array of values found sorted in the same order thney appear in the command line.</returns>
        /// <remarks>
        /// <note>
        /// Only command line options that actually specify a value using the
        /// colon (=) syntax are returned by this method.
        /// </note>
        /// <para>
        /// If the <paramref name="optionName"/> was included in a previous <see cref="DefineOption"/>
        /// call, then all aliases for the option will be searched.
        /// </para>
        /// </remarks>
        public string[] GetOptionValues(string optionName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(optionName));

            var values = new List<string>();

            OptionDefinition    definition;

            if (optionDefinitions.TryGetValue(optionName, out definition))
            {
                var prefixes = new HashSet<string>();

                foreach (var name in definition.Names)
                {
                    prefixes.Add(name);
                }

                foreach (var arg in items)
                {
                    if (arg.StartsWith("-"))
                    {
                        var pos = arg.IndexOf('=');

                        if (pos > 0)
                        {
                            var prefix = arg.Substring(0, pos);

                            if (prefixes.Contains(prefix))
                            {
                                values.Add(arg.Substring(pos + 1));
                            }
                        }
                    }
                }
            }
            else
            {
                var prefix = optionName + "=";

                foreach (var arg in items)
                {
                    if (arg.StartsWith(prefix))
                    {
                        values.Add(arg.Substring(prefix.Length));
                    }
                }
            }

            return values.ToArray();
        }

        /// <summary>
        /// Determines if the <b>--help</b> command line option is present.
        /// </summary>
        /// <returns><c>true</c> if the <b>--help</b> help option is present.</returns>
        public bool HasHelpOption
        {
            get { return options.ContainsKey("--help"); }
        }

        /// <summary>
        /// Returns a new <see cref="CommandLine" /> which includes all of the command line options
        /// and the arguments starting at the position passed to the end of the command line,
        /// essentially shifting arguments to the left.
        /// </summary>
        /// <param name="position">The index of the first argument to be included in the result.</param>
        /// <param name="splitter">
        /// The optional argument used to ensure that we're only shifting the left 
        /// side of a command line.  This defaults to <b>"--"</b> but may be set to
        /// <c>null</c> or the empty string to disable this behavior.
        /// </param>
        /// <returns>The new <see cref="CommandLine" />.</returns>
        public CommandLine Shift(int position, string splitter = "--")
        {
            Covenant.Requires<ArgumentException>(position >= 0);

            if (string.IsNullOrEmpty(splitter))
            {
                if (position < 0 || position > Arguments.Length)
                {
                    throw new ArgumentOutOfRangeException("position");
                }

                var items    = new List<object>();
                var argIndex = 0;

                foreach (var item in this.Items)
                {
                    if (item == splitter)
                    {
                        break;
                    }

                    if (item.StartsWith("-"))
                    {
                        items.Add(item);
                    }
                    else
                    {
                        if (argIndex >= position)
                        {
                            items.Add(item);
                        }

                        argIndex++;
                    }
                }

                return new CommandLine(items.ToArray());
            }
            else
            {
                var split            = this.Split(splitter);
                var leftCommandLine  = split.Left;
                var rightCommandLine = split.Right;

                leftCommandLine = leftCommandLine.Shift(position, splitter: null);

                if (rightCommandLine == null)
                {
                    return leftCommandLine;
                }

                var args = new List<string>();

                foreach (var item in leftCommandLine.Items)
                {
                    args.Add(item);
                }

                args.Add(splitter);

                foreach (var item in rightCommandLine.Items)
                {
                    args.Add(item);
                }

                return new CommandLine(args.ToArray());
            }
        }

        /// <summary>
        /// Splits the command line into two parts, the command line to the left of 
        /// the first specified item (defaults to <b>"--"</b>) and the command line 
        /// to the right of it.
        /// </summary>
        /// <param name="splitter">The split item (defaults to <b>"--"</b>).</param>
        /// <param name="addSplitterToRight">
        /// Optionally specifies that the split item should be included in the 
        /// right command line returned.
        /// </param>
        /// <returns>A tuple with <b>Left</b> and <b>Right</b> properties.</returns>
        /// <remarks>
        /// <note>
        /// The <b>Left</b> command line will return with a copy of the original option
        /// definitions.
        /// </note>
        /// <note>
        /// If there is no split item present, then <b>Right</b> will be <c>null</c>.
        /// </note>
        /// </remarks>
        public (CommandLine Left, CommandLine Right) Split(string splitter = "--", bool addSplitterToRight = false)
        {
            CommandLine     left;
            CommandLine     right;
            var             splitPos = -1;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == splitter)
                {
                    splitPos = i;
                    break;
                }
            }

            if (splitPos == -1)
            {
                left = new CommandLine(items);

                foreach (var item in optionDefinitions)
                {
                    left.optionDefinitions.Add(item.Key, item.Value);
                }

                return (Left: left, Right: null);
            }

            var leftItems  = new List<string>();
            var rightItems = new List<string>();

            for (int i = 0; i < items.Length; i++)
            {
                if (i < splitPos)
                {
                    leftItems.Add(items[i]);
                }
                else if (i > splitPos || addSplitterToRight)
                {
                    rightItems.Add(items[i]);
                }
            }

            left = new CommandLine(leftItems.ToArray());

            foreach (var item in optionDefinitions)
            {
                left.optionDefinitions.Add(item.Key, item.Value);
            }

            right = new CommandLine(rightItems.ToArray());

            return (Left: left, Right: right);
        }

        /// <summary>
        /// Determines whether the command line starts with the specified arguments.
        /// </summary>
        /// <param name="args">The non-<c>null</c> argument strings.</param>
        /// <returns><c>true</c> if the command is prefxed with the specified arguments.</returns>
        /// <remarks>
        /// <note>
        /// The argument comparison is case sensitive.
        /// </note>
        /// </remarks>
        public bool StartsWithArgs(params string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return false;
            }

            foreach (var arg in args)
            {
                if (arg == null)
                {
                    throw new ArgumentException($"[{nameof(args)}] may not include [null] values.");
                }
            }

            if (Arguments.Length < args.Length)
            {
                return false;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != Arguments[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Renders the command line as a string suitable for presenting to a process or
        /// a command line shell.  Arguments that include spaces will be enclosed in 
        /// double quotes.
        /// </summary>
        /// <returns>The command line string.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();

            for (int i = 0; i < Items.Length; i++)
            {
                var arg = Items[i];

                if (i > 0)
                {
                    sb.Append(' ');
                }

                if (arg.IndexOf(' ') != -1)
                {
                    sb.AppendFormat("\"{0}\"", arg);
                }
                else
                {
                    sb.Append(arg);
                }
            }

            return sb.ToString();
        }
    }
}
