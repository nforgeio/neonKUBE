//-----------------------------------------------------------------------------
// FILE:	    Entity.cs
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

using System;
using System.ComponentModel;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Data
{
    /// <summary>
    /// Non generic interface describing an entity.  See <see cref="IEntity{T}"/>
    /// for more information.
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// Can be overridden by derived entities to return the Couchbase key to be used to
        /// persist the entity.  The base implementation throws a <see cref="NotSupportedException"/>.
        /// </summary>
        /// <returns>The Couchbase key for the entity.</returns>
        string GetKey();

        /// <summary>
        /// Can be overridden by derived entities to return the reference to be used to
        /// link to the entity instance.  The base implementation throws a <see cref="NotSupportedException"/>.
        /// </summary>
        /// <returns>The Couchbase ID for the entity.</returns>
        string GetRef();

        /// <summary>
        /// Identifies the entity type.
        /// </summary>
        string __EntityType { get; set; }

        /// <summary>
        /// Ensures that the entity properties are properly initialized.  The default 
        /// <see cref="Entity{T}"/> implementation does nothing.
        /// </summary>
        /// <exception cref="InvalidEntityException">Thrown if any entity properties are not formatted correctly.</exception>
        void Normalize();
    }

    /// <summary>
    /// Generic interface describing an entity.  Most entity classes will will inherit a base
    /// implementation of this interface from <see cref="Entity{T}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All entities must implement the <see cref="IEntity.__EntityType"/> property such that it returns
    /// the bucket unique string that identifies the entity type.  This string will be
    /// used to distinguish entity types within a Couchbase bucket.
    /// </para>
    /// <para>
    /// This interface supports the related concepts of entity <b>key</b> and <b>ref</b>.  The
    /// entity key is the string used to persist an entity instance to Couchbase.  By
    /// convention, this string is generally prefixed by the entity type and then is
    /// followed by instance specific properties, a UUID, or a singleton name.
    /// </para>
    /// <para>
    /// Entity <b>ref</b> is the value that other entities can use to reference an entity instance.
    /// This could be the same as the entity <b>key</b> but typically without the entity
    /// type prefix for brevity,
    /// </para>
    /// <para>
    /// Most entities should implement instance <see cref="IEntity.GetKey()"/> to return the unique Couchbase
    /// key for the instance and entities that can be referenced by other entities should
    /// implement instance <see cref="IEntity.GetRef()"/>.  Note that the base <see cref="Entity{T}"/> 
    /// implementation throws <see cref="NotSupportedException"/> for these methods.
    /// </para>
    /// <para>
    /// As a convention, many <see cref="IEntity{T}"/> implementations also implement <c>static</c>
    /// <b>GetKey(...)</b> and <b>GetRef(...)</b> methods that return the Couchbase key and
    /// reference to an entity based on parameters passed.  Singleton entities (that will have
    /// only one global instance in Couchbase) typically implement the <c>static</c>
    /// <b>GetSingletonKey(...)</b> and <b>GetSingletonRef(...)</b> methods instead.
    /// </para>
    /// <para>
    /// Implement the <see cref="Equals(T)"/> method to compare one entity against another.
    /// The base <see cref="Entity{T}"/> implementation serializes both entities to JSON
    /// and compares them.
    /// </para>
    /// <para>
    /// Implement the <see cref="IEntity.Normalize()"/> method to ensure that the entity properties
    /// are properly initialized.  The default <see cref="Entity{T}"/> implementation
    /// does nothing.
    /// </para>
    /// </remarks>
    public interface IEntity<T> : IEntity
        where T : class, new()
    {
        /// <summary>
        /// Tests this instance against another for equality.
        /// </summary>
        /// <param name="other">The other instance.</param>
        /// <returns><c>true</c> if the instances are equal.</returns>
        bool Equals(T other);
    }
}
