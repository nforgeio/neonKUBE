//-----------------------------------------------------------------------------
// FILE:	    IDynamicEntityContext.cs
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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.DynamicData;
using Neon.DynamicData.Internal;

namespace Neon.DynamicData
{
    /// <summary>
    /// Implements methods used to resolve entity links.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Advanced entity persistence frameworks, like the Neon Couchbase Lite extensions,
    /// implement this interface to provide allowing entities to link to one another.
    /// Links are specified as opaque strings.  For Couchbase Lite, these will be document
    /// IDs.  Other frameworks can specify something else.
    /// </para>
    /// <para>
    /// <see cref="LoadEntity{TEntity}(string, out Func{bool})"/> attempts to load an entity,
    /// given its link.  The method returns <c>null</c> if the linked entity doesn't exist
    /// and will throw an exception if there was some other kind of error.
    /// </para>
    /// <para>
    /// <see cref="LoadDocument{TDocument}(string, out Func{bool})"/> attempts to load a document,
    /// given its link.  The method returns <c>null</c> if the linked document doesn't exist
    /// and will throw an exception if there was some other kind of error.
    /// </para>
    /// </remarks>
    public interface IDynamicEntityContext
    {
        /// <summary>
        /// Instantiates the entity of the requested type by dereferencing the 
        /// <paramref name="link"/> string passed.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="link">The link string.</param>
        /// <param name="isDeletedFunc">
        /// Optionally returns as a function that will determine whether the linked 
        /// entity has been deleted from its context.  This may also return as <c>null</c>.
        /// </param>
        /// <returns>The loaded entity or <c>null</c> if it doesn't exist.
        /// </returns>
        /// <exception cref="Exception">
        /// The implementation should throw an exception if the entity couldn't be loaded 
        /// due to some kind of error, but not if the entity simply doesn't exist.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Advanced entity contexts such as that implemented by the Neon Couchbase
        /// Lite extension will return a non-<c>null</c> <paramref name="isDeletedFunc"/>
        /// value that can be tested to check if the entity still exists.
        /// </para>
        /// </remarks>
        TEntity LoadEntity<TEntity>(string link, out Func<bool> isDeletedFunc)
            where TEntity : class, IDynamicEntity, new();

        /// <summary>
        /// Instantiates the document of the requested type by dereferencing the 
        /// <paramref name="link"/> string passed.
        /// </summary>
        /// <typeparam name="TDocument">The document type.</typeparam>
        /// <param name="link">The link string.</param>
        /// <param name="isDeletedFunc">
        /// Optionally returns as a function that will determine whether the linked 
        /// document has been deleted from its context.  This may also return as <c>null</c>.
        /// </param>
        /// <returns>The loaded document or <c>null</c> if it doesn't exist.
        /// </returns>
        /// <exception cref="Exception">
        /// The implementation should throw an exception if the document couldn't be loaded 
        /// due to some kind of error, but not if the entity simply doesn't exist.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Advanced entity contexts such as that implemented by the Neon Couchbase
        /// Lite extension will return a non-<c>null</c> <paramref name="isDeletedFunc"/>
        /// value that can be tested to check if the document still exists.
        /// </para>
        /// </remarks>
        TDocument LoadDocument<TDocument>(string link, out Func<bool> isDeletedFunc)
            where TDocument : class, IDynamicDocument;
    }
}
