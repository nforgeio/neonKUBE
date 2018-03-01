//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Yaml.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using YamlDotNet.Core;
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

        //---------------------------------------------------------------------
        // Implementation

        private static Lazy<Serializer> yamlSerializer =
            new Lazy<Serializer>(
                () =>
                {
                    return new SerializerBuilder()
                        .WithNamingConvention(new LowercaseYamlNamingConvention())
                        .Build();
                });

        private static Lazy<Deserializer> strictYamlDeserializer =
            new Lazy<Deserializer>(
                () =>
                {
                    return new DeserializerBuilder()
                        .WithNamingConvention(new LowercaseYamlNamingConvention())
                        .Build();
                });

        private static Lazy<Deserializer> relaxedYamlDeserializer =
            new Lazy<Deserializer>(
                () =>
                {
                    return new DeserializerBuilder()
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
        /// Deserializes YAML text to an object.
        /// </para>
        /// <note>
        /// Property names are expected to be lowercase.
        /// </note>
        /// </summary>
        /// <typeparam name="T">The desired output type.</typeparam>
        /// <param name="yaml">The YAML text.</param>
        /// <param name="ignoreUnmatched">Optionally ignore unmatched properties.</param>
        /// <returns>The parsed <typeparamref name="T"/>.</returns>
        public static T YamlDeserialize<T>(string yaml, bool ignoreUnmatched = false)
        {
            try
            {
                if (ignoreUnmatched)
                {
                    return relaxedYamlDeserializer.Value.Deserialize<T>(yaml);
                }
                else
                {
                    return strictYamlDeserializer.Value.Deserialize<T>(yaml);
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
    }
}
