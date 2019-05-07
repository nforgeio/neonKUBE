//-----------------------------------------------------------------------------
// FILE:	    ProxyMessage.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Collections;
using Neon.Data;

// $todo(jeff.lill)
//
// Performance could be improved by maintaining output stream and buffer pools
// rather than allocating these every time.
//
// We should also try to convert the serialize/deserialize methods to be async
// and work on streams.

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// The base class for all messages transferred between the .NET Cadence client
    /// and the <b>cadence-proxy</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is designed to be a very simple and flexible way of communicating
    /// operations and status between the Cadence client and proxy.  The specific 
    /// message type is identified via the <see cref="Type"/> property (one of the 
    /// <see cref="MessageTypes"/> values.  The <see cref="Properties"/> dictionary will be
    /// used to pass named values.  Binary attachments may be passed using the 
    /// <see cref="Attachments"/> property, a list of binary arrays.
    /// </para>
    /// <para>
    /// This is serialized to bytes using a simple structure consisting of 32-bit
    /// integers, UTF-8 encoded strings, and raw bytes with all integers encoded 
    /// using little-endian byte ordering.  Strings are encoded as a 32-bit 
    /// byte length, followed by that many UTF-8 encoded string bytes.  A ZERO
    /// byte length indicates an empty string and a length of -1 indicates a
    /// NULL string.  Encoded strings will look like:
    /// </para>
    /// <code>
    /// +------------------+
    /// |      LENGTH      |   32-bit (little endian)
    /// +------------------+
    /// |                  |
    /// |      UTF-8       |
    /// |      BYTES       |
    /// |                  |
    /// +------------------+
    /// </code>
    /// <para>
    /// A full encoded message will look like:
    /// </para>
    /// <code>
    /// +------------------+
    /// |   MESSAGE-TYPE   |   32-bit
    /// +------------------+
    /// |   PROPERTY-COUNT |   32-bit
    /// +------------------+
    /// |                  |
    /// |  +------------+  |
    /// |  |   NAME     |  |
    /// |  +------------+  |
    /// |  |   VALUE    |  |
    /// |  +------------+  |
    /// |       ...        |
    /// |                  |
    /// +------------------+
    /// |   ATTACH-COUNT   |   32-bit
    /// +------------------+
    /// |                  |
    /// |  +------------+  |
    /// |  |   LENGTH   |  |   32-bit
    /// |  +------------+  |
    /// |  |            |  |
    /// |  |            |  |
    /// |  |   BYTES    |  |
    /// |  |            |  |
    /// |  |            |  |
    /// |  +------------+  |
    /// |       ...        |
    /// |                  |
    /// +------------------+
    /// </code>
    /// <para>
    /// The message starts out with the 32-bit message type followed by the
    /// number of properties to follow.  Each argument consists of an encoded
    /// string for the argument name followed by an encoded string for the value.
    /// </para>
    /// <para>
    /// After the properties will be a 32-bit integer specifying the
    /// number of binary attachment with each encoded as its length in bytes
    /// followed by that actual attachment bytes.  An attachment with length
    /// set to -1 will be considered to be NULL.
    /// </para>
    /// <para>
    /// Proxy messages will be passed between the Cadence client and proxy
    /// via <b>PUT</b> requests using the <b>application/x-neon-cadence-proxy</b>
    /// content-type.  Note that request responses in both directions never
    /// include any content.
    /// </para>
    /// <para>
    /// Note that more complex message property may be passed as JSON strings
    /// that can be serialized and deserialized via the <see cref="GetJsonProperty{T}(string)"/>
    /// and <see cref="SetJsonProperty{T}(string, T)"/> helper methods.
    /// </para>
    /// </remarks>
    [ProxyMessage(MessageTypes.Unspecified)]
    internal class ProxyMessage
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The content type to used for HTTP requests encapsulating a <see cref="ProxyMessage"/>.
        /// </summary>
        public const string ContentType = "application/x-neon-cadence-proxy";

        // Maps the integer message type codes to the associated .NET message type.
        // This is constructed via reflection within the static constructor.
        //
        // Note that this will not be modified after that static constructor
        // initializes it, so subsequent access will be threadsafe without
        // any additional locking.
        private static Dictionary<int, Type> intToMessageClass;

        // This referernces the [Neon.Cadence] assembly.
        private static Assembly cadenceAssembly;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static ProxyMessage()
        {
            cadenceAssembly = Assembly.GetExecutingAssembly();

            // Scan the [Neon.Cadence] assembly for proxy message classes tagged by
            // [ProxyMessage(MessageType)] to build a dictionary mapping message type
            // enumeration values to the corresponding implementation type.
            //
            // We'll use this table below to deserialize the correct type.

            intToMessageClass = new Dictionary<int, Type>();

            foreach (var messageClass in cadenceAssembly.GetTypes())
            {
                var attribute = messageClass.GetCustomAttribute<ProxyMessageAttribute>();

                if (attribute != null)
                {
                    var typeCode = (int)attribute.Type;

                    if (typeCode <= 0)
                    {
                        continue;   // Ignore non-positive type codes.
                    }

                    if (intToMessageClass.TryGetValue(typeCode, out var conflict))
                    {
                        throw new Exception($"Message types [{conflict.FullName}] and [{messageClass.FullName}] conflict because they share the same message type code [{typeCode}].");
                    }

                    intToMessageClass.Add(typeCode, messageClass);
                }
            }
        }

        /// <summary>
        /// Deserializes the message from a stream.
        /// </summary>
        /// <typeparam name="TMessage">The expected message type.</typeparam>
        /// <param name="input">The input stream.</param>
        /// <param name="ignoreTypeCode">Optionally ignore unspecified message types (used for unit testing).</param>
        /// <returns>The decoded message.</returns>
        public static TMessage Deserialize<TMessage>(Stream input, bool ignoreTypeCode = false)
            where TMessage : ProxyMessage, new()
        {
            using (var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true))
            {
                // Read the message type and create a message instance of the specified type.

                Type messageClass;

                var messageType = (MessageTypes)reader.ReadInt32();

                if (!ignoreTypeCode)
                {
                    if (!intToMessageClass.TryGetValue((int)messageType, out messageClass))
                    {
                        throw new FormatException($"Unexpected message type [{messageType}].");
                    }
                }
                else
                {
                    messageClass = typeof(TMessage);
                }

                ProxyMessage message;

                try
                {
                    message = (ProxyMessage)Activator.CreateInstance(messageClass, null);

                    // Read the properties.

                    var argCount = reader.ReadInt32();

                    for (int i = 0; i < argCount; i++)
                    {
                        var name  = ReadString(reader);
                        var value = ReadString(reader);

                        message.Properties.Add(name, value);
                    }

                    // Read the attachments.

                    var attachCount = reader.ReadInt32();

                    for (int i = 0; i < attachCount; i++)
                    {
                        var length = reader.ReadInt32();

                        if (length == -1)
                        {
                            message.Attachments.Add(null);
                        }
                        else if (length == 0)
                        {
                            message.Attachments.Add(new byte[0]);
                        }
                        else
                        {
                            message.Attachments.Add(reader.ReadBytes(length));
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new FormatException("Message deserialzation failed", e);
                }

                var result = message as TMessage;

                if (result == null)
                {
                    throw new InvalidCastException($"Serialized message with [typecode={(int)messageType}] cannot be deserialized as a [{typeof(TMessage).FullName}].");
                }

                return result;
            }
        }

        /// <summary>
        /// Deserialzes a string.
        /// </summary>
        /// <param name="reader">The input reader.</param>
        /// <returns></returns>
        private static string ReadString(BinaryReader reader)
        {
            var length = reader.ReadInt32();

            if (length == -1)
            {
                return null;
            }
            else if (length == 0)
            {
                return string.Empty;
            }
            else
            {
                return Encoding.UTF8.GetString(reader.ReadBytes(length));
            }
        }

        /// <summary>
        /// Serialize a string.
        /// </summary>
        /// <param name="writer">The output writer.</param>
        /// <param name="value">The string being serialized.</param>
        private static void WriteString(BinaryWriter writer, string value)
        {
            if (value == null)
            {
                writer.Write(-1);
            }
            else
            {
                writer.Write(value.Length);
                writer.Write(Encoding.UTF8.GetBytes(value));
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProxyMessage()
        {
        }

        /// <summary>
        /// Indicates the message type, one of the <see cref="MessageTypes"/> values.
        /// </summary>
        public MessageTypes Type { get; set; }

        /// <summary>
        /// Returns a case insensitive dictionary that maps argument names to value strings.
        /// </summary>
        public NiceDictionary<string, string> Properties { get; private set; } = new NiceDictionary<string, string>();

        /// <summary>
        /// Returns the list of binary attachments.
        /// </summary>
        public List<byte[]> Attachments { get; private set; } = new List<byte[]>();

        /// <summary>
        /// Serializes the message to bytes.
        /// </summary>
        /// <param name="ignoreTypeCode">Optionally ignore unspecified message types (used for unit testing).</param>
        /// <returns>The serialized byte array.</returns>
        public byte[] Serialize(bool ignoreTypeCode = false)
        {
            if (!ignoreTypeCode && Type == MessageTypes.Unspecified)
            {
                throw new ArgumentException($"Message type [{this.GetType().FullName}] has not initialized its [{nameof(Type)}] property.");
            }

            using (var output = new MemoryStream())
            {
                using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write((int)Type);

                    // Write the properties.

                    writer.Write(Properties.Count);

                    foreach (var arg in Properties)
                    {
                        WriteString(writer, arg.Key);
                        WriteString(writer, arg.Value);
                    }

                    // Write the attachments.

                    writer.Write(Attachments.Count);

                    foreach (var attachment in Attachments)
                    {
                        if (attachment == null)
                        {
                            writer.Write(-1);
                        }
                        else
                        {
                            writer.Write(attachment.Length);
                            writer.Write(attachment);
                        }
                    }
                }

                return output.ToArray();
            }
        }

        /// <summary>
        /// Implemented by derived classes to make a copy of themselves for echo testing
        /// purposes.  Note that this is not implemented for the base <see cref="ProxyMessage"/>
        /// class.
        /// </summary>
        /// <returns>The cloned message.</returns>
        /// <exception cref="NotImplementedException">Thrown by this base class.</exception>
        internal virtual ProxyMessage Clone()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Implemented by derived classes to copy message properties to another
        /// message instance during a <see cref="Clone()"/> operation.
        /// </summary>
        /// <param name="target">The target message.</param>
        /// <remarks>
        /// <note>
        /// The method implementation can safely assume that the <paramref name="target"/>
        /// message can be cast into the implementation's message type.
        /// </note>
        /// </remarks>
        protected virtual void CopyTo(ProxyMessage target)
        {
        }

        //---------------------------------------------------------------------
        // Helper methods derived classes can use for retrieving typed message properties.

        /// <summary>
        /// Helper method for retrieving a string property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="def">The default value to be returned if the named property doesn't exist.</param>
        /// <returns>The string value.</returns>
        internal string GetStringProperty(string key, string def = null)
        {
            if (Properties.TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                return def;
            }
        }

        /// <summary>
        /// Helper method for retrieving a 32-bit integer property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="def">The default value to be returned if the named property doesn't exist.</param>
        /// <returns>The integer value.</returns>
        internal int GetIntProperty(string key, int def = 0)
        {
            if (Properties.TryGetValue(key, out var value))
            {
                return int.Parse(value);
            }
            else
            {
                return def;
            }
        }

        /// <summary>
        /// Helper method for retrieving a 64-bit integer property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="def">The default value to be returned if the named property doesn't exist.</param>
        /// <returns>The long value.</returns>
        internal long GetLongProperty(string key, long def = 0)
        {
            if (Properties.TryGetValue(key, out var value))
            {
                return long.Parse(value);
            }
            else
            {
                return def;
            }
        }

        /// <summary>
        /// Helper method for retrieving a boolean property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="def">The default value to be returned if the named property doesn't exist.</param>
        /// <returns>The long value.</returns>
        internal bool GetBoolProperty(string key, bool def = false)
        {
            if (Properties.TryGetValue(key, out var value))
            {
                return NeonHelper.ParseBool(value);
            }
            else
            {
                return def;
            }
        }

        /// <summary>
        /// Helper method for retrieving an enumeration property.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="key">The property key.</param>
        /// <param name="def">The default value to be returned if the named property doesn't exist.</param>
        /// <returns>The long value.</returns>
        internal TEnum GetEnumProperty<TEnum>(string key, TEnum def = default(TEnum))
            where TEnum : struct
        {
            if (Properties.TryGetValue(key, out var value))
            {
                return NeonHelper.ParseEnum<TEnum>(value, def);
            }
            else
            {
                return def;
            }
        }

        /// <summary>
        /// Helper method for retrieving a double property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="def">The default value to be returned if the named property doesn't exist.</param>
        /// <returns>The double value.</returns>
        internal double GetDoubleProperty(string key, double def = 0.0)
        {
            if (Properties.TryGetValue(key, out var value))
            {
                return double.Parse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowExponent, CultureInfo.InvariantCulture);
            }
            else
            {
                return def;
            }
        }

        /// <summary>
        /// Helper method for retrieving a date/time property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="def">The default value to be returned if the named property doesn't exist.</param>
        /// <returns>The double value.</returns>
        internal DateTime GetDateTimeProperty(string key, DateTime def = default)
        {
            if (Properties.TryGetValue(key, out var value))
            {
                return DateTime.ParseExact(value, NeonHelper.DateFormatTZ, CultureInfo.InvariantCulture).ToUniversalTime();
            }
            else
            {
                return def;
            }
        }

        /// <summary>
        /// Helper method for retrieving a timespan property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="def">The default value to be returned if the named property doesn't exist.</param>
        /// <returns>The double value.</returns>
        internal TimeSpan GetTimeSpanProperty(string key, TimeSpan def = default)
        {
            if (Properties.TryGetValue(key, out var value))
            {
                var ticks = long.Parse(value, CultureInfo.InvariantCulture);

                return TimeSpan.FromTicks(ticks);
            }
            else
            {
                return def;
            }
        }

        /// <summary>
        /// Helper method for retrieving a complex property serialized as a JSON string.
        /// </summary>
        /// <typeparam name="T">The property type.</typeparam>
        /// <param name="key">The property key.</param>
        /// <returns>The parsed value if the property exists or <c>null</c>.</returns>
        internal T GetJsonProperty<T>(string key)
            where T : class, new()
        {
            if (Properties.TryGetValue(key, out var value))
            {
                if (value == null)
                {
                    return null;
                }

                if (typeof(T).Implements<IRoundtripData>())
                {
                    return RoundtripDataFactory.CreateFrom<T>(JObject.Parse(value));
                }
                else
                {
                    return NeonHelper.JsonDeserialize<T>(value, strict: true);
                }
            }
            else
            {
                return null;
            }
        }

        //---------------------------------------------------------------------
        // Helper methods derived classes can use for setting typed message properties.

        /// <summary>
        /// Helper method for setting a string property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetStringProperty(string key, string value)
        {
            Properties[key] = value;
        }

        /// <summary>
        /// Helper method for setting a 32-bit integer property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetIntProperty(string key, int value)
        {
            Properties[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Helper method for setting a 64-bit integer property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetLongProperty(string key, long value)
        {
            Properties[key] = value.ToString();
        }

        /// <summary>
        /// Helper method for setting a boolean property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetBoolProperty(string key, bool value)
        {
            Properties[key] = NeonHelper.ToBoolString(value);
        }

        /// <summary>
        /// Helper method for setting an enumeration property.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetEnumProperty<TEnum>(string key, TEnum value)
            where TEnum : struct
        {
            Properties[key] = NeonHelper.EnumToString(value);
        }

        /// <summary>
        /// Helper method for setting a double property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetDoubleProperty(string key, double value)
        {
            Properties[key] = value.ToString("G") ;
        }

        /// <summary>
        /// Helper method for setting a date/time property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetDateTimeProperty(string key, DateTime value)
        {
            Properties[key] = value.ToString(NeonHelper.DateFormatTZ);
        }

        /// <summary>
        /// Helper method for setting a timespan property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetTimeSpanProperty(string key, TimeSpan value)
        {
            Properties[key] = value.Ticks.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Helper method for setting a complex property as JSON.
        /// </summary>
        /// <typeparam name="T">The property type.</typeparam>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetJsonProperty<T>(string key, T value)
            where T : class, new()
        {
            string json;

            if (value == null)
            {
                json = null;
            }
            else
            {
                var roundtrip = value as IRoundtripData;

                if (roundtrip != null)
                {
                    json = roundtrip.ToString();
                }
                else
                {
                    json = NeonHelper.JsonSerialize(value);
                }
            }

            Properties[key] = json;
        }
    }
}
