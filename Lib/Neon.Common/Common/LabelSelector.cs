//-----------------------------------------------------------------------------
// FILE:	    LabelSelector.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using System.Collections;

namespace Neon.Common
{
    /// <summary>
    /// <see cref="LabelSelector{TItem}"/> related options.
    /// </summary>
    [Flags]
    public enum LabelSelectorOptions
    {
        /// <summary>
        /// No options are selected.
        /// </summary>
        None = 0x0000,

        /// <summary>
        /// <para>
        /// Normally <see cref="LabelSelector{TItem}"/> matches label values using
        /// case sensitive comparisons.  Use this to make the comparisons case
        /// insensitive.
        /// </para>
        /// <note>
        /// Label name case sensitivity is determined by the the dictionaries returned
        /// by the item <see cref="ILabeled.GetLabels()"/> method.
        /// </note>
        /// </summary>
        CaseInsensitiveValues = 0x0001,

        /// <summary>
        /// <see cref="LabelSelector{TItem}"/> defaults to parsing label names
        /// and values to ensure that they are Kubernetes compliant.  Use this 
        /// to disable this so you can use arbitrary labels.
        /// </summary>
        UnConstraintedLabels = 0x0002
    }

    /// <summary>
    /// Implements a Kubernetes compatible general purpose label based selector mechanism that 
    /// can select a set of items from a collection based on the set of labels assigned to each 
    /// item.  This class supports some simple fixed query methods as well as a simple text 
    /// query language.
    /// </summary>
    /// <typeparam name="TItem">The underlying item type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class supports Kubernetes style label selectors:
    /// </para>
    /// <para>
    /// <a href="https://kubernetes.io/docs/concepts/overview/working-with-objects/labels/">Kubernetes: Labels and Selectors</a>
    /// </para>
    /// <para>
    /// For this to work, your <typeparamref name="TItem"/> type will need to implement
    /// the <see cref="ILabeled"/> interface and its <see cref="ILabeled.GetLabels()"/>
    /// which returns a string/string dictionary holding that items labels.  Then you can
    /// construct a selector instance via <see cref="LabelSelector{TItem}"/>,
    /// passing your set of labeled items.  Then you can call <see cref="GetItemsWith(string)"/>,
    /// <see cref="GetItemsWithout(string)"/>, and <see cref="Select(string)"/>
    /// to select items based on their labels and one ore more label conditions to be
    /// satisified.
    /// </para>
    /// <note>
    /// <para>
    /// Kubernetes labels are key/value pairs. Valid label keys have two segments: an optional prefix and name, separated by a slash <c>(/)</c>. 
    /// The name segment is required and must be 63 characters or less, beginning and ending with an alphanumeric character <c>([a-z0-9A-Z])</c> 
    /// with dashes <c>(-)</c>, underscores <c>(_)</c>, dots <c>(.)</c>, and alphanumerics between. The prefix is optional. If specified, 
    /// the prefix must be a DNS subdomain: a series of DNS labels separated by dots <b>(.)</b>, not longer than 253 characters in total,
    /// followed by a slash <c>(/)</c>.
    /// </para>
    /// <para>
    /// If the prefix is omitted, the label Key is presumed to be private to the user. Automated system components (e.g. kube-scheduler,
    /// kube-controller-manager, kube-apiserver, kubectl, or other third-party automation) which add labels to end-user objects must specify
    /// a prefix.
    /// </para>
    /// <para>
    /// The <c>kubernetes.io/</c> and <c>k8s.io/</c> prefixes are reserved for Kubernetes core components.
    /// </para>
    /// <para>
    /// Valid label values must be 63 characters or less and must be empty or begin and end with an alphanumeric character <c>([a-z0-9A-Z])</c> 
    /// with dashes <c>(-)</c>, underscores <c>(_)</c>, dots <c>(.)</c>, and alphanumerics between.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// <b>Label Names:</b> must conform to the Kubernetes standard and will be treated as case sensitive 
    /// or insensitive based on how the underlying dictionary returned was constructed.  Generally
    /// though, labels are considered to be case insensitive so you should probably use 
    /// <see cref="StringComparison.InvariantCultureIgnoreCase"/> when constructing the dictionaries
    /// returned by your item's <see cref="ILabeled.GetLabels()"/> method.
    /// </para>
    /// <para>
    /// Label names must conform to the Kubernetes conventions by default but this can be
    /// relaxed by passing an option to the constructor.
    /// </para>
    /// </note>
    /// <note>
    /// <b>Label Values:</b> are considered to be case senstive by default but this can be customized
    /// via the constructor.
    /// </note>
    /// <note>
    /// See <see cref="Select(string)"/> for a description of the label selector language.
    /// </note>
    /// <note>
    /// You may use the <c>static</c> <see cref="LabelSelector.ValidateLabelKey(string)"/> and 
    /// <see cref="LabelSelector.ValidateLabelValue(string)"/> methods to explicity confirm that 
    /// label keys and values satisfy the Kubernetes conventions.
    /// </note>
    /// </remarks>
    public class LabelSelector<TItem>
        where TItem : ILabeled
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Enumerates the condition types.
        /// </summary>
        private enum ConditionType
        {
            Equal,
            NotEqual,
            In,
            NotIn,
            Has,
            NotHas
        }

