//-----------------------------------------------------------------------------
// FILE:	    EntityQueryRow.cs
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

namespace Couchbase.Lite
{
    /// <summary>
    /// An entity implementation of <see cref="QueryRow"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <remarks>
    /// The read-only entity row document can be access via the <see cref="Document"/>
    /// property and the underlying <see cref="QueryRow"/> details can be accessed
    /// using <see cref="Base"/>.
    /// </remarks>
    /// <threadsafety instance="false"/>
    public class EntityQueryRow<TEntity>
        where TEntity : class, IDynamicEntity, new()
    {
        private EntityDocument<TEntity> cachedDocument;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="row">The wrapped row.</param>
        internal EntityQueryRow(QueryRow row)
        {
            Covenant.Requires<ArgumentNullException>(row != null);

            Base = row;
        }

        /// <summary>
        /// Returns the row results as an entity <b>read-only</b> document.
        /// </summary>
        public EntityDocument<TEntity> Document
        {
            get
            {
                if (cachedDocument != null)
                {
                    return cachedDocument;
                }

                return cachedDocument = new EntityDocument<TEntity>(Stub.Param, Base.DocumentProperties, EntityDatabase.From(Base.Database));
            }
        }

        /// <summary>
        /// Returns the wrapped row details.
        /// </summary>
        public QueryRow Base { get; private set; }

        /// <summary>
        /// Returns the view's key for this row.
        /// </summary>
        public object Key
        {
            get { return Base.Key; }
        }

        /// <summary>
        /// Returns the view's key for this row cast to a <c>string</c>.
        /// </summary>
        public string KeyString
        {
            get { return (string)Base.Key; }
        }
    }
}
