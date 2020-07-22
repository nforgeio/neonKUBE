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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using System.Xml;
using System.Linq.Expressions;

namespace Neon.Common
{
    /// <summary>
    /// Implements a general purpose label based selector mechanism that can select a set of
    /// items from a collection based on the set of labels assigned to each item.  This class
    /// supports some simple fixed query methods as well as a simple text query language.
    /// </summary>
    /// <typeparam name="TItem">The underlying item type.</typeparam>
    /// <remarks>
    /// <para>
    /// For this to work, your <typeparamref name="TItem"/> type will need to implement
    /// the <see cref="ILabeled"/> interface and its <see cref="ILabeled.GetLabels()"/>
    /// which returns a string/string dictionary holding that items labels.  Then you can
    /// construct a selector instance via <see cref="LabelSelector(IEnumerable{TItem}, bool)"/>,
    /// passing your set of labeled items and optionally specifying case insensitive value
    /// comparisions.  Then you can call <see cref="GetItemsWith(string)"/>,
    /// <see cref="GetItemsWithout(string)"/>, and <see cref="SelectItems(string)"/>
    /// to select items based on their labels.
    /// </para>
    /// <note>
    /// Label names may be treated as case sensitive or insensitive based on how the underlying
    /// dictionary returned was constructed.  Generally though, labels are considered to be
    /// case insensitive so you should probably use <see cref="StringComparison.InvariantCultureIgnoreCase"/>
    /// when constructing the dictionaries returned by your item's <see cref="ILabeled.GetLabels()"/>
    /// method.
    /// </note>
    /// <note>
    /// Label values are considered to be case senstive by default but this can be customized
    /// via the constructor.
    /// </note>
    /// <note>
    /// See <see cref="SelectItems(string)"/> for a description of the query language.
    /// </note>
    /// </remarks>
    public class LabelSelector<TItem>
        where TItem : ILabeled
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Enumerates the expression types.
        /// </summary>
        private enum ExpressionType
        {
            Equal,
            NotEqual,
            In,
            NotIn,
            Has,
            NotHas
        }

        /// <summary>
        /// Implements label expressions.
        /// </summary>
        private class Expression
        {
            //-----------------------------------------------------------------
            // Static members

            /// <summary>
            /// Constructs a <see cref="ExpressionType.Equal"/> expression.
            /// </summary>
            /// <param name="label">The target label</param>
            /// <param name="value">The value.</param>
            /// <returns>The <see cref="Expression"/>.</returns>
            public static Expression Equal(string label, string value)
            {
                return new Expression()
                {
                    type  = ExpressionType.Equal,
                    label = label,
                    value = value
                };
            }

            /// <summary>
            /// Constructs a <see cref="ExpressionType.NotEqual"/> expression.
            /// </summary>
            /// <param name="label">The target label</param>
            /// <param name="value">The value.</param>
            /// <returns>The <see cref="Expression"/>.</returns>
            public static Expression NotEqual(string label, string value)
            {
                return new Expression()
                {
                    type  = ExpressionType.NotEqual,
                    label = label,
                    value = value
                };
            }

            /// <summary>
            /// Constructs a <see cref="ExpressionType.In"/> expression.
            /// </summary>
            /// <param name="label">The target label</param>
            /// <param name="values">The values.</param>
            /// <returns>The <see cref="Expression"/>.</returns>
            public static Expression In(string label, IEnumerable<string> values)
            {
                return new Expression()
                {
                    type   = ExpressionType.In,
                    label  = label,
                    values = values
                };
            }

            /// <summary>
            /// Constructs a <see cref="ExpressionType.NotIn"/> expression.
            /// </summary>
            /// <param name="label">The target label</param>
            /// <param name="values">The values.</param>
            /// <returns>The <see cref="Expression"/>.</returns>
            public static Expression NotIn(string label, IEnumerable<string> values)
            {
                return new Expression()
                {
                    type   = ExpressionType.NotIn,
                    label  = label,
                    values = values
                };
            }

            /// <summary>
            /// Constructs a <see cref="ExpressionType.Has"/> expression.
            /// </summary>
            /// <param name="label">The target label</param>
            /// <returns>The <see cref="Expression"/>.</returns>
            public static Expression Has(string label)
            {
                return new Expression()
                {
                    type  = ExpressionType.Has,
                    label = label
                };
            }

