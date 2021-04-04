//-----------------------------------------------------------------------------
// FILE:	    EntityHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Reflection;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

namespace Neon.Data
{
    /// <summary>
    /// Helper methods for managing database entities.
    /// </summary>
    public static class EntityHelper
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Holds information about an entity's property.
        /// </summary>
        private class EntityPropertyInfo
        {
            /// <summary>
            /// The list of public properties as serialized to JSON.
            /// </summary>
            public List<string> Properties { get; set; }

            /// <summary>
            /// The comma separated property names suitable for using in a N1QL
            /// <c>select</c> statement.
            /// </summary>
            public string Select { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Caches the entity properties defined for an <see cref="IPersistableType"/>.
        /// </summary>
        private static Dictionary<System.Type, EntityPropertyInfo> entityProperties = new Dictionary<System.Type, EntityPropertyInfo>();

        /// <summary>
        /// Generates a URI-safe globally unique ID.
        /// </summary>
        /// <returns>The ID as a string.</returns>
        /// <remarks>
        /// <note>
        /// The value returned is a <see cref="Guid"/> converted to base-64 and then
        /// made URI safe by replacing "=" characters with "-" and "/" to "_" and 
        /// also removing any "=" padding charcters.
        /// </note>
        /// </remarks>
        public static string CreateUuid()
        {
            var base64       = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            var paddingCount = 0;

            for (int i = base64.Length - 1; i >= 0; i--)
            {
                if (base64[i] != '=')
                {
                    break;
                }

                paddingCount++;
            }

            var converted = new char[base64.Length - paddingCount];

            for (int i = 0; i < converted.Length; i++)
            {
                var ch = base64[i];

                switch (ch)
                {
                    case '+':

                        ch = '-';
                        break;

                    case '/':

                        ch = '_';
                        break;
                }

                converted[i] = ch;
            }

            return new string(converted);
        }

        /// <summary>
        /// Extracts the entity reference from an entity key.
        /// </summary>
        /// <param name="entityKey">The entity key.</param>
        /// <returns>The entity ID.</returns>
        /// <remarks>
        /// <para>
        /// This implements a common convention where Couchbase entities are persisted using a
        /// key formed by appending entity ID to the entity type, separated by a double
        /// colon (<b>"::"</b>).  This makes entity types available for filtering when
        /// managing cross datacenter replication.
        /// </para>
        /// <para>
        /// This method extracts the string after the first (<b>"::"</b>) as the document ID.
        /// </para>
        /// </remarks>
        public static string GetEntityRef(string entityKey)
        {
            if (string.IsNullOrEmpty(entityKey))
            {
                return null;
            }

            var pos = entityKey.IndexOf("::");

            if (pos >= 0)
            {
                return entityKey.Substring(pos + 2);
            }
            else
            {
                return entityKey;
            }
        }

        /// <summary>
        /// Generates an entity key from the entity reference and entity type.
        /// </summary>
        /// <param name="entityRef">The entity reference.</param>
        /// <param name="entityType">The entity type.</param>
        /// <returns>The entity key.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entityRef"/> is <c>null</c> or empty.</exception>
        /// <remarks>
        /// <para>
        /// Stoke follows a common convention where Couchbase entities are persisted using a
        /// key formed by appending entity reference to the entity type, separated by a double
        /// colon (<b>"::"</b>).  This makes entity types available for filtering when
        /// managing cross datacenter replication.
        /// </para>
        /// <para>
        /// This method concatenates the entity type and ID using a (<b>"::"</b>) separator.
        /// </para>
        /// </remarks>
        public static string GetEntityKey(string entityRef, string entityType)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(entityRef), nameof(entityRef));

            if (string.IsNullOrEmpty(entityType))
            {
                return entityRef;
            }

            return $"{entityType}::{entityRef}";
        }

        /// <summary>
        /// Generates an entity key from the a GUID and entity type.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <returns>The entity key.</returns>
        /// <remarks>
        /// <para>
        /// Stoke follows a common convention where Couchbase entities are persisted using a
        /// key formed by appending entity ID to the entity type, separated by a double
        /// colon (<b>"::"</b>).  This makes entity types available for filtering when
        /// managing cross datacenter replication.
        /// </para>
        /// <para>
        /// This method concatenates the entity type and ID using a (<b>"::"</b>) separator.
        /// </para>
        /// </remarks>
        public static string GetEntityKey(string entityType)
        {
            var entityId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            if (string.IsNullOrEmpty(entityType))
            {
                return entityId;
            }

            return $"{entityType}::{entityId}";
        }

        // This is the format used to persist time to Couchbase.  Don't change
        // this without really knowing what you're doing.
        private const string couchbaseDateFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";

