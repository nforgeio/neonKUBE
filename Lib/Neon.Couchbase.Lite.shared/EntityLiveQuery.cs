//-----------------------------------------------------------------------------
// FILE:	    EntityLiveQuery.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    /// Implements a live entity query that implements a <b>read-only</b> <see cref="IList{EntityQueryRow}"/>
    /// and <see cref="INotifyCollectionChanged"/> to provide for easy binding of query result rows
    /// to user interface elements.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class differs in behavior from the base <see cref="LiveQuery"/> implementation to
    /// provide for easier integration with user interface frameworks that support data binding,
    /// such as Xamarin.Forms, WPF, and the Windows platforms.
    /// </para>
    /// <para>
    /// Like the base live query, this class polls the associated <see cref="View"/> for changes
    /// that would impact the query results.  The difference is that this class is a read only
    /// <see cref="IList{T}"/> holding the <see cref="EntityQueryRow{TEntity}"/>
    /// instances returned by the query.  Instead of monitoring the <see cref="LiveQuery.Changed"/>
    /// event, applications will listen to the <see cref="ObservableCollection{T}.CollectionChanged"/>
    /// event.  The result rows can be accessed directly.
    /// </para>
    /// <para>
    /// Entity live queries are started immediately by default.  You can disable this by passing <b>start=</b><c>false</c>
    /// to <see cref="EntityQuery{TEntity}.ToLiveQuery(bool)"/> to have the query begin observing the view 
    /// when you're ready.  Call <see cref="Stop"/> to cease its observations.  The collection will be empty until the 
    /// view begins returning rows.  You can call <see cref="WaitForRows()"/> or <see cref="WaitForRowsAsync"/> to block
    /// until the initial query has completed.
    /// </para>
    /// <para>
    /// <see cref="LastError"/> can be used to detect errors and <see cref="UpdateInterval"/> can
    /// be used to customize the observation frequency.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public class EntityLiveQuery<TEntity> : IList<EntityQueryRow<TEntity>>, INotifyCollectionChanged
        where TEntity : class, IDynamicEntity, new()
    {
        private LiveQuery                             liveQuery;
        private List<EntityQueryRow<TEntity>>         rowList;
        private Func<EntityQueryRow<TEntity>, bool>   postFilter;

        /// <summary>
        /// Constructs an entity live query.
        /// </summary>
        /// <param name="liveQuery">The base <see cref="Query"/>.</param>
        /// <param name="postFilter">The post row folder or <c>null</c>.</param>
        /// <param name="start">Indicates that the live query should be started immediately.</param>
        internal EntityLiveQuery(LiveQuery liveQuery, Func<EntityQueryRow<TEntity>, bool> postFilter, bool start)
        {
            Covenant.Requires<ArgumentNullException>(liveQuery != null);

            this.liveQuery     = liveQuery;
            this.rowList       = new List<EntityQueryRow<TEntity>>();
            this.postFilter    = postFilter;
            liveQuery.Changed += OnChanged;

            if (start)
            {
                Start();
            }
        }

        /// <summary>
        /// Returns the <see cref="Exception"/> from the last query execution or <c>null</c>.
        /// </summary>
        public Exception LastError
        {
            get { return liveQuery.LastError; }
        }

        /// <summary>
        /// The minimum query update frequency.  This defaults to <b>200ms</b>.
        /// </summary>
        public TimeSpan UpdateInterval
        {
            get { return liveQuery.UpdateInterval; }

            set
            {
                Covenant.Requires<ArgumentException>(value >= TimeSpan.Zero);
                liveQuery.UpdateInterval = value;
            }
        }

        /// <summary>
        /// Starts the live query.
        /// </summary>
        public void Start()
        {
            liveQuery.Start();
        }

        /// <summary>
        /// Stops the live query.
        /// </summary>
        public void Stop()
        {
            liveQuery.Stop();
        }

        /// <summary>
        /// Waits for the first query to complete.
        /// </summary>
        public void WaitForRows()
        {
            liveQuery.WaitForRows();
        }

        /// <summary>
        /// Asynchronously waits for the first query to complete.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task WaitForRowsAsync()
        {
            await Task.Run(() => liveQuery.WaitForRows());
        }

        /// <summary>
        /// Called when the underlying query changes.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnChanged(object sender, QueryChangeEventArgs args)
        {
            // $todo(jeff.lill):
            //
            // For now, I'm simply going to update the entire collection.  In the future,
            // it may be possible to compare the current collection with the new query
            // results to try to minimize the changes.   This could result in a better
            // user experience.

            rowList.Clear();

            foreach (var row in liveQuery.Rows)
            {
                var entityRow = new EntityQueryRow<TEntity>(row);

                if (postFilter != null && !postFilter(entityRow))
                {
                    continue;
                }

                rowList.Add(entityRow);
            }

            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        //---------------------------------------------------------------------
        // INotifyCollectionChanged implementation

        /// <summary>
        /// Raised when the live query results change.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        //---------------------------------------------------------------------
        // IList implementation

        private const string ReadOnlyError = "Cannot modify entity live query collection because it is read-only.";

        /// <inheritdoc/>
        public int Count
        {
            get { return rowList.Count; }
        }

        /// <inheritdoc/>
        public bool IsReadOnly
        {
            get { return true; }
        }

        /// <inheritdoc/>
        public EntityQueryRow<TEntity> this[int index]
        {
            get { return rowList[index]; }

            set
            {
                throw new InvalidOperationException(ReadOnlyError);
            }
        }

        /// <inheritdoc/>
        public int IndexOf(EntityQueryRow<TEntity> item)
        {
            return rowList.IndexOf(item);
        }

        /// <inheritdoc/>
        public void Insert(int index, EntityQueryRow<TEntity> item)
        {
            throw new InvalidOperationException(ReadOnlyError);
        }

        /// <inheritdoc/>
        public void RemoveAt(int index)
        {
            throw new InvalidOperationException(ReadOnlyError);
        }

        /// <inheritdoc/>
        public void Add(EntityQueryRow<TEntity> item)
        {
            throw new InvalidOperationException(ReadOnlyError);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public bool Contains(EntityQueryRow<TEntity> item)
        {
            return rowList.Contains(item);
        }

        /// <inheritdoc/>
        public void CopyTo(EntityQueryRow<TEntity>[] array, int arrayIndex)
        {
            rowList.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc/>
        public bool Remove(EntityQueryRow<TEntity> item)
        {
            throw new InvalidOperationException(ReadOnlyError);
        }

        /// <inheritdoc/>
        public IEnumerator<EntityQueryRow<TEntity>> GetEnumerator()
        {
            return rowList.GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return rowList.GetEnumerator();
        }
    }
}
