//-----------------------------------------------------------------------------
// FILE:	    EntityQueryRow.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