        /// <summary>
        /// Implements label conditions.
        /// </summary>
        private class LabelCondition
        {
            //-----------------------------------------------------------------
            // Static members

            /// <summary>
            /// Constructs a <see cref="ConditionType.Equal"/> condition.
            /// </summary>
            /// <param name="labelKey">The target label key.</param>
            /// <param name="value">The value.</param>
            /// <param name="options">The selector options.</param>
            /// <returns>The <see cref="LabelCondition"/>.</returns>
            public static LabelCondition Equal(string labelKey, string value, LabelSelectorOptions options)
            {
                ValidateLabelKey(labelKey, options);
                ValidateLabelValue(value, options);

                return new LabelCondition()
                {
                    type       = ConditionType.Equal,
                    labelKey   = labelKey,
                    labelValue = value
                };
            }

            /// <summary>
            /// Constructs a <see cref="ConditionType.NotEqual"/> condition.
            /// </summary>
            /// <param name="labelKey">The target label key.</param>
            /// <param name="value">The value.</param>
            /// <param name="options">The selector options.</param>
            /// <returns>The <see cref="LabelCondition"/>.</returns>
            public static LabelCondition NotEqual(string labelKey, string value, LabelSelectorOptions options)
            {
                ValidateLabelKey(labelKey, options);
                ValidateLabelValue(value, options);

                return new LabelCondition()
                {
                    type       = ConditionType.NotEqual,
                    labelKey   = labelKey,
                    labelValue = value
                };
            }

            /// <summary>
            /// Constructs a <see cref="ConditionType.In"/> condition.
            /// </summary>
            /// <param name="labelKey">The target label key.</param>
            /// <param name="values">The values.</param>
            /// <param name="options">The selector options.</param>
            /// <returns>The <see cref="LabelCondition"/>.</returns>
            public static LabelCondition In(string labelKey, IEnumerable<string> values, LabelSelectorOptions options)
            {
                ValidateLabelKey(labelKey, options);

                foreach (var value in values)
                {
                    ValidateLabelValue(value, options);
                }

                return new LabelCondition()
                {
                    type        = ConditionType.In,
                    labelKey    = labelKey,
                    labelValues = values
                };
            }

            /// <summary>
            /// Constructs a <see cref="ConditionType.NotIn"/> condition.
            /// </summary>
            /// <param name="labelKey">The target label key.</param>
            /// <param name="values">The values.</param>
            /// <param name="options">The selector options.</param>
            /// <returns>The <see cref="LabelCondition"/>.</returns>
            public static LabelCondition NotIn(string labelKey, IEnumerable<string> values, LabelSelectorOptions options)
            {
                ValidateLabelKey(labelKey, options);

                foreach (var value in values)
                {
                    ValidateLabelValue(value, options);
                }

                return new LabelCondition()
                {
                    type        = ConditionType.NotIn,
                    labelKey    = labelKey,
                    labelValues = values
                };
            }

