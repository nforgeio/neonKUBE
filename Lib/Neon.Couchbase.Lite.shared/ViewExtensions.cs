//-----------------------------------------------------------------------------
// FILE:	    ViewExtensions.cs
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.DynamicData;

// $todo(jeff.lill): Look into implementing extensions for SetMapReduce().

namespace Couchbase.Lite
{
    /// <summary>
    /// Entity document mapping delegate.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="document">Passed as the entity.</param>
    /// <param name="emit">The mapping emit delegate.</param>
    public delegate void EntityMapDelegate<TEntity>(EntityDocument<TEntity> document, EmitDelegate emit)
        where TEntity : class, IDynamicEntity, new();

    /// <summary>
    /// Implements extensions to the Couchbase Lite <see cref="View"/> class.
    /// </summary>
    public static class ViewExtensions
    {
        /// <summary>
        /// Sets the view's entity mapping function.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="mapDelegate">The document mapping function.</param>
        /// <param name="version">The view's version.</param>
        /// <remarks>
        /// You may pass the <b>document</b> parameter as a value to the emit
        /// delegate, just as you would for a non-entity mapping function.
        /// In this case, Emit will to the <i>right thing</i> and add the document's
        /// low-level properties, as expected.
        /// </remarks>
        public static void SetMap<TEntity>(this View view, EntityMapDelegate<TEntity> mapDelegate, string version)
            where TEntity : class, IDynamicEntity, new()
        {
            Covenant.Requires<ArgumentNullException>(view != null);
            Covenant.Requires<ArgumentNullException>(mapDelegate != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(version));

            view.SetMap(
                (doc, emit) =>
                {
                    mapDelegate(new EntityDocument<TEntity>(Stub.Param, doc, EntityDatabase.From(view.Database)),
                        (key, value) =>
                        {
                            var entityDocument = value as EntityDocument<TEntity>;

                            if (entityDocument != null)
                            {
                                // For entity document values, we want to add its properties,
                                // not the document itself.

                                emit(key, entityDocument.Properties);
                            }
                            else
                            {
                                emit(key, value);
                            }
                        });
                },
                version);
        }

        /// <summary>
        /// Creates a query intended to return <see cref="EntityQueryRow{TEntity}"/> rows containing
        /// <see cref="EntityDocument{TEntity}"/> instances.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="view">The <see cref="View"/>.</param>
        /// <returns>The created <see cref="EntityLiveQuery{TEntity}"/>.</returns>
        /// <remarks>
        /// <para>
        /// Entity queries are appropriate for situations where the application needs access to document
        /// properties that are not included in the view.  Loading the documents will incur a performance
        /// overhead.  You should use the base <see cref="View.CreateQuery()"/> method to see better performance
        /// for situations where the view covers the desired query results.
        /// </para>
        /// </remarks>
        public static EntityQuery<TEntity> CreateQuery<TEntity>(this View view)
            where TEntity : class, IDynamicEntity, new()
        {
            return new EntityQuery<TEntity>(view.CreateQuery());
        }
    }
}
