//-----------------------------------------------------------------------------
// FILE:	    EntitySerializer.cs
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
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Serialization;
using Couchbase.IO;
using Couchbase.N1QL;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Data;
using Neon.Retry;
using Neon.Time;

// $todo(jeff.lill):
//
// This code is going to generate a some GC activity.  In the distant
// future, we should investigate this:
//
//      https://blog.couchbase.com/using-jil-for-custom-json-serialization-in-the-couchbase-net-sdk/
//
// or perhaps the new Microsoft serializer shipping with .NET CORE 3.0.

namespace Couchbase
{
    /// <summary>
    /// Implements a Couchbase serializer that's capable of handling <see cref="IPersistableType"/>
    /// based objects in addition to plain-old-objects.
    /// </summary>
    internal class EntitySerializer : ITypeSerializer
    {
        private ITypeSerializer     defaultSerializer;

        /// <summary>
        /// Constructor.
        /// </summary>
        public EntitySerializer()
        {
            // We're simply going to wrap the default Couchbase serializer
            // so we can detect generated entities and call their __Save()
            // and __Load() methods as required.
            //
            // Note that we're using the common Neon serializer settings.

            this.defaultSerializer = new DefaultSerializer(NeonHelper.JsonRelaxedSerializerSettings.Value, NeonHelper.JsonRelaxedSerializerSettings.Value);
        }

        /// <inheritdoc/>
        public T Deserialize<T>(byte[] buffer, int offset, int length)
        {
            var entityType = typeof(T);

            if (entityType.Implements<IGeneratedType>())
            {
                var jObject = defaultSerializer.Deserialize<JObject>(buffer, offset, length);

                if (jObject == null)
                {
                    return default(T);
                }

                return GeneratedTypeFactory.CreateFrom<T>(jObject);
            }
            else
            {
                return defaultSerializer.Deserialize<T>(buffer, offset, length);
            }
        }

        /// <inheritdoc/>
        public T Deserialize<T>(Stream stream)
        {
            var entityType = typeof(T);

            if (entityType.Implements<IGeneratedType>())
            {
                // Custom IGeneratedType

                var jObject = defaultSerializer.Deserialize<JObject>(stream);

                return (T)GeneratedTypeFactory.CreateFrom<T>(jObject);
            }
            else
            {
                // Plain old object.

                return defaultSerializer.Deserialize<T>(stream);
            }
        }

        /// <inheritdoc/>
        public byte[] Serialize(object obj)
        {
            var entityType = obj.GetType();

            var generatedDataModel = obj as IGeneratedType;

            if (generatedDataModel != null)
            {
                // Custom IGeneratedType

                var jObject = generatedDataModel.__Save();

                return defaultSerializer.Serialize(jObject);
            }
            else
            {
                // Plain old object.

                return defaultSerializer.Serialize(obj);
            }
        }
    }
}