            /// <summary>
            /// Constructs a <see cref="ExpressionType.NotHas"/> expression.
            /// </summary>
            /// <param name="label">The target label</param>
            /// <returns>The <see cref="Expression"/>.</returns>
            public static Expression NotHas(string label)
            {
                return new Expression()
                {
                    type  = ExpressionType.NotHas,
                    label = label
                };
            }

            //-----------------------------------------------------------------
            // Instance members

            private ExpressionType      type;
            private string              label;
            private string              value;
            private IEnumerable<string> values;

            /// <summary>
            /// Private constructor.
            /// </summary>
            private Expression()
            {
            }

            /// <summary>
            /// Selects the items that satisfy the current operation.
            /// </summary>
            /// <param name="items">The input items.</param>
            /// <param name="comparison">Specifies how string comparisons are to be performed.</param>
            /// <returns><c>true</c> if the label expression was satisfied.</returns>
            public bool Execute(IEnumerable<TItem> items, StringComparison comparison)
            {
                Covenant.Requires<ArgumentNullException>(items != null);

                switch (type)
                {
                    case ExpressionType.Equal:

                        return items.Any(
                            item =>
                            {
                                var labels = item.GetLabels();

                                if (labels == null)
                                {
                                    return false;
                                }

                                if (!labels.TryGetValue(this.label, out var actualValue))
                                {
                                    return false;
                                }

                                return string.Equals(value, actualValue, comparison);
                            });

                    case ExpressionType.NotEqual:

                        return items.Any(
                            item =>
                            {
                                var labels = item.GetLabels();

                                if (labels == null)
                                {
                                    return false;
                                }

                                if (!labels.TryGetValue(this.label, out var actualValue))
                                {
                                    return false;
                                }

                                return !string.Equals(value, actualValue, comparison);
                            });

                    case ExpressionType.In:

                        return items.Any(
                            item =>
                            {
                                var labels = item.GetLabels();

                                if (labels == null)
                                {
                                    return false;
                                }

                                if (!labels.TryGetValue(this.label, out var actualValue))
                                {
                                    return false;
                                }

                                foreach (var value in values)
                                {
                                    if (string.Equals(value, actualValue, comparison))
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            });

                    case ExpressionType.NotIn:

                        return items.Any(
                            item =>
                            {
                                var labels = item.GetLabels();

                                if (labels == null)
                                {
                                    return false;
                                }

                                if (!labels.TryGetValue(this.label, out var actualValue))
                                {
                                    return false;
                                }

                                foreach (var value in values)
                                {
                                    if (string.Equals(value, actualValue, comparison))
                                    {
                                        return false;
                                    }
                                }

                                return true;
                            });

                    case ExpressionType.Has:

                        return items.Any(
                            item =>
                            {
                                var labels = item.GetLabels();

                                if (labels == null)
                                {
                                    return false;
                                }

                                return labels.ContainsKey(this.label);
                            });

                    case ExpressionType.NotHas:

                        return items.Any(
                            item =>
                            {
                                var labels = item.GetLabels();

                                if (labels == null)
                                {
                                    return false;
                                }

                                return !labels.ContainsKey(this.label);
                            });

                    default:

                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Enumerates the possible selector expression token types.
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
            /// <param name="labelSelector">The selector expression being parsed.</param>
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

                        if (input[pos++] == '=')
                        {
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

        /// <summary>
        /// Implements label expressions.
        /// </summary>
        private static class LabelExpression
        {
            /// <summary>
            /// Selects the items that satisfy the a label selector.
            /// </summary>
            /// <param name="items">The input items.</param>
            /// <param name="labelSelector">The label selector.</param>
            /// <param name="ignoreCase">Specifies whether case insensitive value comparisons are to be performed.</param>
            /// <returns>The selected items.</returns>
            /// <exception cref="FormatException">Thrown when the label selector is not valid.</exception>
            public static IEnumerable<TItem> Select(IEnumerable<TItem> items, string labelSelector, bool ignoreCase)
            {
                if (items == null)
                {
                    return Array.Empty<TItem>();
                }

                if (string.IsNullOrWhiteSpace(labelSelector))
                {
                    return items;
                }

                // Parse the label expressions.

                var lexer       = new Lexer(labelSelector);
                var expressions = new List<Expression>();

                while (!lexer.Eof)
                {
                    var token = lexer.Next();

                    switch (token.Type)
                    {
                        case TokenType.Not:

                            // Expecting: !label

                            expressions.Add(Expression.NotHas(lexer.Next(TokenType.String).Value));
                            lexer.Next(TokenType.Comma, TokenType.Eof);
                            break;

                        case TokenType.String:

                            var label = token.Value;

                            token = lexer.Next(TokenType.Equal, TokenType.NotEqual, TokenType.String, TokenType.Comma, TokenType.Eof);

                            switch (token.Type)
                            {
                                case TokenType.Equal:

                                    token = lexer.Next(TokenType.String);

                                    expressions.Add(Expression.Equal(label, token.Value));
                                    lexer.Next(TokenType.Comma, TokenType.Eof);
                                    break;

                                case TokenType.NotEqual:

                                    token = lexer.Next(TokenType.String);

                                    expressions.Add(Expression.NotEqual(label, token.Value));
                                    lexer.Next(TokenType.Comma, TokenType.Eof);
                                    break;

                                case TokenType.Eof:
                                case TokenType.Comma:

                                    expressions.Add(Expression.Has(label));
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

                                    expressions.Add(inOperator ? Expression.In(label, values) : Expression.NotIn(label, values));
                                    break;
                            }
                            break;

                        default:

                            throw new FormatException($"Invalid Label Selector: Unexpected [{token.Type}] in [{labelSelector}].");
                    }
                }

                // Execute the expressions against the items.  Those items with labels that
                // satisfy all expressions will be returned.

                return items.Where(
                    item =>
                    {
                        var stringComparison = ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;

                        foreach (var expression in expressions)
                        {
                            if (!expression.Execute(items, stringComparison))
                            {
                                return false;
                            }
                        }

                        return true;
                    });
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private IEnumerable<TItem>  items;
        private bool                ignoreCase;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="items">The set of items to be queries.  <c>null</c> will treated as an empty set.</param>
        /// <param name="ignoreCase">Optionally ignore character case when comparing label values.</param>
        public LabelSelector(IEnumerable<TItem> items, bool ignoreCase = false)
        {
            this.items      = items ?? new List<TItem>();
            this.ignoreCase = ignoreCase;
        }

        /// <summary>
        /// Returns the set of items including a specific label.
        /// </summary>
        /// <param name="label">The desired label.</param>
        /// <returns>The set of items with the label.</returns>
        public IEnumerable<TItem> GetItemsWith(string label)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(label), nameof(label));

            return items.Where(item => item.GetLabels().ContainsKey(label));
        }

        /// <summary>
        /// Returns the set of items that do not include the label.
        /// </summary>
        /// <param name="label">The undesired label.</param>
        /// <returns>The set of items without the label.</returns>
        public IEnumerable<TItem> GetItemsWithout(string label)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(label), nameof(label));

            return items.Where(item => !item.GetLabels().ContainsKey(label));
        }

        /// <summary>
        /// Returns the set of items that satisfy a label selector.
        /// </summary>
        /// <param name="labelSelector">The selector text.</param>
        /// <returns>The set of items whose meet the query requirements.</returns>
        /// <exception cref="FormatException">Thrown when the label selector is not valid.</exception>
        /// <remarks>
        /// <para>
        /// Label queries must include zero or more label expressions separated by commas. All label 
        /// expressions must be satisfied for an item to be selected. The expressions are essentially AND-ed together.
        /// We'll support two basic types of label expressions: equality/inequality and set based.
        /// </para>
        /// <note>
        /// A <c>null</c> or empty <paramref name="labelSelector"/> simply returns all items.
        /// </note>
        /// <para><b>equality/inequality expressions:</b></para>
        /// <code language="none">
        /// [label] = [value]
        /// [label] == [value]
        /// [label] != [value]      
        /// </code>
        /// <para>
        /// The first two examples two compare label value equality and the last compares for inequality. 
        /// Note that it is not currently possible to compare an empty or null string.
        /// </para>
        /// <para><b>set expressions:</b></para>
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
        /// </remarks>
        public IEnumerable<TItem> SelectItems(string labelSelector)
        {
            if (string.IsNullOrWhiteSpace(labelSelector))
            {
                return items;
            }

            return LabelExpression.Select(items, labelSelector, ignoreCase);
        }
    }
}
