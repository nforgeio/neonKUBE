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

using Neon.Common;
using Neon.Data;
using Neon.Retry;
using Neon.Time;

// $todo(jeff.lill):
//
// This code is going to generate a lot of GC activity.  In the distant
// future, we should investigate this:
//
//      https://blog.couchbase.com/using-jil-for-custom-json-serialization-in-the-couchbase-net-sdk/
//
// or perhaps the new Microsoft serializer shipping with .NET CORE 3.0.

namespace Couchbase
{
    /// <summary>
    /// Implements a Couchbase serializer that's capable of handling <see cref="IEntity"/>
    /// based objects in addition to plain-old-objects.
    /// </summary>
    public class EntitySerializer : ITypeSerializer
    {
        /// <inheritdoc/>
        public T Deserialize<T>(byte[] buffer, int offset, int length)
        {
            using (var stream = new MemoryStream(buffer, offset, length))
            {
                return Deserialize<T>(stream);
            }
        }

        /// <inheritdoc/>
        public T Deserialize<T>(Stream stream)
        {
            var entityType = typeof(T);

            if (entityType.Implements<IGeneratedEntity>())
            {
                // Custom IGeneratedEntity

                // $todo(jeff.lill): DELETE THIS! (actually just return it).

                var v = (T)GeneratedEntityFactory.CreateFrom(entityType, stream, System.Text.Encoding.UTF8);

                return v;
            }
            else
            {
                // Plain old object.

                using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                {
                    using (var jsonReader = new JsonTextReader(reader))
                    {
                        return EntitySerializationHelper.Serializer.Deserialize<T>(jsonReader);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public byte[] Serialize(object obj)
        {
            var entityType = obj.GetType();

            using (var output = new MemoryStream())
            {
                using (var writer = new StreamWriter(output))
                {
                    var generatedDataModel = obj as IGeneratedEntity;

                    if (generatedDataModel != null)
                    {
                        // Custom IGeneratedEntity

                        EntitySerializationHelper.Serializer.Serialize(writer, generatedDataModel.__Save());
                    }
                    else
                    {
                        // Plain old object.

                        EntitySerializationHelper.Serializer.Serialize(writer, obj);
                    }

                    writer.Flush();

                    var v = output.ToArray();

                    return output.ToArray();
                }
            }
        }
    }
}
