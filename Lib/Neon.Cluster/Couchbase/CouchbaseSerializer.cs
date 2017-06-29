//-----------------------------------------------------------------------------
// FILE:	    CouchbaseSerializer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.Core.Serialization;

using Neon.Cluster;
using Neon.Common;
using Neon.Data;
using Neon.Retry;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Neon.Cluster
{
    /// <summary>
    /// Wraps the <see cref="NeonHelper"/> JSON serializer into a form that Couchbase
    /// can use.
    /// </summary>
    internal class CouchbaseSerializer : ITypeSerializer
    {
        /// <inheritdoc/>
        public T Deserialize<T>(byte[] buffer, int offset, int length)
        {
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(buffer, offset, length), NeonHelper.JsonSerializerSettings);
        }

        /// <inheritdoc/>
        public T Deserialize<T>(Stream stream)
        {
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(stream.ReadToEnd()), NeonHelper.JsonSerializerSettings);
        }

        /// <inheritdoc/>
        public byte[] Serialize(object obj)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj, Formatting.None, NeonHelper.JsonSerializerSettings));
        }
    }
}