        /// <summary>
        /// Serializes a <see cref="DateTime"/> into the standard format used
        /// for persisting to Couchbase.
        /// </summary>
        /// <param name="input">The input time.</param>
        /// <returns>The serialized output.</returns>
        public static string ToCouchbase(DateTime input)
        {
            return input.ToString(couchbaseDateFormat);
        }

        /// <summary>
        /// Serializes a <see cref="DateTimeOffset"/> into the standard format used
        /// for persisting to Couchbase.
        /// </summary>
        /// <param name="input">The input time.</param>
        /// <returns>The serialized output.</returns>
        public static string ToCouchbase(DateTimeOffset input)
        {
            return input.ToString(couchbaseDateFormat);
        }

        /// <summary>
        /// Returns the serializable property information for an entity.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <returns>The <see cref="EntityPropertyInfo"/>.</returns>
        private static EntityPropertyInfo GetEntityPropertyInfo<TEntity>()
            where TEntity : IPersistableType
        {
            EntityPropertyInfo info;

            // Look for the mapping in the cache.

            lock (entityProperties)
            {
                if (entityProperties.TryGetValue(typeof(TEntity), out info))
                {
                    return info;
                }
            }

            // Reflect the class properties.

            var properties = typeof(TEntity).GetTypeInfo().GetProperties(BindingFlags.Instance | BindingFlags.Public);

            info = new EntityPropertyInfo()
            {
                Properties = new List<string>(properties.Length)
            };

            foreach (var property in properties
                .OrderBy(property => property.Name))
            {
                if (property.GetCustomAttribute(typeof(JsonIgnoreAttribute)) != null)
                {
                    continue;   // Ignore properties tagged with [JsonIgnore].
                }

                var jsonProperty = property.GetCustomAttribute<JsonPropertyAttribute>();

                if (jsonProperty != null)
                {
                    // Use the [JsonProperty(PropertyName="xxx")] value.

                    info.Properties.Add(jsonProperty.PropertyName);
                }
                else
                {
                    // Use the actual property name.  Note that this is fragile because 
                    // Couchbase may be configured to change the character case when
                    // serializing entities.

                    info.Properties.Add(property.Name);
                }
            }

            if (info.Properties.Count == 0)
            {
                throw new NotSupportedException($"[{nameof(TEntity)}] does not have any serializable JSON properties.");
            }

            // Generate the comma separated list of properties for use 
            // in N1QL select statements and cache the information.

            var sb = new StringBuilder();

            foreach (var property in info.Properties
                .OrderBy(property => property))
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                // We need to quote the property with backticks to 
                // ensure that property names that are the same as 
                // N1QL  reserved words will work properly as described
                // here:
                //
                // https://developer.couchbase.com/documentation/server/current/n1ql/n1ql-language-reference/reservedwords.html

                sb.Append($"`{property}`"); 
            }

            info.Select = sb.ToString();

            lock (entityProperties)
            {
                entityProperties[typeof(TEntity)] = info;
            }

            return info;
        }

        /// <summary>
        /// Returns the names of an entity's properties that will be serialized to JSON.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <returns>The set of property names.</returns>
        /// <remarks>
        /// <para>
        /// This method is handy when manually building <b>N1QL</b> query strings when
        /// you want to return all entity fields but you don't want to use <c>select *</c>
        /// because this nests each result into a property named for the bucket.
        /// I'm not entirely sure why Couchbase does this.
        /// </para>
        /// <note>
        /// This method will include all public entity properties that do not have the
        /// <see cref="JsonIgnoreAttribute"/> and this also honors the property names
        /// specified by any <see cref="JsonPropertyAttribute"/> attributes.
        /// </note>
        /// </remarks>
        public static IEnumerable<string> GetEntityProperties<TEntity>()
            where TEntity : IPersistableType
        {
            return GetEntityPropertyInfo<TEntity>().Properties;
        }

        /// <summary>
        /// Returns the comma separated names of an entity's properties in a form where 
        /// they can be easily added to a manually created N1QL statement.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <returns>The comma separated list of of property names.</returns>
        /// <remarks>
        /// <para>
        /// This method is handy when manually building <b>N1QL</b> query strings when
        /// you want to return all entity fields but you don't want to use <c>select *</c>
        /// because this nests each result into a property named for the bucket.  You may
        /// use the result to replace the star (<b>*</b>) in the <c>select</c> with the
        /// explicit property names.
        /// </para>
        /// <note>
        /// This method will include all public entity properties that do not have the
        /// <see cref="JsonIgnoreAttribute"/> and this also honors the property names
        /// specified by any <see cref="JsonPropertyAttribute"/> attributes.
        /// </note>
        /// </remarks>
        public static string GetEntitySelectProperties<TEntity>()
            where TEntity : IPersistableType
        {
            return GetEntityPropertyInfo<TEntity>().Select;
        }
    }
}
