//-----------------------------------------------------------------------------
// FILE:	    ModuleContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

namespace NeonCli.Ansible
{
    /// <summary>
    /// Module execution state.
    /// </summary>
    public class ModuleContext
    {
        private List<string>    output = new List<string>();
        private List<string>    errors = new List<string>();

        /// <summary>
        /// The module name.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string Module { get; set; }

        /// <summary>
        /// The output verbosity.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public AnsibleVerbosity Verbosity { get; set; } = AnsibleVerbosity.Important;

        /// <summary>
        /// The Ansible module arguments.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public JObject Arguments { get; set; }

        /// <summary>
        /// Indicates whether the model is being executed in Ansible <b>check mode</b>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool CheckMode { get; set; }

        /// <summary>
        /// The hive login.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public HiveLogin Login { get; set; }

        /// <summary>
        /// Initializes the Ansible module arguments.
        /// </summary>
        /// <param name="argsPath">Path to the Ansible arguments file.</param>
        public void SetArguments(string argsPath)
        {
            Arguments = JObject.Parse(File.ReadAllText(argsPath));

            if (Arguments.TryGetValue<int>("_ansible_verbosity", out var ansibleVerbosity))
            {
                this.Verbosity = (AnsibleVerbosity)ansibleVerbosity;
            }

            var checkMode = ParseBool("_ansible_check_mode");

            if (!checkMode.HasValue)
            {
                checkMode = false;
            }

            this.CheckMode = checkMode.Value;
        }

        //-----------------------------------------------------------------
        // These standard output fields are described here:
        //
        //      http://docs.ansible.com/ansible/latest/common_return_values.html
        //
        // Note that we're not currently implementing the INTERNAL properties.