            /// <summary>
            /// Constructs a <see cref="ConditionType.Has"/> condition.
            /// </summary>
            /// <param name="labelKey">The target label kewy.</param>
            /// <param name="options">The selector options.</param>
            /// <returns>The <see cref="LabelCondition"/>.</returns>
            public static LabelCondition Has(string labelKey, LabelSelectorOptions options)
            {
                ValidateLabelKey(labelKey, options);

                return new LabelCondition()
                {
                    type     = ConditionType.Has,
                    labelKey = labelKey
                };
            }

            /// <summary>
            /// Constructs a <see cref="ConditionType.NotHas"/> condition.
            /// </summary>
            /// <param name="labelKey">The target label key</param>
            /// <param name="options">The selector options.</param>
            /// <returns>The <see cref="LabelCondition"/>.</returns>
            public static LabelCondition NotHas(string labelKey, LabelSelectorOptions options)
            {
                ValidateLabelKey(labelKey, options);

                return new LabelCondition()
                {
                    type     = ConditionType.NotHas,
                    labelKey = labelKey
                };
            }


            /// <summary>
            /// Validates a label key.
            /// </summary>
            /// <param name="labelKey">The label key.</param>
            /// <param name="options">The selector options.</param>
            /// <exception cref="FormatException">Thrown if the key is not valid.</exception>
            public static void ValidateLabelKey(string labelKey, LabelSelectorOptions options)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(labelKey), nameof(labelKey));

                if ((options & LabelSelectorOptions.UnConstraintedLabels) != 0)
                {
                    return;
                }

