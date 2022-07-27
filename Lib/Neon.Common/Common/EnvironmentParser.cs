//-----------------------------------------------------------------------------
// FILE:        EnvironmentParser.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Common
{
    /// <summary>
    /// Handles parsing of environment variables by default or optionally variables
    /// from a custom source.  This has built-in methods for parsing: <c>int</c>,
    /// <c>long</c>, <c>double</c>, <c>TimeSpan</c>, <c>string</c> and <c>enum</c>
    /// variables as well as mechanisms to parse custom types.
    /// </summary>
    public class EnvironmentParser
    {
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Attempts to parse an environment variable as a specific type.
        /// </summary>
        /// <typeparam name="T">The output type.</typeparam>
        /// <param name="input">The string to be parsed.</param>
        /// <param name="value">Returns as the parsed value on success.</param>
        /// <param name="error">Returns as the error message on failure.</param>
        /// <returns><c>true</c> If the input was parsed successfully.</returns>
        public delegate bool Parser<T>(string input, out T value, out string error);

        /// <summary>
        /// Validates that a parsed environment variable is valid.
        /// </summary>
        /// <typeparam name="T">The parsed variable type.</typeparam>
        /// <param name="input">The input value.</param>
        /// <returns>Returns <c>true</c> if the input value is valid.</returns>
        public delegate bool Validator<T>(T input);

        /// <summary>
        /// <para>
        /// Used for custom variable lookup implementation.  This will be passed
        /// a variable name and should return the value as a string when the variable
        /// exists or <c>null</c> if it does not.
        /// </para>
        /// <para>
        /// Pass a custom implementation to the <see cref="EnvironmentParser"/> constructor
        /// when necessary.
        /// </para>
        /// </summary>
        /// <param name="variable">The variable name.</param>
        /// <returns>The variable's value or <c>null</c>.</returns>
        public delegate string VariableSource(string variable);

        //---------------------------------------------------------------------
        // Built-in parsers.

        /// <summary>
        /// Parses a <see cref="string"/>.
        /// </summary>
        /// <param name="input">The input value.</param>
        /// <param name="value">Returns as the parsed value.</param>
        /// <param name="error">Returns as the error message on failure.</param>
        /// <returns>Returns <c>true</c> if the input value is valid.</returns>
        private static bool StringParser(string input, out string value, out string error)
        {
            // We just pass the input through unchanged for strings.

            value = input;
            error = null;

            return true;
        }

        /// <summary>
        /// Parses an <see cref="int"/>.
        /// </summary>
        /// <param name="input">The input value.</param>
        /// <param name="value">Returns as the parsed value.</param>
        /// <param name="error">Returns as the error message on failure.</param>
        /// <returns>Returns <c>true</c> if the input value is valid.</returns>
        private static bool IntParser(string input, out int value, out string error)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(input), nameof(input));

            value = 0;
            error = null;

            if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                error = $"Invalid integer: {input}";

                return false;
            }

            return true;
        }

        /// <summary>
        /// Parses a <see cref="long"/>.
        /// </summary>
        /// <param name="input">The input value.</param>
        /// <param name="value">Returns as the parsed value.</param>
        /// <param name="error">Returns as the error message on failure.</param>
        /// <returns>Returns <c>true</c> if the input value is valid.</returns>
        private static bool LongParser(string input, out long value, out string error)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(input), nameof(input));

            value = 0L;
            error = null;

            if (!long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                error = $"Invalid long: {input}";

                return false;
            }

            return true;
        }

        /// <summary>
        /// Parses a <see cref="double"/>.
        /// </summary>
        /// <param name="input">The input value.</param>
        /// <param name="value">Returns as the parsed value.</param>
        /// <param name="error">Returns as the error message on failure.</param>
        /// <returns>Returns <c>true</c> if the input value is valid.</returns>
        private static bool DoubleParser(string input, out double value, out string error)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(input), nameof(input));

            value = 0.0;
            error = null;

            if (!double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                error = $"Invalid double: {input}";

                return false;
            }

            return true;
        }

        /// <summary>
        /// Parses a <see cref="bool"/>.
        /// </summary>
        /// <param name="input">The input value.</param>
        /// <param name="value">Returns as the parsed value.</param>
        /// <param name="error">Returns as the error message on failure.</param>
        /// <returns>Returns <c>true</c> if the input value is valid.</returns>
        private static bool BoolParser(string input, out bool value, out string error)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(input), nameof(input));

            input = input.ToLowerInvariant();
            value = false;
            error = null;

            switch (input)
            {
                case "true":
                case "yes":
                case "on":
                case "1":

                    value = true;
                    break;
 
                case "false":
                case "no":
                case "off":
                case "0":

                    value = false;
                    break;

                default:

                    error = $"Invalid boolean: {input}";
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Parses a <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="input">The input value.</param>
        /// <param name="value">Returns as the parsed value.</param>
        /// <param name="error">Returns as the error message on failure.</param>
        /// <returns>Returns <c>true</c> if the input value is valid.</returns>
        private static bool TimeSpanParser(string input, out TimeSpan value, out string error)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(input), nameof(input));

            input = input.ToLowerInvariant();
            value = TimeSpan.Zero;
            error = null;

            // First, try parsing a float value with optional unit suffix (defaults to seconds).

            var units = (string)null;

            if (input.EndsWith("ms", StringComparison.InvariantCultureIgnoreCase))
            {
                units = "ms";   // milliseconds
            }
            else if (input.EndsWith("s", StringComparison.InvariantCultureIgnoreCase))
            {
                units = "s";    // seconds
            }
            else if (input.EndsWith("m", StringComparison.InvariantCultureIgnoreCase))
            {
                units = "m";    // minutes
            }
            else if (input.EndsWith("h", StringComparison.InvariantCultureIgnoreCase))
            {
                units = "h";    // hours
            }
            else if (input.EndsWith("d", StringComparison.InvariantCultureIgnoreCase))
            {
                units = "d";    // days
            }

            if (units == null)
            {
                units = "s";
            }
            else
            {
                input = input.Substring(0, input.Length - units.Length);
            }

            if (!double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue))
            {
                goto tryStandard;
            }

            switch (units)
            {
                case "s":

                    value = TimeSpan.FromSeconds(doubleValue);
                    return true;

                case "m":

                    value = TimeSpan.FromMinutes(doubleValue);
                    return true;

                case "ms":

                    value = TimeSpan.FromMilliseconds(doubleValue);
                    return true;

                case "h":

                    value = TimeSpan.FromHours(doubleValue);
                    return true;

                case "d":

                    value = TimeSpan.FromDays(doubleValue);
                    return true;

                default:

                    throw new NotImplementedException($"Unexpected TimeSpan units [{units}].");
            }

            // Next, try to parse a standard TimeSpan string.

        tryStandard:

            if (TimeSpan.TryParse(input, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            error = $"Cannot parse timespan: {input}";

            return false;
        }

        /// <summary>
        /// Parses an enumeration.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="input">The input value.</param>
        /// <param name="value">Returns as the parsed value.</param>
        /// <param name="error">Returns as the error message on failure.</param>
        /// <returns>Returns <c>true</c> if the input value is valid.</returns>
        private static bool EnumParser<TEnum>(string input, out TEnum value, out string error)
            where TEnum : Enum
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(input), nameof(input));

            value = default(TEnum);
            error = null;

            if (NeonHelper.TryParseEnum(typeof(TEnum), input, out var objValue))
            {
                value = (TEnum)objValue;

                return true;
            }
            else
            {
                error = $"Cannot parse enum [{typeof(TEnum).FullName}]: {input}";

                return false;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private VariableSource  source;
        private INeonLogger     log;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="log">Optionally specifies the logger where parsing errors will be logged.</param>
        /// <param name="source">Optionally specifies an alternative variable source.  This defaults to retrieving environment variables.</param>
        public EnvironmentParser(INeonLogger log = null, VariableSource source = null)
        {
            this.log    = log;
            this.source = source ?? EnvironmentSource;
        }

        /// <summary>
        /// Used for (rare) situations where we need to start without a logger because we're
        /// using the <see cref="EnvironmentParser"/> to read the log settings first, create the
        /// logger and then initialize the logger parser here.
        /// </summary>
        /// <param name="log">The logger or <c>null</c> to disable logging.</param>
        public void SetLogger(INeonLogger log)
        {
            this.log = log;
        }

        /// <summary>
        /// Implements the default environment variable based source.
        /// </summary>
        /// <param name="variable">The variable name.</param>
        /// <returns>The variable value or <c>null</c>.</returns>
        private string EnvironmentSource(string variable)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(variable), nameof(variable));

            return Environment.GetEnvironmentVariable(variable);
        }

        /// <summary>
        /// Reports a missing environment variable.
        /// </summary>
        /// <param name="variable">The variable name.</param>
        private void LogMissingVariable(string variable)
        {
            if (log != null)
            {
                log.LogError(() => $"[{variable}] environment variable does not exist.");
            }
        }

        /// <summary>
        /// Reports an invalid environment variable.
        /// </summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="value">The invalid variable value.</param>
        /// <param name="error">Specifies an optional error message.</param>
        private void LogInvalidVariable(string variable, string value, string error = null)
        {
            error = error ?? "Environment variable could not be parsed.";

            if (error.EndsWith("."))
            {
                error += ".";
            }

            if (log != null)
            {
                log.LogError(() => $"[{variable}={value}]: {error}");
            }
        }

        /// <summary>
        /// Reports an invalid environment variable.
        /// </summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="value">The invalid variable value.</param>
        /// <param name="def">The value actually used instead.</param>
        /// <param name="error">Specifies an optional error message.</param>
        private void LogInvalidVariable(string variable, string value, object def, string error = null)
        {
            error = error ?? "Environment variable could not be parsed.";

            if (error.EndsWith("."))
            {
                error += ".";
            }

            if (log != null)
            {
                log.LogError(() => $"[{variable}={value}]: {error}  Defaulting to [{def}].");
            }
        }

        /// <summary>
        /// Reports an environment variable value.
        /// </summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="value">The invalid variable value.</param>
        /// <param name="redacted">Whether to redact the log output.</param>
        private void LogVariable(string variable, string value, bool redacted = false)
        {
            if (redacted)
            {
                value = "REDACTED";
            }

            if (log != null)
            {
                log.LogInfo(() => $"{variable}={value}");
            }
        }

        /// <summary>
        /// Throws a <see cref="KeyNotFoundException"/> for an environment variable.
        /// </summary>
        /// <param name="variable">The variable name.</param>
        private void ThrowNotFound(string variable)
        {
            throw new KeyNotFoundException($"Required environment variable [{variable}] does not exist.");
        }

        /// <summary>
        /// Attempts to parse an environment variable as a <typeparamref name="T"/>, writting 
        /// messages to the associated logger if one was passed to the constructor.
        /// </summary>
        /// <typeparam name="T">The parsed output type.</typeparam>
        /// <param name="variable">The variable name.</param>
        /// <param name="defaultInput">The default value.</param>
        /// <param name="required">Optionally specifies that the variable is required to exist.</param>
        /// <param name="parser">The parser function.</param>
        /// <param name="validator">Optional validation function.</param>
        /// <param name="redacted">Optionally redact log output of the variable.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the variable does not exists and <paramref name="required"/>=<c>true</c>.</exception>
        /// <exception cref="FormatException">Thrown if the variable could not be parsed or the <paramref name="validator"/> returned an error.</exception>
        public T Parse<T>(string variable, string defaultInput, Parser<T> parser, bool required = false, Validator<T> validator = null, bool redacted = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(variable), nameof(variable));
            Covenant.Requires<ArgumentNullException>(parser != null, nameof(parser));

            string error;

            // Verify that the default value can be parsed.

            if (!parser(defaultInput, out var defaultValue, out error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    error = "  " + error;
                }

                throw new FormatException($"Illegal default value [{defaultInput}] when parsing environment variable [{variable}].{error}");
            }

            // Fetch and parse the environment variable.

            var input = source(variable);

            if (input == null)
            {
                if (required)
                {
                    LogMissingVariable(variable);
                    ThrowNotFound(variable);
                }

                input = defaultInput;
            }

            LogVariable(variable, input, redacted);

            if (!parser(input, out var value, out error))
            {
                if (required)
                {
                    LogInvalidVariable(variable, input, error: error);
                }
                else
                {
                    LogInvalidVariable(variable, input, defaultInput, error: error);
                    value = defaultValue;
                }
            }

            // Validate the parsed value.

            if (validator != null)
            {
                if (!validator(value))
                {
                    const string invalidValue = "Invalid value.";

                    if (required)
                    {
                        LogInvalidVariable(variable, input, error: invalidValue);
                        ThrowNotFound(variable);
                    }
                    else
                    {
                        LogInvalidVariable(variable, input, defaultInput, error: invalidValue);
                    }
                }
            }

            return value;
        }

        /// <summary>
        /// Attempts to parse an environment variable as a <see cref="string"/>, writting messages
        /// to the associated logger if one was passed to the constructor.
        /// </summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="defaultInput">The default value.</param>
        /// <param name="required">Optionally specifies that the variable is required to exist.</param>
        /// <param name="validator">
        /// Optional validation function to be called to verify that the parsed variable
        /// value is valid.  This should return <c>null</c> for valid values and an error
        /// message for invalid ones.
        /// </param>
        /// <param name="redacted">Optionally redact log output of the variable.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the variable does not exists and <paramref name="required"/>=<c>true</c>.</exception>
        /// <exception cref="FormatException">Thrown if the variable could not be parsed or the <paramref name="validator"/> returned an error.</exception>
        public string Get(string variable, string defaultInput, bool required = false, Validator<string> validator = null, bool redacted = false)
        {
            return Parse<string>(variable, defaultInput, StringParser, required, validator, redacted);
        }

        /// <summary>
        /// Attempts to parse an environment variable as an <see cref="int"/>, writting messages
        /// to the associated logger if one was passed to the constructor.
        /// </summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="defaultInput">The default value.</param>
        /// <param name="required">Optionally specifies that the variable is required to exist.</param>
        /// <param name="validator">
        /// Optional validation function to be called to verify that the parsed variable
        /// value is valid.  This should return <c>null</c> for valid values and an error
        /// message for invalid ones.
        /// </param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the variable does not exists and <paramref name="required"/>=<c>true</c>.</exception>
        /// <exception cref="FormatException">Thrown if the variable could not be parsed or the <paramref name="validator"/> returned an error.</exception>
        public int Get(string variable, int defaultInput, bool required = false, Validator<int> validator = null)
        {
            return Parse<int>(variable, defaultInput.ToString(CultureInfo.InvariantCulture), IntParser, required, validator);
        }

        /// <summary>
        /// Attempts to parse an environment variable as a <see cref="long"/>, writting messages
        /// to the associated logger if one was passed to the constructor.
        /// </summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="defaultInput">The default value.</param>
        /// <param name="required">Optionally specifies that the variable is required to exist.</param>
        /// <param name="validator">
        /// Optional validation function to be called to verify that the parsed variable
        /// value is valid.  This should return <c>null</c> for valid values and an error
        /// message for invalid ones.
        /// </param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the variable does not exists and <paramref name="required"/>=<c>true</c>.</exception>
        /// <exception cref="FormatException">Thrown if the variable could not be parsed or the <paramref name="validator"/> returned an error.</exception>
        public long Get(string variable, long defaultInput, bool required = false, Validator<long> validator = null)
        {
            return Parse<long>(variable, defaultInput.ToString(CultureInfo.InvariantCulture), LongParser, required, validator);
        }

        /// <summary>
        /// Attempts to parse an environment variable as an <see cref="double"/>, writting messages
        /// to the associated logger if one was passed to the constructor.
        /// </summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="defaultInput">The default value.</param>
        /// <param name="required">Optionally specifies that the variable is required to exist.</param>
        /// <param name="validator">
        /// Optional validation function to be called to verify that the parsed variable
        /// value is valid.  This should return <c>null</c> for valid values and an error
        /// message for invalid ones.
        /// </param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the variable does not exists and <paramref name="required"/>=<c>true</c>.</exception>
        /// <exception cref="FormatException">Thrown if the variable could not be parsed or the <paramref name="validator"/> returned an error.</exception>
        public double Get(string variable, double defaultInput, bool required = false, Validator<double> validator = null)
        {
            return Parse<double>(variable, defaultInput.ToString(CultureInfo.InvariantCulture), DoubleParser, required, validator);
        }

        /// <summary>
        /// Attempts to parse an environment variable as a <see cref="bool"/>, writting messages
        /// to the associated logger if one was passed to the constructor.
        /// </summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="defaultInput">The default value.</param>
        /// <param name="required">Optionally specifies that the variable is required to exist.</param>
        /// <param name="validator">
        /// Optional validation function to be called to verify that the parsed variable
        /// value is valid.  This should return <c>null</c> for valid values and an error
        /// message for invalid ones.
        /// </param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the variable does not exists and <paramref name="required"/>=<c>true</c>.</exception>
        /// <exception cref="FormatException">Thrown if the variable could not be parsed or the <paramref name="validator"/> returned an error.</exception>
        public bool Get(string variable, bool defaultInput, bool required = false, Validator<bool> validator = null)
        {
            return Parse<bool>(variable, defaultInput.ToString(CultureInfo.InvariantCulture), BoolParser, required, validator);
        }

        /// <summary>
        /// Attempts to parse an environment variable as a <see cref="TimeSpan"/>, writting messages
        /// to the associated logger if one was passed to the constructor.
        /// </summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="defaultInput">The default value.</param>
        /// <param name="required">Optionally specifies that the variable is required to exist.</param>
        /// <param name="validator">
        /// Optional validation function to be called to verify that the parsed variable
        /// value is valid.  This should return <c>null</c> for valid values and an error
        /// message for invalid ones.
        /// </param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the variable does not exists and <paramref name="required"/>=<c>true</c>.</exception>
        /// <exception cref="FormatException">Thrown if the variable could not be parsed or the <paramref name="validator"/> returned an error.</exception>
        public TimeSpan Get(string variable, TimeSpan defaultInput, bool required = false, Validator<TimeSpan> validator = null)
        {
            return Parse<TimeSpan>(variable, defaultInput.TotalSeconds.ToString(CultureInfo.InvariantCulture) + "s", TimeSpanParser, required, validator);
        }

        /// <summary>
        /// Attempts to parse an environment variable as an eumeration, writting messages
        /// to the associated logger if one was passed to the constructor.
        /// </summary>
        /// <typeparam name="TEnum">The desired enumeration type.</typeparam>
        /// <param name="variable">The variable name.</param>
        /// <param name="defaultInput">The default value.</param>
        /// <param name="required">Optionally specifies that the variable is required to exist.</param>
        /// <param name="validator">
        /// Optional validation function to be called to verify that the parsed variable
        /// value is valid.  This should return <c>null</c> for valid values and an error
        /// message for invalid ones.
        /// </param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the variable does not exists and <paramref name="required"/>=<c>true</c>.</exception>
        /// <exception cref="FormatException">Thrown if the variable could not be parsed or the <paramref name="validator"/> returned an error.</exception>
        public TEnum Get<TEnum>(string variable, TEnum defaultInput, bool required = false, Validator<TEnum> validator = null)
            where TEnum : struct, Enum
        {
            return Parse<TEnum>(variable, NeonHelper.EnumToString(defaultInput), EnumParser, required, validator);
        }
    }
}
