//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Yaml.cs
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Neon.Common
{
    public static partial class NeonHelper
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// YAML naming convention that renders property names as lowercase
        /// and is case insensitive.
        /// </summary>
        private class LowercaseYamlNamingConvention : INamingConvention
        {
            public string Apply(string value)
            {
                return value.ToLowerInvariant();
            }
        }

        /// <summary>
        /// Customizes <c>enum</c> type conversions to/from strings recognizing
        /// <c>[EnumMember]</c> attributes when present.
        /// </summary>
        private class YamlEnumTypeConverter : IYamlTypeConverter
        {
            /// <inheritdoc/>
            public bool Accepts(Type type)
            {
                return type.IsEnum;
            }

            /// <inheritdoc/>
            public object ReadYaml(IParser parser, Type type)
            {
                var scaler = parser.Current as Scalar;

                parser.MoveNext();

                if (TryParseEnum(type, scaler.Value, out var output))
                {
                    return output;
                }
                else
                {
                    throw new InvalidDataException($"Cannot parse enumeration: {type.FullName}={scaler.Value}");
                }
            }

            /// <inheritdoc/>
            public void WriteYaml(IEmitter emitter, object value, Type type)
            {
                emitter.Emit(new Scalar(null, NeonHelper.EnumToString(type, value)));
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private static Lazy<ISerializer> yamlSerializer =
            new Lazy<ISerializer>(
                () =>
                {
                    return new SerializerBuilder()

                        // Note that we need to emit default values because it appears
                        // that YamlDotNet does not recognize the [DefaultValue] attributes
                        // and instead won't emit anything for zero values.  This means
                        // that without this, zero integers, doubles, null strings or
                        // enums with 0 values won't be emitted by default.
                        //
                        // Issues:
                        //
                        //      https://github.com/aaubry/YamlDotNet/issues/251
                        //      https://github.com/aaubry/YamlDotNet/issues/298

                        .EmitDefaults()

                        // We also need a custom type converter that honors [EnumMember]
                        // attributes on enumeration values.
                        //
                        //      https://www.cyotek.com/blog/using-custom-type-converters-with-csharp-and-yamldotnet-part-1
                        //      https://www.cyotek.com/blog/using-custom-type-converters-with-csharp-and-yamldotnet-part-2

                        .WithTypeConverter(new YamlEnumTypeConverter())

                        .WithNamingConvention(new LowercaseYamlNamingConvention())
                        .Build();
                });

        private static Lazy<IDeserializer> strictYamlDeserializer =
            new Lazy<IDeserializer>(
                () =>
                {
                    return new DeserializerBuilder()
                        .WithTypeConverter(new YamlEnumTypeConverter())
                        .WithNamingConvention(new LowercaseYamlNamingConvention())
                        .Build();
                });

        private static Lazy<IDeserializer> relaxedYamlDeserializer =
            new Lazy<IDeserializer>(
                () =>
                {
                    return new DeserializerBuilder()
                        .WithTypeConverter(new YamlEnumTypeConverter())
                        .WithNamingConvention(new LowercaseYamlNamingConvention())
                        .IgnoreUnmatchedProperties()
                        .Build();
                });

        /// <summary>
        /// <para>
        /// Serializes an object to YAML. 
        /// </para>
        /// <note>
        /// Property names are always converted to lowercase when serializing to YAML.
        /// </note>
        /// </summary>
        /// <param name="value">The value to be serialized.</param>
        /// <returns>The YAML text.</returns>
        public static string YamlSerialize(object value)
        {
            return yamlSerializer.Value.Serialize(value);
        }

        /// <summary>
        /// <para>
        /// Deserializes YAML text to an object, optionally requiring strict mapping of input properties to the target type.
        /// </para>
        /// <note>
        /// Property names are expected to be lowercase.
        /// </note>
        /// </summary>
        /// <typeparam name="T">The desired output type.</typeparam>
        /// <param name="yaml">The YAML text.</param>
        /// <param name="strict">Optionally require that all input properties map to route properties.</param>
        /// <returns>The parsed <typeparamref name="T"/>.</returns>
        public static T YamlDeserialize<T>(string yaml, bool strict = false)
        {
            try
            {
                if (strict)
                {
                    return strictYamlDeserializer.Value.Deserialize<T>(yaml);
                }
                else
                {
                    return relaxedYamlDeserializer.Value.Deserialize<T>(yaml);
                }
            }
            catch (YamlException e)
            {
                // The default parsing exceptions thrown by YamlDotNet aren't super 
                // helpful because the actual error is reported by the inner exception.
                // We're going to try to throw a new exception, with a nicer message.

                string message;

                if (e.InnerException == null)
                {
                    // Try to extract the part of the message after
                    // the error markers.

                    message = e.Message;

                    var pos = message.IndexOf("):");

                    if (pos != -1)
                    {
                        message = message.Substring(pos + 2).Trim();
                    }
                }
                else
                {
                    message = e.InnerException.Message;
                }

                throw new YamlException($"(line: {e.Start.Line}): {message}", e.InnerException);
            }
        }

        /// <summary>
        /// Converts a JSON text to YAML.
        /// </summary>
        /// <param name="jsonText">The JSON text.</param>
        /// <returns>The equivalent YAML text.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="jsonText"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="jsonText"/> does not specify a value, array, or object.</exception>
        /// <remarks>
        /// <note>
        /// Property names are always converted to lower case when converting to YAML.
        /// </note>
        /// </remarks>
        public static string JsonToYaml(string jsonText)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jsonText));

            // Note that we can't use [YamlSerialize()] because YamlDotNet doesn't
            // appear to honor the input types for dynamic/dictionary based types.
            // For example the string value "1001" will be serialized as the number
            // 1001, so we lose the "stringness".  I suspect the same thing may happen
            // for other property types.  This link seems to discuss a related problem:
            //
            //      https://stackoverflow.com/questions/50527836/yamldotnet-deserialize-integer-as-numeric-not-as-string
            //
            // We're going to work around this by doing our own serialization (GRRRR!).

            var sbYaml = new StringBuilder();
            var jToken = NeonHelper.JsonDeserialize<JToken>(jsonText);

            SerializeYaml(sbYaml, jToken, 0);

            return sbYaml.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sbYaml"></param>
        /// <param name="jToken"></param>
        /// <param name="nesting"></param>
        /// <param name="isArrayElement"></param>
        private static void SerializeYaml(StringBuilder sbYaml, JToken jToken, int nesting, bool isArrayElement = false)
        {
            string firstItemIndent;
            string otherItemIndent;

            if (!isArrayElement)
            {
                firstItemIndent =
                otherItemIndent = new string(' ', nesting * 2);
            }
            else
            {
                if (nesting == 0)
                {
                    firstItemIndent = "- ";
                }
                else
                {
                    firstItemIndent = new string(' ', (nesting - 1) * 2) + "- ";
                }

                otherItemIndent = new string(' ', nesting * 2);
            }

            if (jToken is JArray)
            {
                var jArray = (JArray)jToken;

                foreach (var item in jArray)
                {
                    SerializeYaml(sbYaml, item, nesting + 1, isArrayElement: true);
                }
            }
            else if (jToken is JObject)
            {
                var jObject         = (JObject)jToken;
                var properties      = jObject.Properties().ToList();
                var isFirstProperty = true;

                if (properties.Count > 0)
                {
                    foreach (var property in properties)
                    {
                        var propertyName  = property.Name.ToLowerInvariant();
                        var propertyValue = property.Value;
                        var indent        = string.Empty;

                        if (isFirstProperty)
                        {
                            indent          = firstItemIndent;
                            isFirstProperty = false;
                        }
                        else
                        {
                            indent = otherItemIndent;
                        }

                        if (propertyValue is JArray || propertyValue is JObject)
                        {
                            sbYaml.AppendLine($"{indent}{propertyName}:");
                            SerializeYaml(sbYaml, propertyValue, nesting + 1);
                        }
                        else if (propertyValue is JValue)
                        {
                            sbYaml.AppendLine($"{indent}{propertyName}: {GetYamlValue((JValue)propertyValue)}");
                        }
                        else
                        {
                            throw new NotSupportedException($"Unexpected token: {property.Value}");
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException("JObject with no properties cannot be serialized to YAML.");
                }
            }
            else if (jToken is JValue)
            {
                var valueToken = (JValue)jToken;
                var value      = GetYamlValue(valueToken);

                sbYaml.AppendLine($"{firstItemIndent}{value}");
            }
            else
            {
                throw new ArgumentException($"Unexpected token: {jToken}");
            }
        }

        private static readonly char[] specialYamlChars =
            new char[]
            {
                ':','{','}','[',']',',','&','*','#','?','|','-','<','>','=','!','%','@','\\','\r','\n','"','\''
            };

        /// <summary>
        /// Returns the serialized YAML value for a <see cref="JValue"/>.
        /// </summary>
        /// <param name="jValue">The value.</param>
        /// <returns>The serialized value.</returns>
        private static string GetYamlValue(JValue jValue)
        {
            switch (jValue.Type)
            {
                case JTokenType.Boolean:

                    return NeonHelper.ToBoolString((bool)jValue.Value);

                case JTokenType.Integer:
                case JTokenType.Float:

                    return jValue.ToString();

                case JTokenType.String:

                    // Strings are a bit tricky:
                    //
                    //      * Strings like "true", "yes", "on", "false", "no", or "off" will be single quoted so they won't
                    //        be misinterpreted as booleans.
                    //
                    //      * Strings like "1234" or "123.4" that parse as a number will be single quoted so they
                    //        won't be misinterpreted as a number.
                    //
                    //      * Strings with special characters will to be double quoted with appropriate escapes.
                    //
                    //      * Otherwise, we can render the string without quotes.

                    var value = (string)jValue.Value;

                    // Handle reserved booleans.

                    switch (value.ToLowerInvariant())
                    {
                        case "true":
                        case "false":
                        case "yes":
                        case "no":
                        case "on":
                        case "off":

                            return $"'{value}'";
                    }

                    // Handle strings that parse to a number.

                    if (double.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                    {
                        return $"'{value}'";
                    }

                    // Handle strings with special characters.

                    if (value.IndexOfAny(specialYamlChars) != -1)
                    {
                        var sb = new StringBuilder();

                        sb.Append("\"");

                        foreach (var ch in value)
                        {
                            switch (ch)
                            {
                                case '\r':

                                    sb.Append("\\r");
                                    break;

                                case '\n':

                                    sb.Append("\\n");
                                    break;

                                case '\'':

                                    sb.Append("'");
                                    break;

                                case '"':

                                    sb.Append("\\\"");
                                    break;

                                default:

                                    sb.Append(ch);
                                    break;
                            }
                        }

                        sb.Append("\"");

                        return sb.ToString();
                    }

                    // We dont need to quote the string.

                    return value;

                case JTokenType.Guid:
                case JTokenType.TimeSpan:
                case JTokenType.Uri:

                    return $"\"{jValue.Value}\"";

                case JTokenType.Null:

                    return "null";

                default:

                    throw new NotImplementedException();
            }
        }
    }
}
