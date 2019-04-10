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
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;

// $todo(jeff.lill)
//
// Performance could be improved by maintaining output stream and buffer pools
// rather than allocating these every time.

namespace Neon.Cadence
{
    /// <summary>
    /// The base class for all messages transferred between the .NET Cadence client
    /// and the Cadence proxy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is designed to be a very simple and flexible way of communicating
    /// operations and status between the Cadence client and proxy.  The specific 
    /// message type is identified via the <see cref="Type"/> property (one of the 
    /// <see cref="MessageType"/> values.  The <see cref="Arguments"/> dictionary will be
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
    /// |    ARG-COUNT     |   32-bit
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
    /// number of arguments to follow.  Each argument consists of an encoded
    /// string for the argument name followed by an encoded string for the value.
    /// </para>
    /// <para>
    /// After the arguments will be a 32-bit integer specifying the
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
    /// </remarks>
    internal class ProxyMessage
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The content type to used for HTTP requests encapsulating a <see cref="ProxyMessage"/>.
        /// </summary>
        public const string ContentType = "application/x-neon-cadence-proxy";

        /// <summary>
        /// Deserializes the message from a stream.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <returns>The decoded message.</returns>
        public static ProxyMessage Deserialize(Stream input)
        {
            using (var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true))
            {
                ProxyMessage message;

                // Read the message type and create a message instance of the specified type.

                var messageType = reader.ReadInt32();

                switch (messageType)
                {
                    case MessageType.Unknown:

                        message = new ProxyMessage();
                        break;

                    default:

                        throw new NotImplementedException($"Unexpected message type [{messageType}].");
                }

                // Read the arguments.

                var argCount = reader.ReadInt32();

                for (int i = 0; i < argCount; i++)
                {
                    var name  = ReadString(reader);
                    var value = ReadString(reader);

                    message.Arguments.Add(name, value);
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

                return message;
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
        /// Constructor.
        /// </summary>
        public ProxyMessage()
        {
        }

        /// <summary>
        /// Indicates the message type, one of the <see cref="MessageType"/> values.
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// Returns a case insensitive dictionary that maps argument names to value strings.
        /// </summary>
        public Dictionary<string, string> Arguments { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Returns the list of binary attachments.
        /// </summary>
        public List<byte[]> Attachments { get; private set; } = new List<byte[]>();

        /// <summary>
        /// Serializes the message to bytes.
        /// </summary>
        /// <returns>The serialized byte array.</returns>
        public byte[] Serialize()
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write(Type);

                    // Write the arguments.

                    writer.Write(Arguments.Count);

                    foreach (var arg in Arguments)
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
    }
}