        /// <summary>
        /// For those modules that implement backup=no|yes when manipulating files, a path to the backup file created.
        /// </summary>
        [JsonProperty(PropertyName = "backup_file", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public string BackupFile { get; set; } = null;

        /// <summary>
        /// A boolean indicating if the task had to make changes.
        /// </summary>
        [JsonProperty(PropertyName = "changed", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool Changed { get; set; } = false;

        /// <summary>
        /// A boolean that indicates if the task failed or not.
        /// </summary>
        [JsonProperty(PropertyName = "failed", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool Failed { get; set; } = false;

        /// <summary>
        /// A string with a generic message relayed to the user.
        /// </summary>
        [JsonProperty(PropertyName = "msg", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Some modules execute command line utilities or are geared
        /// for executing commands directly (raw, shell, command, etc), 
        /// this field contains <b>return code</b>k of these utilities.
        /// </summary>
        [JsonProperty(PropertyName = "rc", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int ReturnCode { get; set; } = 0;

        /// <summary>
        /// If this key exists, it indicates that a loop was present for the task 
        /// and that it contains a list of the normal module <b>result</b> per item.
        /// </summary>
        [JsonProperty(PropertyName = "results", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<ModuleContext> Results { get; set; } = null;

        /// <summary>
        /// A boolean that indicates if the task was skipped or not.
        /// </summary>
        [JsonProperty(PropertyName = "skipped", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool Skipped { get; set; } = false;

        /// <summary>
        /// Some modules execute command line utilities or are geared for executing 
        /// commands  directly (raw, shell, command, etc), this field contains the 
        /// error output of these utilities.
        /// </summary>
        [JsonProperty(PropertyName = "stderr", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string StdErr { get; set; } = string.Empty;

        /// <summary>
        /// When stdout is returned, Ansible always provides a list of strings, each
        /// containing one item per line from the original output.
        /// </summary>
        [JsonProperty(PropertyName = "stderr_lines", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> StdErrLines { get; set; } = null;

        /// <summary>
        /// Some modules execute command line utilities or are geared for executing 
        /// commands directly (raw, shell, command, etc). This field contains the
        /// normal output of these utilities.
        /// </summary>
        [JsonProperty(PropertyName = "stdout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string StdOut { get; set; } = string.Empty;

        /// <summary>
        /// When stdout is returned, Ansible always provides a list of strings, each
        /// containing one item per line from the original output.
        /// </summary>
        [JsonProperty(PropertyName = "stdout_lines", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> StdOutLines { get; set; } = null;

        /// <summary>
        /// Indicates whether one or more errors have been reported.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool HasErrors
        {
            get { return errors.Count > 0; }
        }

        /// <summary>
        /// Returns the first error line or <c>null</c>.
        /// </summary>
        public string GetFirstError()
        {
            return errors.FirstOrDefault();
        }

        /// <summary>
        /// Writes a line of text to the standard output lines.
        /// </summary>
        /// <param name="verbosity">The verbosity level for this message.</param>
        /// <param name="text">The text to be written.</param>
        public void WriteLine(AnsibleVerbosity verbosity, string text = null)
        {
            if (verbosity <= this.Verbosity)
            {
                output.Add(text ?? string.Empty);
            }
        }

        /// <summary>
        /// Writes a line of text to the standard error lines.
        /// </summary>
        /// <param name="text">The text to be written.</param>
        public void WriteErrorLine(string text = null)
        {
            errors.Add(text ?? string.Empty);
        }

        /// <summary>
        /// Appends a line of text to the <b>ansible-debug.log</b> file in the
        /// current directory for development and debugging purposes.
        /// </summary>
        /// <param name="line">The line of text to be logged.</param>
        public void LogDebug(string line = null)
        {
            line = line ?? string.Empty;

            File.AppendAllText("ansible-debug.log", line + Environment.NewLine);
        }

        /// <summary>
        /// Renders the instance as a JSON string.
        /// </summary>
        public override string ToString()
        {
            // Set [StdErrLines] and [StdOutLines] if necessary.

            if (!string.IsNullOrEmpty(StdErr))
            {
                StdErrLines = StdErr.ToLines().ToList();
            }
            else if (errors.Count > 0)
            {
                StdErrLines = errors;
            }

            if (!string.IsNullOrEmpty(StdOut))
            {
                StdOutLines = StdOut.ToLines().ToList();
            }
            else if (output.Count > 0)
            {
                StdOutLines = output;
            }

            return NeonHelper.JsonSerialize(this, Formatting.Indented);
        }

        //---------------------------------------------------------------------
        // Parsing helpers

        /// <summary>
        /// Attempts to parse a boolean value.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="errorMessage">The optional context error message to log when the input is not valid.</param>
        /// <returns>The parsed value or <c>null</c> if the input was <c>null</c> or invalid.</returns>
        public bool? ParseBoolValue(string input, string errorMessage = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            try
            {
                return NeonHelper.ParseBool(input);
            }
            catch (FormatException)
            {
                if (errorMessage != null)
                {
                    WriteErrorLine(errorMessage);
                }

                return null;
            }
        }

        /// <summary>
        /// Attempts to parse an <c>int</c> value.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="errorMessage">The optional context error message to log when the input is not valid.</param>
        /// <returns>The parsed value or <c>null</c> if the input was <c>null</c> or invalid.</returns>
        public int? ParseIntValue(string input, string errorMessage = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            if (int.TryParse(input, out var value))
            {
                return value;
            }
            else
            {
                if (errorMessage != null)
                {
                    WriteErrorLine(errorMessage);
                }

                return null;
            }
        }

        /// <summary>
        /// Attempts to parse a <c>long</c> value.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="errorMessage">The optional context error message to log when the input is not valid.</param>
        /// <returns>The parsed value or <c>null</c> if the input was <c>null</c> or invalid.</returns>
        public long? ParseLongValue(string input, string errorMessage = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            if (long.TryParse(input, out var value))
            {
                return value;
            }
            else
            {
                if (errorMessage != null)
                {
                    WriteErrorLine(errorMessage);
                }

                return null;
            }
        }

        /// <summary>
        /// Attempts to parse an enumeration value.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="errorMessage">The optional context error message to log when the input is not valid.</param>
        /// <returns>The parsed value or <c>null</c> if the input was <c>null</c> or invalid.</returns>
        public TEnum? ParseEnumValue<TEnum>(string input, string errorMessage = null)
            where TEnum : struct
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            if (!NeonHelper.TryParse<TEnum>(input, out var value))
            {
                if (errorMessage != null)
                {
                    WriteErrorLine(errorMessage);
                }

                return null;
            }
            else
            {
                return value;
            }
        }

        /// <summary>
        /// Attempts to parse an enumeration value.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <param name="errorMessage">The optional context error message to log when the input is not valid.</param>
        /// <returns>The parsed value or <paramref name="defaultValue"/> if the input was <c>null</c> or invalid.</returns>
        public TEnum? ParseEnumValue<TEnum>(string input, TEnum defaultValue, string errorMessage = null)
            where TEnum : struct
        {
            if (string.IsNullOrEmpty(input))
            {
                return defaultValue;
            }

            if (!NeonHelper.TryParse<TEnum>(input, out var value))
            {
                if (errorMessage != null)
                {
                    WriteErrorLine(errorMessage);
                }

                return defaultValue;
            }
            else
            {
                return value;
            }
        }

        /// <summary>
        /// Parses a boolean argument.
        /// </summary>>
        /// <param name="argName">The argument name.</param>
        /// <returns>The parsed boolean or <c>null</c>.</returns>
        public bool? ParseBool(string argName)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            var value = ParseBoolValue((string)jToken);

            if (!value.HasValue)
            {
                WriteErrorLine($"[{argName}] is not a valid boolean.");
            }

            return value;
        }

        /// <summary>
        /// Parses a <c>string</c> argument.
        /// </summary>>
        /// <param name="argName">The argument name.</param>
        /// <param name="validator">Optional validation function.</param>
        /// <returns>The parsed string or <c>null</c>.</returns>
        public string ParseString(string argName, Func<string, bool> validator = null)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            var value = (string)jToken;

            if (validator != null && !validator(value))
            {
                WriteErrorLine($"[{argName}={value}] is not valid.");
            }

            return value;
        }

        /// <summary>
        /// Parses an <c>int</c> argument.
        /// </summary>>
        /// <param name="argName">The argument name.</param>
        /// <param name="validator">Optional validation function.</param>
        /// <returns>The parsed integer or <c>null</c>.</returns>
        public int? ParseInt(string argName, Func<int, bool> validator = null)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            try
            {
                var valueString = (string)jToken;
                var value       = int.Parse(valueString);

                if (validator != null && !validator(value))
                {
                    WriteErrorLine($"[{argName}={value}] is not valid.");
                }

                return value;
            }
            catch
            {
                WriteErrorLine($"[{argName}] is not a valid integer.");
                return null;
            }
        }

        /// <summary>
        /// Parses a <c>long</c> argument.
        /// </summary>>
        /// <param name="argName">The argument name.</param>
        /// <param name="validator">Optional validation function.</param>
        /// <returns>The parsed integer or <c>null</c>.</returns>
        public long? ParseLong(string argName, Func<long, bool> validator = null)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            try
            {
                var valueString = (string)jToken;
                var value       = int.Parse(valueString);

                if (validator != null && !validator(value))
                {
                    WriteErrorLine($"[{argName}={value}] is not valid.");
                }

                return value;
            }
            catch
            {
                WriteErrorLine($"[{argName}] is not a valid integer.");
                return null;
            }
        }

        /// <summary>
        /// Parses a <c>double</c> argument.
        /// </summary>>
        /// <param name="argName">The argument name.</param>
        /// <param name="validator">Optional validation function.</param>
        /// <returns>The parsed double or <c>null</c>.</returns>
        public double? ParseDouble(string argName, Func<double, bool> validator = null)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            try
            {
                var valueString = (string)jToken;
                var value       = double.Parse(valueString);

                if (validator != null && !validator(value))
                {
                    WriteErrorLine($"[{argName}={value}] is not valid.");
                }

                return value;
            }
            catch
            {
                WriteErrorLine($"[{argName}] is not a valid double.");
                return null;
            }
        }

        /// <summary>
        /// Parses an enumeration argument returning <c>null</c> if the
        /// property doesn't exist or is invalid.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="argName">The argument name.</param>
        /// <returns>The enumeration value or <c>null</c>.</returns>
        public TEnum? ParseEnum<TEnum>(string argName)
            where TEnum : struct
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            try
            {
                var valueString = (string)jToken;

                if (NeonHelper.TryParse<TEnum>(valueString, out var value))
                {
                    return value;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                WriteErrorLine($"[{argName}] is not a valid [{typeof(TEnum).Name}].");
                return null;
            }
        }

        /// <summary>
        /// Parses an enumeration argument returning a default value if the
        /// property doesn't exist or is invalid.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="argName">The argument name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>
        /// The parsed enumeration value or <paramref name="defaultValue"/>
        /// if the property doesn't exist or is invalid.
        /// </returns>
        public TEnum ParseEnum<TEnum>(string argName, TEnum defaultValue)
            where TEnum : struct
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return defaultValue;
            }

            try
            {
                var valueString = (string)jToken;

                try
                {
                    return (TEnum)NeonHelper.ParseEnum<TEnum>(valueString);
                }
                catch
                {
                    WriteErrorLine($"[{argName}={valueString}] is not a valid [{typeof(TEnum).Name}].");
                    return defaultValue;
                }
            }
            catch
            {
                WriteErrorLine($"[{argName}] is not a valid [{typeof(TEnum).Name}].");
                return defaultValue;
            }
        }

        /// <summary>
        /// Parses a Docker time interval.
        /// </summary>
        /// <param name="argName">The argument name.</param>
        /// <returns>The parsed duration in nanoseconds or <c>null</c>.</returns>
        public long? ParseDockerInterval(string argName)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            try
            {
                var orgValue = (string)jToken;
                var value    = orgValue;
                var units    = 1000000000L;    // default unit: 1s = 1000000000ns

                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                if (value.EndsWith("ns", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1L;
                    value = value.Substring(0, value.Length - 2);
                }
                else if (value.EndsWith("us", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1000L;
                    value = value.Substring(0, value.Length - 2);
                }
                else if (value.EndsWith("ms", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1000000L;
                    value = value.Substring(0, value.Length - 2);
                }
                else if (value.EndsWith("s", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1000000000L;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (value.EndsWith("m", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 60 * 1000000000L;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (value.EndsWith("h", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 60 * 60 * 1000000000L;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (!char.IsDigit(value.Last()))
                {
                    WriteErrorLine($"[{argName}={orgValue}] has an unexpected unit.");
                    return null;
                }

                if (long.TryParse(value, out var time))
                {
                    if (time < 0)
                    {
                        WriteErrorLine($"[{argName}={orgValue}] cannot be negative.");
                        return null;
                    }

                    return time * units;
                }
                else
                {
                    WriteErrorLine($"[{argName}={orgValue}] is not a valid duration.");
                    return null;
                }
            }
            catch
            {
                WriteErrorLine($"[{argName}] cannot be converted into a time period.");
                return null;
            }
        }

        /// <summary>
        /// Parses a Docker memory size.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="errorMessage">The optional context error message to log when the input is not valid.</param>
        /// <returns>The parsed value or <c>null</c> if the input was <c>null</c> or invalid.</returns>
        public long? ParseDockerByteSizeValue(string input, string errorMessage = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            try
            {
                var units = 1L;    // default unit is 1 byte

                if (input.EndsWith("b", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1L;
                    input = input.Substring(0, input.Length - 1);
                }
                else if (input.EndsWith("k", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = (long)NeonHelper.Kilo;
                    input = input.Substring(0, input.Length - 1);
                }
                else if (input.EndsWith("m", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = (long)NeonHelper.Mega;
                    input = input.Substring(0, input.Length - 1);
                }
                else if (input.EndsWith("g", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = (long)NeonHelper.Giga;
                    input = input.Substring(0, input.Length - 1);
                }
                else if (!char.IsDigit(input.Last()))
                {
                    WriteErrorLine($"[{input}] has an unexpected unit.");
                    return null;
                }

                if (long.TryParse(input, out var size))
                {
                    if (size < 0)
                    {
                        WriteErrorLine($"[{input}] cannot be negative.");
                        return null;
                    }

                    return size * units;
                }
                else
                {
                    if (errorMessage != null)
                    {
                        WriteErrorLine(errorMessage);
                    }

                    return null;
                }
            }
            catch
            {
                if (errorMessage != null)
                {
                    WriteErrorLine(errorMessage);
                }
                
                return null;
            }
        }

        /// <summary>
        /// Parses a Docker memory size.
        /// </summary>
        /// <param name="argName">The argument name.</param>
        /// <returns>The parsed memory size in bytes.</returns>
        public long? ParseDockerByteSize(string argName)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            try
            {
                var orgValue = (string)jToken;
                var value    = orgValue;
                var units    = 1L;    // default unit is 1 byte

                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                if (value.EndsWith("b", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1L;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (value.EndsWith("k", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = (long)NeonHelper.Kilo;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (value.EndsWith("m", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = (long)NeonHelper.Mega;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (value.EndsWith("g", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = (long)NeonHelper.Giga;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (!char.IsDigit(value.Last()))
                {
                    WriteErrorLine($"[{argName}={orgValue}] has an unexpected unit.");
                    return null;
                }

                if (long.TryParse(value, out var size))
                {
                    if (size < 0)
                    {
                        WriteErrorLine($"[{argName}={orgValue}] cannot be negative.");
                        return null;
                    }

                    return size * units;
                }
                else
                {
                    WriteErrorLine($"[{argName}={orgValue}] is not a valid memory size.");
                    return null;
                }
            }
            catch
            {
                WriteErrorLine($"[{argName}] cannot be converted into a memory size.");
                return null;
            }
        }

        /// <summary>
        /// Parses an argument as a string array.
        /// </summary>
        /// <param name="argName">The argument name.</param>
        public List<String> ParseStringArray(string argName)
        {
            var array = new List<string>();

            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return array;
            }

            var jArray = jToken as JArray;

            if (jArray == null)
            {
                WriteErrorLine($"[{argName}] is not an array.");
                return array;
            }

            foreach (var item in jArray)
            {
                try
                {
                    array.Add((string)item);
                }
                catch
                {
                    WriteErrorLine($"[{argName}] array as one or more invalid elements.");
                    return array;
                }
            }

            return array;
        }

        /// <summary>
        /// Parses an argument as an <see cref="IPAddress"/> array.
        /// </summary>
        /// <param name="argName">The argument name.</param>
        public List<IPAddress> ParseIPAddressArray(string argName)
        {
            var array       = new List<IPAddress>();
            var stringArray = ParseStringArray(argName);

            foreach (var item in stringArray)
            {
                if (IPAddress.TryParse(item, out var address))
                {
                    array.Add(address);
                }
                else
                {
                    WriteErrorLine($"[{argName}] is includes invalid IP address [{item}].");
                }
            }

            return array;
        }

        /// <summary>
        /// Verifies that all of a <see cref="JObject"/>'s properties are well-known.
        /// </summary>
        /// <param name="jObject">The object being tested.</param>
        /// <param name="validNames">The set of valid property names.</param>
        /// <param name="prefix">The optional prefix to be used when reporting invalid properties.</param>
        /// <returns><c>true</c> if the object property names are all valid.</returns>
        public bool ValidateArguments(JObject jObject, HashSet<string> validNames, string prefix = null)
        {
            Covenant.Requires<ArgumentNullException>(jObject != null);
            Covenant.Requires<ArgumentNullException>(validNames != null);

            var valid = true;

            foreach (var property in jObject.Properties())
            {
                if (property.Name.StartsWith("_"))
                {
                    // Ansible built-in arguments begin with "_".  Ignore these.

                    continue;
                }

                if (!validNames.Contains(property.Name))
                {
                    valid = false;

                    if (string.IsNullOrEmpty(prefix))
                    {
                        WriteErrorLine($"Unknown module argument: {property.Name}");
                    }
                    else
                    {
                        WriteErrorLine($"Unknown module argument: {prefix}.{property.Name}");
                    }
                }
            }

            return valid;
        }
    }
}
