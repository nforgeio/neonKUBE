//-----------------------------------------------------------------------------
// FILE:	    MessageSerializer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EasyNetQ;
using EasyNetQ.DI;
using EasyNetQ.Logging;
using EasyNetQ.Management.Client;
using EasyNetQ.Topology;

using RabbitMQ;
using RabbitMQ.Client;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;

namespace Neon.HiveMQ
{
    /// <summary>
    /// Manages the serialization of messages for the <see cref="HiveBus"/> API
    /// for delivery via RabbitMQ.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="HiveBus"/> messages are serialized to bytes using a custom 
    /// framing format along with the message body as JSON UTF-8 encoded text.
    /// Here's the serialized message format:
    /// </para>
    /// <code>
    /// +------------------+
    /// |     0x8F21EE1D   |    Magic Number (4-bytes)
    /// +------------------+
    /// |                  |
    /// |    TYPE-LENGTH   |    Type name byte length (variable)
    /// |                  |    (https://en.wikipedia.org/wiki/LEB128)
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |       UTF-8      |    Type name (TYPE-LENGTH-bytes)
    /// |                  |
    /// |                  |
    /// +------------------+
    /// |                  |
    /// |  MESSAGE-LENGTH  |    Message JSON byte length (variable)
    /// |                  |    (https://en.wikipedia.org/wiki/LEB128)
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |       UTF-8      |    Message JSON (MESSAGE-LENGTH bytes)
    /// |                  |
    /// |                  |
    /// +------------------+
    /// </code>
    /// <note>
    /// The Magic Number and lengtn fields are serialized using <b>little endian</b>
    /// byte ordering.
    /// </note>
    /// </remarks>
    internal static class MessageSerializer
    {
        private const uint MagicNumber = 0x8F21EE1D;

        /// <summary>
        /// Serializes a message into a byte array.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message.</param>
        /// <returns>The serialized message bytes.</returns>
        public static byte[] Serialize<TMessage>(TMessage message)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(message != null);

            var json = NeonHelper.JsonSerialize(message, Formatting.None);

            // Compute a buffer size that should be plenty big enough and
            // is a multiple of 1024 to help avoid heap fragementation.

            var bufSize = 512 + json.Length;

            bufSize = ((bufSize / 1024) + 1) * 1024;

            using (var stream = new MemoryStream(bufSize))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(MagicNumber);
                    writer.Write(typeof(TMessage).FullName);
                    writer.Write(json);
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes a message from a byte array.
        /// </summary>
        /// <param name="bytes">The serialized message bytes.</param>
        /// <param name="typeToConsumer">Dictionary that maps the message type names to the consumers for a channel.</param>
        /// <returns>
        /// The channel consumer and deserialized message.  Both results will be <c>null</c>
        /// if the deserialized message does not have a registered channel consumer.
        /// </returns>
        /// <exception cref="FormatException">Thrown if the message cannot be deserialized due to a format problem.</exception>
        public static (ConsumerBase Consumer, object Message) Deserialize(byte[] bytes, ConcurrentDictionary<string, ConsumerBase> typeToConsumer)
        {
            Covenant.Requires<ArgumentNullException>(bytes != null);

            if (bytes.Length <= 4)
            {
                throw new FormatException($"Message is too small [{bytes.Length} bytes] to deserialize.");
            }

            try
            {
                using (var stream = new MemoryStream(bytes))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        var magic = (uint)reader.ReadInt32();

                        if (magic != MagicNumber)
                        {
                            throw new FormatException($"Message magic number [{magic}] is not valid.");
                        }

                        var typeName = reader.ReadString();

                        if (!typeToConsumer.TryGetValue(typeName, out var consumer))
                        {
                            return (Consumer: null, Message: null);
                        }

                        var message = NeonHelper.JsonDeserialize(consumer.MessageType, reader.ReadString());

                        return (Consumer: consumer, Message: message);
                    }
                }
            }
            catch (FormatException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new FormatException("Cannot deserialize HiveMQ message.", e);
            }
        }
    }
}