                LabelSelector.ValidateLabelKey(labelKey);
            }

            /// <summary>
            /// Validates a label value.
            /// </summary>
            /// <param name="labelValue">The label value.</param>
            /// <param name="options">The selector options.</param>
            /// <exception cref="FormatException">Thrown if the key is not valid.</exception>
            public static void ValidateLabelValue(string labelValue, LabelSelectorOptions options)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(labelValue), nameof(labelValue));

                if ((options & LabelSelectorOptions.UnConstraintedLabels) != 0)
                {
                    return;
                }

                LabelSelector.ValidateLabelValue(labelValue);
            }

            //-----------------------------------------------------------------
            // Instance members

            private ConditionType       type;
            private string              labelKey;
            private string              labelValue;
            private IEnumerable<string> labelValues;

            /// <summary>
            /// Private constructor.
            /// </summary>
            private LabelCondition()
            {
            }

            /// <summary>
            /// Detects whether an item satisfies a label condition.
            /// </summary>
            /// <param name="item">The input item.</param>
            /// <param name="valueComparison">Specifies how label value comparisons are to be performed.</param>
            /// <returns><c>true</c> if the label selector was satisfied.</returns>
            public bool Execute(TItem item, StringComparison valueComparison)
            {
                string actualValue;

                var labels = item.GetLabels();

                if (labels == null)
                {
                    return false;
                }

                switch (type)
                {
                    case ConditionType.Equal:

                        if (!labels.TryGetValue(this.labelKey, out actualValue))
                        {
                            return false;
                        }

                        return string.Equals(labelValue, actualValue, valueComparison);

                    case ConditionType.NotEqual:

                        if (!labels.TryGetValue(this.labelKey, out actualValue))
                        {
                            return false;
                        }

                        return !string.Equals(labelValue, actualValue, valueComparison);

                    case ConditionType.In:

                        if (!labels.TryGetValue(this.labelKey, out actualValue))
                        {
                            return false;
                        }

                        foreach (var value in labelValues)
                        {
                            if (string.Equals(value, actualValue, valueComparison))
                            {
                                return true;
                            }
                        }

                        return false;

                    case ConditionType.NotIn:

                        if (!labels.TryGetValue(this.labelKey, out actualValue))
                        {
                            return false;
                        }

                        foreach (var value in labelValues)
                        {
                            if (string.Equals(value, actualValue, valueComparison))
                            {
                                return false;
                            }
                        }

                        return true;

                    case ConditionType.Has:

                        return labels.ContainsKey(this.labelKey);

                    case ConditionType.NotHas:

                        return !labels.ContainsKey(this.labelKey);

                    default:

                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Enumerates the possible label selector token types.
        /// </summary>
        private enum TokenType
        {
            /// <summary>
            /// Indicates the end of input.
            /// </summary>
            Eof,

            /// <summary>
            /// A label name, value or a text operator like <b>in</b> or <b>notin</b>.
            /// </summary>
            String,

            /// <summary>
            /// A <b>'!'</b> operator.
            /// </summary>
            Not,

            /// <summary>
            /// A left parentheses <b>'('</b>
            /// </summary>
            LParen,

            /// <summary>
            /// A right parentheses <b>'>'</b>
            /// </summary>
            RParen,

            /// <summary>
            /// A comma <b>','</b>
            /// </summary>
            Comma,

            /// <summary>
            /// A <b>=</b> or <b>==</b>
            /// </summary>
            Equal,

            /// <summary>
            /// A <b>!=</b>
            /// </summary>
            NotEqual,
        }

        /// <summary>
        /// Holds a token extracted by the <see cref="Lexer"/>.
        /// </summary>
        private struct Token
        {
            //-----------------------------------------------------------------
            // Static members

            /// <summary>
            /// Returns <see cref="TokenType.Eof"/> token.
            /// </summary>
            /// <returns>The token.</returns>
            public static Token Eof()
            {
                return new Token() { Type = TokenType.Eof };
            }

            /// <summary>
            /// Returns <see cref="TokenType.String"/> token.
            /// </summary>
            /// <param name="value">The string value.</param>
            /// <returns>The token.</returns>
            public static Token String(string value)
            {
                return new Token() { Type = TokenType.String, Value = value };
            }

            /// <summary>
            /// Returns <see cref="TokenType.Not"/> token.
            /// </summary>
            /// <returns>The token.</returns>
            public static Token Not()
            {
                return new Token() { Type = TokenType.Not };
            }

            /// <summary>
            /// Returns <see cref="TokenType.LParen"/> token.
            /// </summary>
            /// <returns>The token.</returns>
            public static Token LParen()
            {
                return new Token() { Type = TokenType.LParen };
            }

            /// <summary>
            /// Returns <see cref="TokenType.RParen"/> token.
            /// </summary>
            /// <returns>The token.</returns>
            public static Token RParen()
            {
                return new Token() { Type = TokenType.RParen };
            }

            /// <summary>
            /// Returns <see cref="TokenType.Comma"/> token.
            /// </summary>
            /// <returns>The token.</returns>
            public static Token Comma()
            {
                return new Token() { Type = TokenType.Comma };
            }

            /// <summary>
            /// Returns <see cref="TokenType.Equal"/> token.
            /// </summary>
            /// <returns>The token.</returns>
            public static Token Equal()
            {
                return new Token() { Type = TokenType.Equal };
            }

            /// <summary>
            /// Returns <see cref="TokenType.NotEqual"/> token.
            /// </summary>
            /// <returns>The token.</returns>
            public static Token NotEqual()
            {
                return new Token() { Type = TokenType.NotEqual };
            }

            //-----------------------------------------------------------------
            // Instance members

            /// <summary>
            /// Identifies the token type.
            /// </summary>
            public TokenType Type;

            /// <summary>
            /// Returns the value for <see cref="TokenType.String"/> tokens.
            /// </summary>
            public string Value;
        }

        /// <summary>
        /// Implements a simple lexical analyzer.  This is probably a bit of an overkill
        /// for the current implementation but may come in handy if we ever extend the
        /// selector language to include nested subexpressions, etc.
        /// </summary>
        private struct Lexer
        {
            private string  input;
            private int     pos;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="labelSelector">The label selector being parsed.</param>
            public Lexer(string labelSelector)
            {
                input = labelSelector ?? string.Empty;
                pos   = 0;
            }

            /// <summary>
            /// Returns the next token from the input, optionally verifying that the token
            /// found was expected.
            /// </summary>
            /// <returns>The next <see cref="Token"/>.</returns>
            public Token Next()
            {
                if (pos >= input.Length)
                {
                    return Token.Eof();
                }

                // Skip over any whitespace

                while (pos < input.Length && char.IsWhiteSpace(input[pos]))
                {
                    pos++;
                }

                if (Eof)
                {
                    return Token.Eof();
                }

                switch (input[pos])
                {
                    case '!':

                        pos++;

                        if (Eof)
                        {
                            return Token.Not();
                        }

                        if (input[pos] == '=')
                        {
                            pos++;

                            return Token.NotEqual();
                        }
                        else
                        {
                            return Token.Not();
                        }

                    case '=':

                        pos++;

                        if (Eof)
                        {
                            return Token.Equal();
                        }

                        if (input[pos] == '=')
                        {
                            pos++;
                        }

                        return Token.Equal();

                    case '(':

                        pos++;
                        return Token.LParen();

                    case ')':

                        pos++;
                        return Token.RParen();

                    case ',':

                        pos++;
                        return Token.Comma();

                    default:

                        // Must be a string.  We'll scan forward until we hit whitespace, another
                        // token or the EOF.  We'll consider anything else to be part of the string.

                        var posEnd = pos;

                        while (posEnd < input.Length)
                        {
                            switch (input[posEnd])
                            {
                                case ' ':
                                case '\r':
                                case '\n':
                                case '\t':
                                case '!':
                                case '(':
                                case ')':
                                case ',':
                                case '=':

                                    var value1 = input.Substring(pos, posEnd - pos);

                                    pos = posEnd;

                                    return Token.String(value1);

                            }

                            posEnd++;
                        }

                        // Must have reached EOF.

                        var value2 = input.Substring(pos, posEnd - pos);

                        pos = posEnd;

                        return Token.String(value2);
                }
            }

            /// <summary>
            /// Returns the next token from the input, optionally verifying that the token
            /// found was expected.
            /// </summary>
            /// <param name="expectedTypes">Specifies the expected token types.</param>
            /// <returns>The next <see cref="Token"/>.</returns>
            /// <exception cref="FormatException">Thrown when the next token doesn't match <paramref name="expectedTypes"/>.</exception>
            public Token Next(params TokenType[] expectedTypes)
            {
                var token = Next();

                if (!expectedTypes.Any(t => t == token.Type))
                {
                    var sbExpectedTypes = new StringBuilder();

                    foreach (var type in expectedTypes)
                    {
                        sbExpectedTypes.AppendWithSeparator(type.ToString(), ", ");
                    }

                    throw new FormatException($"Invalid Label Selector: [{sbExpectedTypes}] token expected, not [{token.Type}] in [{input}].");
                }

                return token;
            }

            /// <summary>
            /// Returns <c>true</c> when the lexer has reached the end of the input.
            /// </summary>
            public bool Eof => pos >= input.Length;
        }

        //---------------------------------------------------------------------
        // Instance members

        private IEnumerable<TItem>      items;
        private LabelSelectorOptions    options;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="items">The set of items to be queries.  <c>null</c> will treated as an empty set.</param>
        /// <param name="options">Optionally customize selector case sensitivity and other behaviors.</param>
        public LabelSelector(IEnumerable<TItem> items, LabelSelectorOptions options = LabelSelectorOptions.None)
        {
            this.items   = items ?? new List<TItem>();
            this.options = options;
        }

        /// <summary>
        /// Indicates that we're doing case insensitive label value comparisons.
        /// </summary>
        private bool CaseInsensitive => (options & LabelSelectorOptions.CaseInsensitiveValues) != 0;

        /// <summary>
        /// Returns the set of items including a specific label.
        /// </summary>
        /// <param name="labelKey">The desired label key.</param>
        /// <returns>The set of items with the label.</returns>
        public IEnumerable<TItem> GetItemsWith(string labelKey)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(labelKey), nameof(labelKey));

            return items.Where(item => item.GetLabels().ContainsKey(labelKey));
        }

        /// <summary>
        /// Returns the set of items that do not include the label.
        /// </summary>
        /// <param name="labelKey">The undesired label key.</param>
        /// <returns>The set of items without the label.</returns>
        public IEnumerable<TItem> GetItemsWithout(string labelKey)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(labelKey), nameof(labelKey));

            return items.Where(item => !item.GetLabels().ContainsKey(labelKey));
        }

        /// <summary>
        /// Returns the set of items that satisfy a label selector.
        /// </summary>
        /// <param name="labelSelector">The label selector condistions(s).</param>
        /// <returns>The set of items whose meet the query requirements.</returns>
        /// <exception cref="FormatException">Thrown when the label selector is not valid.</exception>
        /// <remarks>
        /// <para>
        /// This class supports Kubernetes style label selectors:
        /// </para>
        /// <para>
        /// <a href="https://kubernetes.io/docs/concepts/overview/working-with-objects/labels/">Kubernetes: Labels and Selectors</a>
        /// </para>
        /// <para>
        /// Label selectors must include zero or more label conditions separated by commas. All label 
        /// conditions must be satisfied for an item to be selected. The conditions are essentially AND-ed together.
        /// We'll support two basic types of label conditions: equality/inequality and set based.
        /// </para>
        /// <note>
        /// A <c>null</c> or empty <paramref name="labelSelector"/> simply returns all items.
        /// </note>
        /// <para><b>equality/inequality conditions:</b></para>
        /// <code language="none">
        /// [label] = [value]
        /// [label] == [value]
        /// [label] != [value]      
        /// </code>
        /// <para>
        /// The first two examples two compare label value equality and the last compares for inequality. 
        /// Note that it is not currently possible to compare an empty or null string.
        /// </para>
        /// <para><b>set conditions:</b></para>
        /// <code language="none">
        /// [label] in ([value1], [value2],,...)
        /// notin([value1], [value2],...)
        /// [label]
        /// ![label]
        /// </code>
        /// <para>
        /// The first example selects items if they have a label with any of the values listed and the second 
        /// selects items that have the label that doesn't have any of the values. The last two examples select 
        /// items when they have or don't have a label, regardless of its value.
        /// </para>
        /// <note>
        /// The <b>in</b> and <b>notin</b> operators both require that the item have the target label for a match.
        /// </note>
        /// <note>
        /// <b>Case Sensitivity:</b> Label name lookups are actually determined by the dictionary returned
        /// by each item.  .NET string dictionaries are typically case sensitive by default but you can
        /// change this behavior by having your item implementations construct case insenstive dictionaries.
        /// By default, this class performs case insensitive comparisions for label values.  You can override
        /// this by passing <see cref="LabelSelectorOptions.CaseInsensitiveValues"/> to the
        /// <see cref="LabelSelector{TItem}.LabelSelector(IEnumerable{TItem}, LabelSelectorOptions)"/> constructor.
        /// </note>
        /// <note>
        /// <para>
        /// <b>Label Name Constraints:</b> Label keys are checked to ensure that they match Kubernetes conventions
        /// by default.  You can override this by passing <see cref="LabelSelectorOptions.UnConstraintedLabels"/> to the
        /// <see cref="LabelSelector{TItem}.LabelSelector(IEnumerable{TItem}, LabelSelectorOptions)"/> constructor.
        /// </para>
        /// <para>
        /// <b>Label Value Constraints:</b> Label values are also checked to ensure that they match Kubernetes conventions
        /// by default.  This behavior can also be overriden by passing to the constructor.
        /// </para>
        /// </note>
        /// </remarks>
        public IEnumerable<TItem> Select(string labelSelector)
        {
            if (items == null || items.Count() == 0)
            {
                return Array.Empty<TItem>();
            }

            if (string.IsNullOrWhiteSpace(labelSelector))
            {
                return items;
            }

            if (items == null)
            {
                return Array.Empty<TItem>();
            }

            // Parse the label conditions.

            var lexer      = new Lexer(labelSelector);
            var conditions = new List<LabelCondition>();

            while (!lexer.Eof)
            {
                var token = lexer.Next();

                switch (token.Type)
                {
                    case TokenType.Not:

                        // Expecting: !label

                        conditions.Add(LabelCondition.NotHas(lexer.Next(TokenType.String).Value, options));
                        lexer.Next(TokenType.Comma, TokenType.Eof);
                        break;

                    case TokenType.String:

                        var label = token.Value;

                        token = lexer.Next(TokenType.Equal, TokenType.NotEqual, TokenType.String, TokenType.Comma, TokenType.Eof);

                        switch (token.Type)
                        {
                            case TokenType.Equal:

                                token = lexer.Next(TokenType.String);

                                conditions.Add(LabelCondition.Equal(label, token.Value, options));
                                lexer.Next(TokenType.Comma, TokenType.Eof);
                                break;

                            case TokenType.NotEqual:

                                token = lexer.Next(TokenType.String);

                                conditions.Add(LabelCondition.NotEqual(label, token.Value, options));
                                lexer.Next(TokenType.Comma, TokenType.Eof);
                                break;

                            case TokenType.Eof:
                            case TokenType.Comma:

                                conditions.Add(LabelCondition.Has(label, options));
                                break;

                            case TokenType.String:

                                bool inOperator;

                                if (token.Value.Equals("in", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    inOperator = true;
                                }
                                else if (token.Value.Equals("notin", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    inOperator = false;
                                }
                                else
                                {
                                    throw new FormatException($"Invalid Label Selector: Invalid [{token.Value}] set operator in [{labelSelector}].");
                                }

                                var values = new List<string>();

                                lexer.Next(TokenType.LParen);

                                while (true)
                                {
                                    token = lexer.Next(TokenType.String);

                                    values.Add(token.Value);

                                    token = lexer.Next(TokenType.Comma, TokenType.RParen);

                                    if (token.Type == TokenType.RParen)
                                    {
                                        break;
                                    }
                                }

                                conditions.Add(inOperator ? LabelCondition.In(label, values, options) : LabelCondition.NotIn(label, values, options));
                                lexer.Next(TokenType.Comma, TokenType.Eof);
                                break;
                        }
                        break;

                    default:

                        throw new FormatException($"Invalid Label Selector: Unexpected [{token.Type}] in [{labelSelector}].");
                }
            }

            // Execute the conditsions against the items.  Those items with labels that
            // satisfy all conditions will be returned.

            return items.Where(
                item =>
                {
                    var stringComparison = CaseInsensitive ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;

                    foreach (var condition in conditions)
                    {
                        if (!condition.Execute(item, stringComparison))
                        {
                            return false;
                        }
                    }

                    return true;
                });
        }
    }

    /// <summary>
    /// Implements label related utilities.
    /// </summary>
    public static class LabelSelector
    {
        /// <summary>
        /// Determines whether a character is [a-z0-9A-Z].
        /// </summary>
        /// <param name="ch">The test character.</param>
        /// <returns><c>true</c> for valid.</returns>
        private static bool IsAlphaNum(char ch)
        {
            if ('a' <= ch && ch <= 'z')
            {
                return true;
            }
            else if ('A' <= ch && ch <= 'Z')
            {
                return true;
            }
            else if ('0' <= ch && ch <= '9')
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether a character is a valid label punctuation character.
        /// </summary>
        /// <param name="ch">The test character.</param>
        /// <returns><c>true</c> for valid.</returns>
        private static bool IsValidPunctuation(char ch)
        {
            return ch == '_' || ch == '.' || ch == '-';
        }

        /// <summary>
        /// Validates a label key.
        /// </summary>
        /// <param name="labelKey">The label key.</param>
        /// <exception cref="FormatException">Thrown if the key is not valid.</exception>
        public static void ValidateLabelKey(string labelKey)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(labelKey), nameof(labelKey));

            // Extract the prefix and name parts from the key.

            string prefix;
            string name;

            var pos = labelKey.IndexOf('/');

            if (pos == -1)
            {
                prefix = null;
                name = labelKey;
            }
            else
            {
                prefix = labelKey.Substring(0, pos);
                name = labelKey.Substring(pos + 1);
            }

            // Validate that the label prefix looks like a DNS name.

            if (prefix != null)
            {
                if (prefix.Length == 0)
                {
                    throw new FormatException($"Label key prefix cannot be empty: [{labelKey}].");
                }
                else if (prefix.Length > 253)
                {
                    throw new FormatException($"Label key prefix cannot be longer than 253 characters: [{labelKey}].");
                }

                var dnsLabels = prefix.Split('.');

                if (dnsLabels.Length < 2)
                {
                    throw new FormatException($"Label key prefix is not a valid DNS name: [{labelKey}].");
                }

                if (dnsLabels.Any(
                    label =>
                    {
                        if (label.Length == 0 || label.Length > 63)
                        {
                            return true;
                        }

                        if (!IsAlphaNum(label.First()) || !IsAlphaNum(label.Last()))
                        {
                            return true;
                        }

                        foreach (var ch in label)
                        {
                            if (!IsAlphaNum(ch) && !IsValidPunctuation(ch))
                            {
                                return true;
                            }
                        }

                        return false;
                    }))
                {
                    throw new FormatException($"Label key prefix is not a valid DNS name: [{labelKey}].");
                }
            }

            // Validate that the label name is valid.

            if (name.Length == 0)
            {
                throw new FormatException($"Label names cannot be empty: [{labelKey}].");
            }
            else if (name.Length > 63)
            {
                throw new FormatException($"Label names cannot be longer than 63 characters: [{labelKey}].");
            }

            if (!IsAlphaNum(name.First()) || !IsAlphaNum(name.Last()))
            {
                throw new FormatException($"Label names must start and end with [a-z0-9A-Z]: [{labelKey}].");
            }

            foreach (var ch in name)
            {
                if (!IsAlphaNum(ch) && !IsValidPunctuation(ch))
                {
                    throw new FormatException($"Label name includes the invalid character [{ch}]: [{labelKey}].");
                }
            }
        }

        /// <summary>
        /// Validates a label value.
        /// </summary>
        /// <param name="labelValue">The label value.</param>
        /// <exception cref="FormatException">Thrown if the key is not valid.</exception>
        public static void ValidateLabelValue(string labelValue)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(labelValue), nameof(labelValue));

            if (labelValue.Length == 0)
            {
                return;     // Empty values are OK.
            }

            if (labelValue.Length > 63)
            {
                throw new FormatException($"Label value cannot be longer than 63 characters: [{labelValue}].");
            }
            else if (!IsAlphaNum(labelValue.First()) || !IsAlphaNum(labelValue.Last()))
            {
                throw new FormatException($"Label value must start and end with [a-z0-9A-Z]: [{labelValue}].");
            }

            foreach (var ch in labelValue)
            {
                if (!IsAlphaNum(ch) && !IsValidPunctuation(ch))
                {
                    throw new FormatException($"Label value includes the invalid character [{ch}]: [{labelValue}].");
                }
            }
        }
    }
}

