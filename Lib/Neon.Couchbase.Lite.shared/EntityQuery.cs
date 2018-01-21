//-----------------------------------------------------------------------------
// FILE:	    EntityQuery.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite;

using Neon.Common;
using Neon.DynamicData;

namespace Couchbase.Lite
{
    /// <summary>
    /// An entity implementation of <see cref="Query"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity entity type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class wraps a base Couchbase Lite <see cref="Query"/> object such that
    /// it returns its results as <see cref="EntityQueryRow{TEntity}"/> instances.
    /// </para>
    /// <note>
    /// This class implicitly reads the entire resulting documents from the database.
    /// Often, this is what applications want, but this will come at a performance
    /// cost for applications that need only data present in the view.  You should
    /// use the basic <see cref="Query"/> class for those situations.
    /// </note>
    /// <para>
    /// The class implements most of the members defined by <see cref="Query"/>.  Here's a
    /// summary of the differences:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="PostFilter"/></term>
    ///     <description>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="Query.Prefetch"/></term>
    ///     <description>
    ///     This class does not implement this because it implicitly prefetches the entire
    ///     document and includes it as the <see cref="EntityQueryRow{TEntity}.Document"/>
    ///     property.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="Query.Completed"/></term>
    ///     <description>
    ///     This is not implemented.  Applications should use <see cref="RunAsync"/> to implement
    ///     asynchronous queries.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public sealed class EntityQuery<TEntity> : IDisposable
        where TEntity : class, IDynamicEntity, new()
    {
        private Query                                   query;
        private Func<EntityQueryRow<TEntity>, bool>     postFilter;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="query">The associated low-level query.</param>
        internal EntityQuery(Query query)
        {
            Covenant.Requires<ArgumentNullException>(query != null);

            this.query          = query;
            this.query.Prefetch = true;
        }

        /// <summary>
        /// Possible modes of an all-docs query. See the <see cref="AllDocsMode"/> enumeration for details.
        /// </summary>
        public AllDocsMode AllDocsMode
        {
            get { return query.AllDocsMode; }
            set { query.AllDocsMode = value; }
        }

        /// <summary>
        /// Returns the database that owns the query's <see cref="View"/>.
        /// </summary>
        public Database Database
        {
            get { return query.Database; }
        }

        /// <summary>
        /// Indicates whether the results should be returned in decending key order.  This defaults
        /// to <c>false</c>.
        /// </summary>
        public bool Descending
        {
            get { return query.Descending; }
            set { query.Descending = value; }
        }

        /// <summary>
        /// <para>
        /// The last key to be returned.  If <c>null</c>, the enumeration will continue to the end of the index.
        /// </para>
        /// <note>
        /// If the query is descending, the <see cref="EndKey"/> will be the lowest key to return and a <c>null</c>
        /// value means to stop at the lowest key of the index.
        /// </note>
        /// </summary>
        public object EndKey
        {
            get { return query.EndKey; }
            set { query.EndKey = value; }
        }

        /// <summary>
        /// The document ID of the last value to return. A <c>null</c> value has no effect.  This is useful if
        /// the view contains multiple identical keys, making <see cref="EndKey"/> ambiguous.
        /// </summary>
        public string EndKeyDocId
        {
            get { return query.EndKeyDocId; }
            set { query.EndKeyDocId = value; }
        }

        /// <summary>
        /// Indicates whether results will be grouped for views that have reduce functions.
        /// </summary>
        public int GroupLevel
        {
            get { return query.GroupLevel; }
            set { query.GroupLevel = value; }
        }

        /// <summary>
        /// Indicates whether deleted documents should be returned.  This defaults to <c>false</c>.
        /// </summary>
        public bool IncludeDeleted
        {
            get { return (query.AllDocsMode & AllDocsMode.IncludeDeleted) != 0; }
            set { query.AllDocsMode |= AllDocsMode.IncludeDeleted; }
        }

        /// <summary>
        /// Indicates whether rows whose key exactly matches <see cref="EndKey"/> should be
        /// returned.  This defaults to <c>true</c>.
        /// </summary>
        public bool InclusiveEnd
        {
            get { return query.InclusiveEnd; }
            set { query.InclusiveEnd = value; }
        }

        /// <summary>
        /// Indicates whether rows whose key exactly matches <see cref="StartKey"/> should be
        /// returned.  This defaults to <c>true</c>.
        /// </summary>
        public bool InclusiveStart
        {
            get { return query.InclusiveStart; }
            set { query.InclusiveStart = value; }
        }

        /// <summary>
        /// Controls when a <see cref="View"/> is updated when a query is running.
        /// </summary>
        public IndexUpdateMode IndexUpdateMode
        {
            get { return query.IndexUpdateMode; }
            set { query.IndexUpdateMode = value; }
        }

        /// <summary>
        /// The specific set of keys to be returned from the view.  A <c>null</c> value
        /// has no impact.
        /// </summary>
        public IEnumerable<object> Keys
        {
            get { return query.Keys; }
            set { query.Keys = value; }
        }

        /// <summary>
        /// The maximim number or rows to be returned  or <b>0</b> to return all rows.
        /// </summary>
        public int Limit
        {
            get { return query.Limit; }
            set { query.Limit = value; }
        }

        /// <summary>
        /// Indicates whether only the view's map function is to be used, ignorning the
        /// reduction function, it one was specified.
        /// </summary>
        public bool MapOnly
        {
            get { return query.MapOnly; }
            set { query.MapOnly = value; }
        }

        /// <summary>
        /// An optional predicate used to filters rows returned by the query.  The predicate
        /// should return <c>true</c> for rows to be included in the results or <c>false</c>'
        /// for rows to be ignored.
        /// </summary>
        public Func<EntityQueryRow<TEntity>, bool> PostFilter
        {
            get { return postFilter; }
            set { postFilter = value; }
        }

        /// <summary>
        /// The number of initial rows to be skipped.  This defaults to <b>0</b>.
        /// </summary>
        public int Skip
        {
            get { return query.Skip; }
            set { query.Skip = value; }
        }

        /// <summary>
        /// <para>
        /// The first key to return. If <c>null</c>, the enumeration will start at the first key in the index.
        /// </para>
        /// <note>
        /// Note that if the query is descending, the <see cref="StartKey"/> will be the highest key to return
        /// and a <c>null</c> value means to start at the highest key of the index.
        /// </note>
        /// </summary>
        public object StartKey
        {
            get { return query.StartKey; }
            set { query.StartKey = value; }
        }

        /// <summary>
        /// The document ID of the first value to return.  A <c>null</c> value has no effect.  This is useful if the 
        /// view contains multiple identical keys, making <see cref="StartKey"/> ambiguous. 
        /// </summary>
        public string StartKeyDocId
        {
            get { return query.StartKeyDocId; }
            set { query.StartKeyDocId = value; }
        }

        /// <summary>
        /// Runs the query returning an enumerator over the resulting rows. 
        /// </summary>
        /// <returns>A <see cref="EntityQueryEnumerator{TEntity}"/>.</returns>
        public EntityQueryEnumerator<TEntity> Run()
        {
            return new EntityQueryEnumerator<TEntity>(query.Run(), postFilter);
        }

        /// <summary>
        /// Asynchronously runs the query returning an enumerator over the resulting rows. 
        /// </summary>
        /// <returns>A <see cref="EntityQueryEnumerator{TEntity}"/>.</returns>
        public async Task<EntityQueryEnumerator<TEntity>> RunAsync()
        {
            return new EntityQueryEnumerator<TEntity>(await query.RunAsync(), postFilter);
        }

        /// <summary>
        /// Returns an entity live query with the same properties as this query.
        /// </summary>
        /// <param name="start">
        /// Optionally controls whether the live query will be started immediately.  
        /// This defaults to <c>true</c>.
        /// </param>
        /// <returns>A <see cref="EntityLiveQuery{TEntity}"/>.</returns>
        public EntityLiveQuery<TEntity> ToLiveQuery(bool start = true)
        {
            return new EntityLiveQuery<TEntity>(query.ToLiveQuery(), postFilter, start);
        }

        //---------------------------------------------------------------------
        // IDisposable implementation

        /// <inheritdoc/>
        public void Dispose()
        {
            var query = this.query;

            this.query = null;

            if (query != null)
            {
                query.Dispose();
            }
        }
    }
}

