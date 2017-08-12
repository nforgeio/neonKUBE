//-----------------------------------------------------------------------------
// FILE:	    EntityHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;

namespace Neon.Data
{
    /// <summary>
    /// Helper methods for managing database entities.
    /// </summary>
    public static class EntityHelper
    {
        /// <summary>
        /// Generates a URI-safe globally unique ID.
        /// </summary>
        /// <returns>The ID as a string.</returns>
        /// <remarks>
        /// <note>
        /// The value returned is a <see cref="Guid"/> converted to base-64 and then
        /// made URI safe by replacing "=" characters with "-" and "/" to "_" and
        /// removing any "=" padding charcters.
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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(entityRef));

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
    }
}
