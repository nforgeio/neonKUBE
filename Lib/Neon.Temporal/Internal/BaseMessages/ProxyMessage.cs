//-----------------------------------------------------------------------------
// FILE:	    ProxyMessage.cs
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
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Collections;
using Neon.Data;
using Neon.Temporal;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// The base class for all messages transferred between the .NET Temporal client
    /// and the <b>temporal-proxy</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is designed to be a very simple and flexible way of communicating
    /// operations and status between the Temporal client and proxy.  The specific 
    /// message type is identified via the <see cref="Type"/> property (one of the 
    /// <see cref="InternalMessageTypes"/> values.  The <see cref="Properties"/> dictionary will be
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
    /// Proxy messages will be passed between the Temporal client and proxy
    /// via <b>PUT</b> requests using the <b>application/x-neon-temporal-proxy</b>
    /// content-type.  Note that request responses in both directions never
    /// include any content.
    /// </para>
    /// <para>
    /// Note that more complex message property may be passed as JSON strings
    /// that can be serialized and deserialized via the <see cref="GetJsonProperty{T}(PropertyNameUtf8)"/>
    /// and <see cref="SetJsonProperty{T}(PropertyNameUtf8, T)"/> helper methods.
    /// </para>
    /// </remarks>
    [InternalProxyMessage(InternalMessageTypes.Unspecified)]
    internal class ProxyMessage
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// The return value of <see cref="ReadPropertyNameBytes(BinaryReader)"/>.
        /// </summary>
        private ref struct PropertyNameBytes
        {
            public byte[]       Bytes;
            public Span<byte>   Span;
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The content type to used for HTTP requests encapsulating a <see cref="ProxyMessage"/>.
        /// </summary>
        public const string ContentType = "application/x-neon-temporal-proxy";

        // Maps the integer message type codes to the associated .NET message type.
        // This is constructed via reflection within the static constructor.
        //
        // Note that this will not be modified after that static constructor
        // initializes it, so subsequent access will be threadsafe without
        // any additional locking.
        private static Dictionary<int, Type> intToMessageClass;

        // This referernces the [Neon.Temporal] assembly.
        private static Assembly temporalAssembly;

        // Used to pool UTF-8 encoded byte arrays used to deserialize message
        // property names.
        private static ArrayPool<byte> bufferPool = ArrayPool<byte>.Shared;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static ProxyMessage()
        {
            temporalAssembly = Assembly.GetExecutingAssembly();

            // Scan the [Neon.Temporal] assembly for proxy message classes tagged by
            // [ProxyMessage(MessageType)] to build a dictionary mapping message type
            // enumeration values to the corresponding implementation type.
            //
            // We'll use this table below to deserialize the correct type.

            intToMessageClass = new Dictionary<int, Type>();

            foreach (var messageClass in temporalAssembly.GetTypes())
            {
                var attribute = messageClass.GetCustomAttribute<InternalProxyMessageAttribute>();

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

                var messageType = (InternalMessageTypes)reader.ReadInt32();

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
                        var propertyNameBytes = ReadPropertyNameBytes(reader);

                        try
                        {
                            var value = ReadString(reader);

                            message.Properties.Add(PropertyNames.Lookup(propertyNameBytes.Span), value);
                        }
                        finally
                        {
                            bufferPool.Return(propertyNameBytes.Bytes);
                        }
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
                            message.Attachments.Add(Array.Empty<byte>());
                        }
                        else
                        {
                            message.Attachments.Add(reader.ReadBytes(length));
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new FormatException("Message deserialization failed", e);
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
        /// <returns>The string.</returns>
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
                var bytes = bufferPool.Rent(length);

                try
                {
                    reader.Read(bytes, 0, length);

                    return Encoding.UTF8.GetString(bytes, 0, length);
                }
                finally
                {
                    bufferPool.Return(bytes);
                }
            }
        }

        /// <summary>
        /// Deserialzes a string as UTF-8 bytes allocated from a local pool.
        /// The value returned should be added back to the pool when you're
        /// donw with it.
        /// </summary>
        /// <param name="reader">The input reader.</param>
        /// <returns>The UTF-8 encoded string bytes.</returns>
        private static PropertyNameBytes ReadPropertyNameBytes(BinaryReader reader)
        {
            var length = reader.ReadInt32();

            if (length == -1)
            {
                throw new FormatException("Message property names cannot be NULL.");
            }
            else if (length == 0)
            {
                throw new FormatException("Message property names cannot be empty.");
            }
            else
            {
                var bytes = bufferPool.Rent(length);

                reader.Read(bytes, 0, length);

                return new PropertyNameBytes()
                {
                    Bytes = bytes,
                    Span  = new Span<byte>(bytes, 0, length)
                };
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

                if (value.Length > 0)
                {
                    var byteCount = Encoding.UTF8.GetByteCount(value);
                    var bytes     = bufferPool.Rent(byteCount);

                    try
                    {
                        Encoding.UTF8.GetBytes(value, 0, value.Length, bytes, 0);

                        writer.Write(bytes, 0, byteCount);
                    }
                    finally
                    {
                        bufferPool.Return(bytes);
                    }
                }
            }
        }

        /// <summary>
        /// Serialize a string from a <see cref="PropertyNameUtf8"/>.
        /// </summary>
        /// <param name="writer">The output writer.</param>
        /// <param name="value">The string being serialized.</param>
        private static void WriteString(BinaryWriter writer, PropertyNameUtf8 value)
        {
            writer.Write(value.NameUtf8.Length);
            writer.Write(value.NameUtf8);
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
        /// Indicates the message type, one of the <see cref="InternalMessageTypes"/> values.
        /// </summary>
        public InternalMessageTypes Type { get; set; }

        /// <summary>
        /// Returns a case insensitive dictionary that maps argument names to value strings.
        /// </summary>
        public NiceDictionary<PropertyNameUtf8, string> Properties { get; private set; } = new NiceDictionary<PropertyNameUtf8, string>();

        /// <summary>
        /// Returns the list of binary attachments.
        /// </summary>
        public List<byte[]> Attachments { get; private set; } = new List<byte[]>();

        /// <summary>
        /// Serializes the message to a pooled <see cref="MemoryStream"/>.  Be sure to
        /// add the stream returned back to the <see cref="MemoryStreamPool"/> when you've 
        /// finished with it.
        /// </summary>
        /// <param name="ignoreTypeCode">Optionally ignore unspecified message types (used for unit testing).</param>
        /// <returns>A <see cref="MemoryStream"/> holding the serialized message.</returns>
        public MemoryStream SerializeAsStream(bool ignoreTypeCode = false)
        {
            if (!ignoreTypeCode && Type == InternalMessageTypes.Unspecified)
            {
                throw new ArgumentException($"Message type [{this.GetType().FullName}] has not initialized its [{nameof(Type)}] property.", nameof(ProxyMessage));
            }

            var output = MemoryStreamPool.Alloc();

            try
            {
                using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write((int)Type);

                    // Write the properties.

                    writer.Write(Properties.Count);

                    foreach (var property in Properties)
                    {
                        WriteString(writer, property.Key);
                        WriteString(writer, property.Value);
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

                // Rewind the stream.

                output.Position = 0;
            }
            catch
            {
                MemoryStreamPool.Free(output);
                throw;
            }

            return output;
        }

        /// <summary>
        /// <para>
        /// Serializes the message to bytes.
        /// </para>
        /// <note>
        /// This method is intended for testing purposes.  Use <see cref="SerializeAsStream(bool)"/>
        /// for production since that method will perform better by not needing to allocate a
        /// byte array with the message contents for every call.
        /// </note>
        /// </summary>
        /// <param name="ignoreTypeCode">Optionally ignore unspecified message types (used for unit testing).</param>
        /// <returns>The serialized byte array.</returns>
        public byte[] SerializeAsBytes(bool ignoreTypeCode = false)
        {
            if (!ignoreTypeCode && Type == InternalMessageTypes.Unspecified)
            {
                throw new ArgumentException($"Message type [{this.GetType().FullName}] has not initialized its [{nameof(Type)}] property.", nameof(ProxyMessage));
            }

            var output = SerializeAsStream(ignoreTypeCode);

            try
            {
                return output.ToArray();
            }
            finally
            {
                MemoryStreamPool.Free(output);
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
            target.ClientId = this.ClientId;
        }

        //---------------------------------------------------------------------
        // Helper methods derived classes can use for retrieving typed message properties.

        /// <summary>
        /// Helper method for retrieving a string property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="def">The default value to be returned if the named property doesn't exist.</param>
        /// <returns>The string value.</returns>
        internal string GetStringProperty(PropertyNameUtf8 key, string def = null)
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
        internal int GetIntProperty(PropertyNameUtf8 key, int def = 0)
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
        internal long GetLongProperty(PropertyNameUtf8 key, long def = 0)
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
        internal bool GetBoolProperty(PropertyNameUtf8 key, bool def = false)
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
        internal TEnum GetEnumProperty<TEnum>(PropertyNameUtf8 key, TEnum def = default(TEnum))
            where TEnum : struct, Enum
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
        internal double GetDoubleProperty(PropertyNameUtf8 key, double def = 0.0)
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
        internal DateTime GetDateTimeProperty(PropertyNameUtf8 key, DateTime def = default)
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
        internal TimeSpan GetTimeSpanProperty(PropertyNameUtf8 key, TimeSpan def = default)
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
        /// <remarks>
        /// <note>
        /// <para>
        /// <b>IMPORTANT:</b> Be very careful when referencing properties that use this
        /// method because the behavior will probably be unexepected.  You should:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     When you need to access multiple subfields of the property value,
        ///     dereference the property once, save the value to a variable and
        ///     then use the variable to access the subproperty.  Not doing this
        ///     will result in the JSON being parsed again for each property
        ///     reference.
        ///     </item>
        ///     <item>
        ///     Dereferencing the property and changing a subproperty value won't
        ///     actually persist the change back to the underlying property.  You'll
        ///     need to dereference the property to a variable, change the subproperty,
        ///     and then use <see cref="SetJsonProperty{T}(PropertyNameUtf8, T)"/> to persist the
        ///     change. 
        ///     </item>
        /// </list>
        /// <para>
        /// These restrictions are a bit odd but we're not actually expecting to 
        /// be doing any of these things within the <b>temporal-client</b> code.
        /// </para>
        /// </note>
        /// </remarks>
        internal T GetJsonProperty<T>(PropertyNameUtf8 key)
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
                    return NeonHelper.JsonDeserialize<T>(value, strict: false);
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Helper method for retrieving a byte array property.
        /// </summary>
        /// <param name="key">The property key.</param>]
        /// <returns>The byte array or <c>null</c>.</returns>
        internal byte[] GetBytesProperty(PropertyNameUtf8 key)
        {
            if (Properties.TryGetValue(key, out var value))
            {
                if (value == null)
                {
                    return null;
                }

                return Convert.FromBase64String(value);
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
        internal void SetStringProperty(PropertyNameUtf8 key, string value)
        {
            Properties[key] = value;
        }

        /// <summary>
        /// Helper method for setting a 32-bit integer property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetIntProperty(PropertyNameUtf8 key, int value)
        {
            Properties[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Helper method for setting a 64-bit integer property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetLongProperty(PropertyNameUtf8 key, long value)
        {
            Properties[key] = value.ToString();
        }

        /// <summary>
        /// Helper method for setting a boolean property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetBoolProperty(PropertyNameUtf8 key, bool value)
        {
            Properties[key] = NeonHelper.ToBoolString(value);
        }

        /// <summary>
        /// Helper method for setting an enumeration property.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetEnumProperty<TEnum>(PropertyNameUtf8 key, TEnum value)
            where TEnum : struct, Enum
        {
            Properties[key] = NeonHelper.EnumToString(value);
        }

        /// <summary>
        /// Helper method for setting a double property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetDoubleProperty(PropertyNameUtf8 key, double value)
        {
            Properties[key] = value.ToString("G") ;
        }

        /// <summary>
        /// Helper method for setting a date/time property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetDateTimeProperty(PropertyNameUtf8 key, DateTime value)
        {
            Properties[key] = value.ToString(NeonHelper.DateFormatTZ);
        }

        /// <summary>
        /// Helper method for setting a timespan property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetTimeSpanProperty(PropertyNameUtf8 key, TimeSpan value)
        {
            Properties[key] = value.Ticks.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Helper method for setting a complex property as JSON.
        /// </summary>
        /// <typeparam name="T">The property type.</typeparam>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetJsonProperty<T>(PropertyNameUtf8 key, T value)
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

        /// <summary>
        /// Helper method for setting a byte array property.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        internal void SetBytesProperty(PropertyNameUtf8 key, byte[] value)
        {
            if (value == null)
            {
                Properties[key] = null;
            }
            else
            {
                Properties[key] = Convert.ToBase64String(value);
            }
        }

        //---------------------------------------------------------------------
        // Common message properties.

        /// <summary>
        /// Identifies the Temporal service client the request references.  This will
        /// be zero for the few messages that don't reference a client.
        /// </summary>
        public long ClientId
        {
            get => GetLongProperty(PropertyNames.ClientId);
            set => SetLongProperty(PropertyNames.ClientId, value);
        }

        /// <summary>
        /// Optionally identifies a client specific worker.
        /// </summary>
        public long WorkerId
        {
            get => GetLongProperty(PropertyNames.WorkerId);
            set => SetLongProperty(PropertyNames.WorkerId, value);
        }
    }
}
