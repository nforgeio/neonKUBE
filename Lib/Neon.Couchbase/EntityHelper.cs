//-----------------------------------------------------------------------------
// FILE:	    EntityHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Data
{
    /// <summary>
    /// Helper methods for managing database entities.
    /// </summary>
    public static class EntityHelper
    {
        /// <summary>
        /// Generates a globally unique ID.
        /// </summary>
        /// <returns>The ID as a string.</returns>
        public static string GenerateUuid()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }

        /// <summary>
        /// Extracts the entity ID from an entity key.
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
        /// This method extracts the string after the last (<b>"::"</b>) as the document ID.
        /// </para>
        /// </remarks>
        public static string GetEntityId(string entityKey)
        {
            if (string.IsNullOrEmpty(entityKey))
            {
                return null;
            }

            var pos = entityKey.LastIndexOf("::");

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
        /// Generates an entity key from the entity ID and entity type.
        /// </summary>
        /// <param name="entityId">The entity ID.</param>
        /// <param name="entityType">The entity type.</param>
        /// <returns>The entity key.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entityId"/> is <c>null</c> or empty.</exception>
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
        public static string GetEntityKey(string entityId, string entityType)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(entityId));

            if (string.IsNullOrEmpty(entityType))
            {
                return entityId;
            }

            return $"{entityType}::{entityId}";
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
    }
}
