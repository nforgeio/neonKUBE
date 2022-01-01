//-----------------------------------------------------------------------------
// FILE:	    NeonBucket.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Threading;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Analytics;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Monitoring;
using Couchbase.Core.Version;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.Management;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Views;

using Neon.Common;
using Neon.Data;
using Neon.Retry;
using Neon.Time;

#pragma warning disable 0618    // Allow calls to wrapped obsolete members.

// $todo(jefflill):
//
// The methods in this class do not override methods in a parent class so the
// <inheritdoc/> tags don't actually do anything.  This means that these methods
// won't generate any code documentation.
//
// I'm not entirely sure this is worth fixing since we're gravitating away from
// Couchbase to YugaByte.  Perhaps something to work on during a vacation.

namespace Couchbase
{
    /// <summary>
    /// Wraps an <see cref="IBucket"/> adding some additional capabilities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This bucket can be configured to ignore replicate and persist durability
    /// constraints.  You can explicitly pass <c>ignoreDurability=true</c> to the
    /// constructor for development and test environments where there may not be enough 
    /// cluster nodes to satisfy durability constraints.  If this is <c>null</c> (the default)
    /// then the bucket  will look for the presence of the <b>DEV_WORKSTATION</b> environment
    /// variable and ignore durability constraints if this variable exists.
    /// </para>
    /// <para>
    /// This means that production and staging environments that don't have the
    /// <b>DEV_WORKSTATION</b> environment variable will enforce durability by
    /// default and development/test environments that have this set will not
    /// enforce durability be default.
    /// </para>
    /// </remarks>
    public class NeonBucket : IBucket
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// <b>HACk:</b> Returns the timeout used when ensuring that a bucket is ready for use.
        /// </summary>
        public static TimeSpan ReadyTimeout { get; private set; } = TimeSpan.FromSeconds(120);

        /// <summary>
        /// <b>HACk:</b> Returns the retry policy used when ensuring that a bucket is ready for use.
        /// </summary>
        public static LinearRetryPolicy ReadyRetry { get; private set; } = new LinearRetryPolicy(TransientDetector.Always, maxAttempts: (int)ReadyTimeout.TotalSeconds, retryInterval: TimeSpan.FromSeconds(1));

        //---------------------------------------------------------------------
        // Instance members

        private IBucket             bucket;
        private CouchbaseSettings   settings;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="bucket">The underlying Couchbase bucket implementation.</param>
        /// <param name="settings">The Couchbase client settings.</param>
        /// <param name="ignoreDurability">Optionally configure the bucket to ignore durability parameters.</param>
        /// <remarks>
        /// <para>
        /// You may explicitly pass <paramref name="ignoreDurability"/><c>=true</c> for 
        /// development and test environments where there may not be enough cluster nodes to
        /// satisfy durability constraints.  If this is <c>null</c> (the default) then the bucket 
        /// will look for the presence of the <b>DEV_WORKSTATION</b> environment variable
        /// and ignore durability constraints if this variable exists, otherwise durability
        /// constraints will be honored.
        /// </para>
        /// </remarks>
        public NeonBucket(IBucket bucket, CouchbaseSettings settings, bool? ignoreDurability = null)
        {
            Covenant.Requires<ArgumentNullException>(bucket != null, nameof(bucket));
            Covenant.Requires<ArgumentNullException>(settings != null, nameof(settings));

            this.bucket   = bucket;
            this.settings = settings;

            if (ignoreDurability.HasValue)
            {
                this.IgnoreDurability = ignoreDurability.Value;
            }
            else
            {
                this.IgnoreDurability = Environment.GetEnvironmentVariable("DEV_WORKSTATION") != null;
            }
        }

        /// <summary>
        /// Indicates whether this bucket is ignoring any <see cref="ReplicateTo"/> and/or <see cref="PersistTo"/>
        /// durability constraints.  This is useful for unit testing where it'll be likely that there won't be
        /// enough Couchbase cluster nodes to satisfy durability constraints for production code.
        /// </summary>
        public bool IgnoreDurability { get; private set; }

        /// <summary>
        /// Adjusts a <see cref="ReplicateTo"/> parameter based on the bucket's
        /// durability mode.
        /// </summary>
        /// <param name="replicateTo">The input value.</param>
        /// <returns>The value to actually use.</returns>
        private ReplicateTo Adjust(ReplicateTo replicateTo)
        {
            return IgnoreDurability ? ReplicateTo.Zero : replicateTo;
        }

        /// <summary>
        /// Adjusts a <see cref="PersistTo"/> parameter based on the bucket's
        /// durability mode.
        /// </summary>
        /// <param name="persistTo">The input value.</param>
        /// <returns>The value to actually use.</returns>
        private PersistTo Adjust(PersistTo persistTo)
        {
            return IgnoreDurability ? PersistTo.Zero : persistTo;
        }

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> This is used in special situations where the internal
        /// backing bucket needs to be replaced.  This is currently used within the Couchbase
        /// test fixture.
        /// </summary>
        /// <param name="bucket">The bucket to be set or <c>null</c>.</param>
        public void SetInternalBucket(IBucket bucket)
        {
            Covenant.Requires<ArgumentNullException>(bucket != null, nameof(bucket));

            this.bucket = bucket;
        }

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Returns the internal bucket.
        /// </summary>
        /// <returns>The internal <see cref="IBucket"/>.</returns>
        public IBucket GetInternalBucket()
        {
            return this.bucket;
        }

        //-----------------------------------------------------------------
        // IBucket pass-thru implementations.

        /// <inheritdoc/>
        public string Name => bucket.Name;

        /// <inheritdoc/>
        public BucketTypeEnum BucketType => bucket.BucketType;

        /// <inheritdoc/>
        public ICluster Cluster => bucket.Cluster;

        /// <inheritdoc/>
        public bool IsSecure => bucket.IsSecure;

        /// <inheritdoc/>
        public bool SupportsEnhancedDurability => bucket.SupportsEnhancedDurability;

        /// <inheritdoc/>
        public bool SupportsKvErrorMap => bucket.SupportsKvErrorMap;

        /// <inheritdoc/>
        public BucketConfiguration Configuration => bucket.Configuration;

        /// <inheritdoc/>
        public IOperationResult<string> Append(string key, string value)
        {
            return bucket.Append(key, value);
        }

        /// <inheritdoc/>
        public IOperationResult<string> Append(string key, string value, TimeSpan timeout)
        {
            return bucket.Append(key, value, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<byte[]> Append(string key, byte[] value)
        {
            return bucket.Append(key, value);
        }

        /// <inheritdoc/>
        public IOperationResult<byte[]> Append(string key, byte[] value, TimeSpan timeout)
        {
            return bucket.Append(key, value, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<string>> AppendAsync(string key, string value)
        {
            return bucket.AppendAsync(key, value);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<string>> AppendAsync(string key, string value, TimeSpan timeout)
        {
            return bucket.AppendAsync(key, value, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<byte[]>> AppendAsync(string key, byte[] value)
        {
            return bucket.AppendAsync(key, value);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<byte[]>> AppendAsync(string key, byte[] value, TimeSpan timeout)
        {
            return bucket.AppendAsync(key, value, timeout);
        }

        /// <inheritdoc/>
        public IBucketManager CreateManager(string username, string password)
        {
            return bucket.CreateManager(username, password);
        }

        /// <inheritdoc/>
        public IBucketManager CreateManager()
        {
            return bucket.CreateManager();
        }

        /// <inheritdoc/>
        public IViewQuery CreateQuery(string designDoc, string view)
        {
            return bucket.CreateQuery(designDoc, view);
        }

        /// <inheritdoc/>
        public IViewQuery CreateQuery(string designdoc, string view, bool development)
        {
            return bucket.CreateQuery(designdoc, view, development);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Decrement(string key)
        {
            return bucket.Decrement(key);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Decrement(string key, TimeSpan timeout)
        {
            return bucket.Decrement(key, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Decrement(string key, ulong delta)
        {
            return bucket.Decrement(key, delta);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Decrement(string key, ulong delta, TimeSpan timeout)
        {
            return bucket.Decrement(key, delta, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial)
        {
            return bucket.Decrement(key, delta, initial);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, uint expiration)
        {
            return bucket.Decrement(key, delta, initial, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, uint expiration, TimeSpan timeout)
        {
            return bucket.Decrement(key, delta, initial, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, TimeSpan expiration)
        {
            return bucket.Decrement(key, delta, initial, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.Decrement(key, delta, initial, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> DecrementAsync(string key)
        {
            return bucket.DecrementAsync(key);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, TimeSpan timeout)
        {
            return bucket.DecrementAsync(key, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta)
        {
            return bucket.DecrementAsync(key, delta);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, TimeSpan timeout)
        {
            return bucket.DecrementAsync(key, delta, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial)
        {
            return bucket.DecrementAsync(key, delta, initial);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial, uint expiration)
        {
            return bucket.DecrementAsync(key, delta, initial, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial, uint expiration, TimeSpan timeout)
        {
            return bucket.DecrementAsync(key, delta, initial, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial, TimeSpan expiration)
        {
            return bucket.DecrementAsync(key, delta, initial, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.DecrementAsync(key, delta, initial, expiration, timeout);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            bucket.Dispose();
        }

        /// <inheritdoc/>
        public bool Exists(string key)
        {
            return bucket.Exists(key);
        }

        /// <inheritdoc/>
        public bool Exists(string key, TimeSpan timeout)
        {
            return bucket.Exists(key, timeout);
        }

        /// <inheritdoc/>
        public Task<bool> ExistsAsync(string key)
        {
            return bucket.ExistsAsync(key);
        }

        /// <inheritdoc/>
        public Task<bool> ExistsAsync(string key, TimeSpan timeout)
        {
            return bucket.ExistsAsync(key, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Get<T>(string key)
        {
            return bucket.Get<T>(key);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Get<T>(string key, TimeSpan timeout)
        {
            return bucket.Get<T>(key, timeout);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys)
        {
            return bucket.Get<T>(keys);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, TimeSpan timeout)
        {
            return bucket.Get<T>(keys, timeout);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options)
        {
            return bucket.Get<T>(keys, options);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options, TimeSpan timeout)
        {
            return bucket.Get<T>(keys, options, timeout);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options, int rangeSize)
        {
            return bucket.Get<T>(keys, options, rangeSize);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options, int rangeSize, TimeSpan timeout)
        {
            return bucket.Get<T>(keys, options, rangeSize, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> GetAndLock<T>(string key, uint expiration)
        {
            return bucket.GetAndLock<T>(key, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> GetAndLock<T>(string key, uint expiration, TimeSpan timeout)
        {
            return bucket.GetAndLock<T>(key, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> GetAndLock<T>(string key, TimeSpan expiration)
        {
            return bucket.GetAndLock<T>(key, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> GetAndLock<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.GetAndLock<T>(key, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, uint expiration)
        {
            return bucket.GetAndLockAsync<T>(key, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, uint expiration, TimeSpan timeout)
        {
            return bucket.GetAndLockAsync<T>(key, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, TimeSpan expiration)
        {
            return bucket.GetAndLockAsync<T>(key, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.GetAndLockAsync<T>(key, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> GetAndTouch<T>(string key, TimeSpan expiration)
        {
            return bucket.GetAndTouch<T>(key, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> GetAndTouch<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.GetAndTouch<T>(key, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> GetAndTouchAsync<T>(string key, TimeSpan expiration)
        {
            return bucket.GetAndTouchAsync<T>(key, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> GetAndTouchAsync<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.GetAndTouchAsync<T>(key, expiration, timeout);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> GetAndTouchDocument<T>(string key, TimeSpan expiration)
        {
            return bucket.GetAndTouchDocument<T>(key, expiration);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> GetAndTouchDocument<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.GetAndTouchDocument<T>(key, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> GetAndTouchDocumentAsync<T>(string key, TimeSpan expiration)
        {
            return bucket.GetAndTouchDocumentAsync<T>(key, expiration);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> GetAndTouchDocumentAsync<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.GetAndTouchDocumentAsync<T>(key, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> GetAsync<T>(string key)
        {
            return bucket.GetAsync<T>(key);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> GetAsync<T>(string key, TimeSpan timeout)
        {
            return bucket.GetAsync<T>(key, timeout);
        }

        /// <inheritdoc/>
        public ClusterVersion? GetClusterVersion()
        {
            return bucket.GetClusterVersion();
        }

        /// <inheritdoc/>
        public Task<ClusterVersion?> GetClusterVersionAsync()
        {
            return bucket.GetClusterVersionAsync();
        }

        /// <inheritdoc/>
        public IDocumentResult<T> GetDocument<T>(string id)
        {
            return bucket.GetDocument<T>(id);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> GetDocument<T>(string id, TimeSpan timeout)
        {
            return bucket.GetDocument<T>(id, timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> GetDocumentAsync<T>(string id)
        {
            return bucket.GetDocumentAsync<T>(id);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> GetDocumentAsync<T>(string id, TimeSpan timeout)
        {
            return bucket.GetDocumentAsync<T>(id, timeout);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> GetDocumentFromReplica<T>(string id)
        {
            return bucket.GetDocumentFromReplica<T>(id);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> GetDocumentFromReplica<T>(string id, TimeSpan timeout)
        {
            return bucket.GetDocumentFromReplica<T>(id, timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> GetDocumentFromReplicaAsync<T>(string id)
        {
            return bucket.GetDocumentFromReplicaAsync<T>(id);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> GetDocumentFromReplicaAsync<T>(string id, TimeSpan timeout)
        {
            return bucket.GetDocumentFromReplicaAsync<T>(id, timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> GetDocumentsAsync<T>(IEnumerable<string> ids)
        {
            return bucket.GetDocumentsAsync<T>(ids);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> GetDocumentsAsync<T>(IEnumerable<string> ids, TimeSpan timeout)
        {
            return bucket.GetDocumentsAsync<T>(ids, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> GetFromReplica<T>(string key)
        {
            return bucket.GetFromReplica<T>(key);
        }

        /// <inheritdoc/>
        public IOperationResult<T> GetFromReplica<T>(string key, TimeSpan timeout)
        {
            return bucket.GetFromReplica<T>(key, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> GetFromReplicaAsync<T>(string key)
        {
            return bucket.GetFromReplicaAsync<T>(key);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> GetFromReplicaAsync<T>(string key, TimeSpan timeout)
        {
            return bucket.GetFromReplicaAsync<T>(key, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> GetWithLock<T>(string key, uint expiration)
        {
            return bucket.GetWithLock<T>(key, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> GetWithLock<T>(string key, TimeSpan expiration)
        {
            return bucket.GetWithLock<T>(key, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> GetWithLockAsync<T>(string key, uint expiration)
        {
            return bucket.GetWithLockAsync<T>(key, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> GetWithLockAsync<T>(string key, TimeSpan expiration)
        {
            return bucket.GetWithLockAsync<T>(key, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Increment(string key)
        {
            return bucket.Increment(key);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Increment(string key, TimeSpan timeout)
        {
            return bucket.Increment(key, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Increment(string key, ulong delta)
        {
            return bucket.Increment(key, delta);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Increment(string key, ulong delta, TimeSpan timeout)
        {
            return bucket.Increment(key, delta, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Increment(string key, ulong delta, ulong initial)
        {
            return bucket.Increment(key, delta, initial);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Increment(string key, ulong delta, ulong initial, uint expiration)
        {
            return bucket.Increment(key, delta, initial, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Increment(string key, ulong delta, ulong initial, uint expiration, TimeSpan timeout)
        {
            return bucket.Increment(key, delta, initial, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Increment(string key, ulong delta, ulong initial, TimeSpan expiration)
        {
            return bucket.Increment(key, delta, initial, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<ulong> Increment(string key, ulong delta, ulong initial, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.Increment(key, delta, initial, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> IncrementAsync(string key)
        {
            return bucket.IncrementAsync(key);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, TimeSpan timeout)
        {
            return bucket.IncrementAsync(key, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta)
        {
            return bucket.IncrementAsync(key, delta);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, TimeSpan timeout)
        {
            return bucket.IncrementAsync(key, delta, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial)
        {
            return bucket.IncrementAsync(key, delta, initial);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial, uint expiration)
        {
            return bucket.IncrementAsync(key, delta, initial, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial, uint expiration, TimeSpan timeout)
        {
            return bucket.IncrementAsync(key, delta, initial, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial, TimeSpan expiration)
        {
            return bucket.IncrementAsync(key, delta, initial, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.IncrementAsync(key, delta, initial, expiration, timeout);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Insert<T>(IDocument<T> document)
        {
            return bucket.Insert<T>(document);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Insert<T>(IDocument<T> document, TimeSpan timeout)
        {
            return bucket.Insert<T>(document, timeout);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return bucket.Insert<T>(document, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.Insert<T>(document, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Insert<T>(document, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Insert<T>(document, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value)
        {
            return bucket.Insert<T>(key, value);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value, uint expiration)
        {
            return bucket.Insert<T>(key, value, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value, uint expiration, TimeSpan timeout)
        {
            return bucket.Insert<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration)
        {
            return bucket.Insert<T>(key, value, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.Insert<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo)
        {
            return bucket.Insert<T>(key, value, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.Insert<T>(key, value, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Insert<T>(key, value, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Insert<T>(key, value, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Insert<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Insert<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Insert<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Insert<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents)
        {
            return bucket.InsertAsync<T>(documents);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, TimeSpan timeout)
        {
            return bucket.InsertAsync<T>(documents, timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo)
        {
            return bucket.InsertAsync<T>(documents, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.InsertAsync<T>(documents, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.InsertAsync<T>(documents, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.InsertAsync<T>(documents, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document)
        {
            return bucket.InsertAsync<T>(document);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, TimeSpan timeout)
        {
            return bucket.InsertAsync<T>(document, timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return bucket.InsertAsync<T>(document, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.InsertAsync<T>(document, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.InsertAsync<T>(document, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.InsertAsync<T>(document, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value)
        {
            return bucket.InsertAsync<T>(key, value);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration)
        {
            return bucket.InsertAsync<T>(key, value, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration, TimeSpan timeout)
        {
            return bucket.InsertAsync<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration)
        {
            return bucket.InsertAsync<T>(key, value, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.InsertAsync<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo)
        {
            return bucket.InsertAsync<T>(key, value, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.InsertAsync<T>(key, value, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.InsertAsync<T>(key, value, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.InsertAsync<T>(key, value, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.InsertAsync<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.InsertAsync<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.InsertAsync<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.InsertAsync<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IResult ListAppend(string key, object value, bool createList)
        {
            return bucket.ListAppend(key, value, createList);
        }

        /// <inheritdoc/>
        public IResult ListAppend(string key, object value, bool createList, TimeSpan timeout)
        {
            return bucket.ListAppend(key, value, createList, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult> ListAppendAsync(string key, object value, bool createList)
        {
            return bucket.ListAppendAsync(key, value, createList);
        }

        /// <inheritdoc/>
        public Task<IResult> ListAppendAsync(string key, object value, bool createList, TimeSpan timeout)
        {
            return bucket.ListAppendAsync(key, value, createList, timeout);
        }

        /// <inheritdoc/>
        public IResult<TContent> ListGet<TContent>(string key, int index)
        {
            return bucket.ListGet<TContent>(key, index);
        }

        /// <inheritdoc/>
        public IResult<TContent> ListGet<TContent>(string key, int index, TimeSpan timeout)
        {
            return bucket.ListGet<TContent>(key, index, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult<TContent>> ListGetAsync<TContent>(string key, int index)
        {
            return bucket.ListGetAsync<TContent>(key, index);
        }

        /// <inheritdoc/>
        public Task<IResult<TContent>> ListGetAsync<TContent>(string key, int index, TimeSpan timeout)
        {
            return bucket.ListGetAsync<TContent>(key, index, timeout);
        }

        /// <inheritdoc/>
        public IResult ListPrepend(string key, object value, bool createList)
        {
            return bucket.ListPrepend(key, value, createList);
        }

        /// <inheritdoc/>
        public IResult ListPrepend(string key, object value, bool createList, TimeSpan timeout)
        {
            return bucket.ListPrepend(key, value, createList, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult> ListPrependAsync(string key, object value, bool createList)
        {
            return bucket.ListPrependAsync(key, value, createList);
        }

        /// <inheritdoc/>
        public Task<IResult> ListPrependAsync(string key, object value, bool createList, TimeSpan timeout)
        {
            return bucket.ListPrependAsync(key, value, createList, timeout);
        }

        /// <inheritdoc/>
        public IResult ListRemove(string key, int index)
        {
            return bucket.ListRemove(key, index);
        }

        /// <inheritdoc/>
        public IResult ListRemove(string key, int index, TimeSpan timeout)
        {
            return bucket.ListRemove(key, index, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult> ListRemoveAsync(string key, int index)
        {
            return bucket.ListRemoveAsync(key, index);
        }

        /// <inheritdoc/>
        public Task<IResult> ListRemoveAsync(string key, int index, TimeSpan timeout)
        {
            return bucket.ListRemoveAsync(key, index, timeout);
        }

        /// <inheritdoc/>
        public IResult ListSet(string key, int index, string value)
        {
            return bucket.ListSet(key, index, value);
        }

        /// <inheritdoc/>
        public IResult ListSet(string key, int index, string value, TimeSpan timeout)
        {
            return bucket.ListSet(key, index, value, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult> ListSetAsync(string key, int index, string value)
        {
            return bucket.ListSetAsync(key, index, value);
        }

        /// <inheritdoc/>
        public Task<IResult> ListSetAsync(string key, int index, string value, TimeSpan timeout)
        {
            return bucket.ListSetAsync(key, index, value, timeout);
        }

        /// <inheritdoc/>
        public IResult<int> ListSize(string key)
        {
            return bucket.ListSize(key);
        }

        /// <inheritdoc/>
        public IResult<int> ListSize(string key, TimeSpan timeout)
        {
            return bucket.ListSize(key, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult<int>> ListSizeAsync(string key)
        {
            return bucket.ListSizeAsync(key);
        }

        /// <inheritdoc/>
        public Task<IResult<int>> ListSizeAsync(string key, TimeSpan timeout)
        {
            return bucket.ListSizeAsync(key, timeout);
        }

        /// <inheritdoc/>
        public ILookupInBuilder<TDocument> LookupIn<TDocument>(string key)
        {
            return bucket.LookupIn<TDocument>(key);
        }

        /// <inheritdoc/>
        public ILookupInBuilder<TDocument> LookupIn<TDocument>(string key, TimeSpan timeout)
        {
            return bucket.LookupIn<TDocument>(key, timeout);
        }

        /// <inheritdoc/>
        public IResult MapAdd(string key, string mapkey, string value, bool createMap)
        {
            return bucket.MapAdd(key, mapkey, value, createMap);
        }

        /// <inheritdoc/>
        public IResult MapAdd(string key, string mapkey, string value, bool createMap, TimeSpan timeout)
        {
            return bucket.MapAdd(key, mapkey, value, createMap, timeout); ;
        }

        /// <inheritdoc/>
        public Task<IResult> MapAddAsync(string key, string mapkey, string value, bool createMap)
        {
            return bucket.MapAddAsync(key, mapkey, value, createMap);
        }

        /// <inheritdoc/>
        public Task<IResult> MapAddAsync(string key, string mapkey, string value, bool createMap, TimeSpan timeout)
        {
            return bucket.MapAddAsync(key, mapkey, value, createMap, timeout);
        }

        /// <inheritdoc/>
        public IResult<TContent> MapGet<TContent>(string key, string mapkey)
        {
            return bucket.MapGet<TContent>(key, mapkey);
        }

        /// <inheritdoc/>
        public IResult<TContent> MapGet<TContent>(string key, string mapkey, TimeSpan timeout)
        {
            return bucket.MapGet<TContent>(key, mapkey, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult<TContent>> MapGetAsync<TContent>(string key, string mapkey)
        {
            return bucket.MapGetAsync<TContent>(key, mapkey);
        }

        /// <inheritdoc/>
        public Task<IResult<TContent>> MapGetAsync<TContent>(string key, string mapkey, TimeSpan timeout)
        {
            return bucket.MapGetAsync<TContent>(key, mapkey, timeout);
        }

        /// <inheritdoc/>
        public IResult MapRemove(string key, string mapkey)
        {
            return bucket.MapRemove(key, mapkey);
        }

        /// <inheritdoc/>
        public IResult MapRemove(string key, string mapkey, TimeSpan timeout)
        {
            return bucket.MapRemove(key, mapkey, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult> MapRemoveAsync(string key, string mapkey)
        {
            return bucket.MapRemoveAsync(key, mapkey);
        }

        /// <inheritdoc/>
        public Task<IResult> MapRemoveAsync(string key, string mapkey, TimeSpan timeout)
        {
            return bucket.MapRemoveAsync(key, mapkey, timeout);
        }

        /// <inheritdoc/>
        public IResult<int> MapSize(string key)
        {
            return bucket.MapSize(key);
        }

        /// <inheritdoc/>
        public IResult<int> MapSize(string key, TimeSpan timeout)
        {
            return bucket.MapSize(key, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult<int>> MapSizeAsync(string key)
        {
            return bucket.MapSizeAsync(key);
        }

        /// <inheritdoc/>
        public Task<IResult<int>> MapSizeAsync(string key, TimeSpan timeout)
        {
            return bucket.MapSizeAsync(key, timeout);
        }

        /// <inheritdoc/>
        public IMutateInBuilder<TDocument> MutateIn<TDocument>(string key)
        {
            return bucket.MutateIn<TDocument>(key);
        }

        /// <inheritdoc/>
        public IMutateInBuilder<TDocument> MutateIn<TDocument>(string key, TimeSpan timeout)
        {
            return bucket.MutateIn<TDocument>(key, timeout);
        }

        /// <inheritdoc/>
        public ObserveResponse Observe(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Observe(key, cas, deletion, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public ObserveResponse Observe(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Observe(key, cas, deletion, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<ObserveResponse> ObserveAsync(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.ObserveAsync(key, cas, deletion, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<ObserveResponse> ObserveAsync(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.ObserveAsync(key, cas, deletion, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IPingReport Ping(params ServiceType[] services)
        {
            return bucket.Ping(services);
        }

        /// <inheritdoc/>
        public IPingReport Ping(string reportId, params ServiceType[] services)
        {
            return bucket.Ping(reportId, services);
        }

        /// <inheritdoc/>
        public IOperationResult<string> Prepend(string key, string value)
        {
            return bucket.Prepend(key, value);
        }

        /// <inheritdoc/>
        public IOperationResult<string> Prepend(string key, string value, TimeSpan timeout)
        {
            return bucket.Prepend(key, value, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<byte[]> Prepend(string key, byte[] value)
        {
            return bucket.Prepend(key, value);
        }

        /// <inheritdoc/>
        public IOperationResult<byte[]> Prepend(string key, byte[] value, TimeSpan timeout)
        {
            return bucket.Prepend(key, value, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<string>> PrependAsync(string key, string value)
        {
            return bucket.PrependAsync(key, value);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<string>> PrependAsync(string key, string value, TimeSpan timeout)
        {
            return bucket.PrependAsync(key, value, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<byte[]>> PrependAsync(string key, byte[] value)
        {
            return bucket.PrependAsync(key, value);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<byte[]>> PrependAsync(string key, byte[] value, TimeSpan timeout)
        {
            return bucket.PrependAsync(key, value, timeout);
        }

        /// <inheritdoc/>
        public IViewResult<T> Query<T>(IViewQueryable query)
        {
            return bucket.Query<T>(query);
        }

        /// <inheritdoc/>
        public IQueryResult<T> Query<T>(string query)
        {
            return bucket.Query<T>(query);
        }

        /// <inheritdoc/>
        public IQueryResult<T> Query<T>(IQueryRequest queryRequest)
        {
            return bucket.Query<T>(queryRequest);
        }

        /// <inheritdoc/>
        public IAnalyticsResult<T> Query<T>(IAnalyticsRequest analyticsRequest)
        {
            return bucket.Query<T>(analyticsRequest);
        }

        /// <inheritdoc/>
        public ISearchQueryResult Query(SearchQuery searchQuery)
        {
            return bucket.Query(searchQuery);
        }

        /// <inheritdoc/>
        public Task<IViewResult<T>> QueryAsync<T>(IViewQueryable query)
        {
            return bucket.QueryAsync<T>(query);
        }

        /// <inheritdoc/>
        public Task<IQueryResult<T>> QueryAsync<T>(string query)
        {
            return bucket.QueryAsync<T>(query);
        }

        /// <inheritdoc/>
        public Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest)
        {
            return bucket.QueryAsync<T>(queryRequest);
        }

        /// <inheritdoc/>
        public Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest, CancellationToken cancellationToken)
        {
            return bucket.QueryAsync<T>(queryRequest, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest analyticsRequest)
        {
            return bucket.QueryAsync<T>(analyticsRequest);
        }

        /// <inheritdoc/>
        public Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest analyticsRequest, CancellationToken cancellationToken)
        {
            return bucket.QueryAsync<T>(analyticsRequest, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<ISearchQueryResult> QueryAsync(SearchQuery searchQuery)
        {
            return bucket.QueryAsync(searchQuery);
        }

        /// <inheritdoc/>
        public Task<ISearchQueryResult> QueryAsync(SearchQuery searchQuery, CancellationToken cancellationToken)
        {
            return bucket.QueryAsync(searchQuery, cancellationToken);
        }

        /// <inheritdoc/>
        public IResult<T> QueuePop<T>(string key)
        {
            return bucket.QueuePop<T>(key);
        }

        /// <inheritdoc/>
        public IResult<T> QueuePop<T>(string key, TimeSpan timeout)
        {
            return bucket.QueuePop<T>(key, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult<T>> QueuePopAsync<T>(string key)
        {
            return bucket.QueuePopAsync<T>(key);
        }

        /// <inheritdoc/>
        public Task<IResult<T>> QueuePopAsync<T>(string key, TimeSpan timeout)
        {
            return bucket.QueuePopAsync<T>(key, timeout);
        }

        /// <inheritdoc/>
        public IResult QueuePush<T>(string key, T value, bool createQueue)
        {
            return bucket.QueuePush<T>(key, value, createQueue);
        }

        /// <inheritdoc/>
        public IResult QueuePush<T>(string key, T value, bool createQueue, TimeSpan timeout)
        {
            return bucket.QueuePush<T>(key, value, createQueue, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult> QueuePushAsync<T>(string key, T value, bool createQueue)
        {
            return bucket.QueuePushAsync<T>(key, value, createQueue);
        }

        /// <inheritdoc/>
        public Task<IResult> QueuePushAsync<T>(string key, T value, bool createQueue, TimeSpan timeout)
        {
            return bucket.QueuePushAsync<T>(key, value, createQueue, timeout);
        }

        /// <inheritdoc/>
        public IResult<int> QueueSize(string key)
        {
            return bucket.QueueSize(key);
        }

        /// <inheritdoc/>
        public IResult<int> QueueSize(string key, TimeSpan timeout)
        {
            return bucket.QueueSize(key, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult<int>> QueueSizeAsync(string key)
        {
            return bucket.QueueSizeAsync(key);
        }

        /// <inheritdoc/>
        public Task<IResult<int>> QueueSizeAsync(string key, TimeSpan timeout)
        {
            return bucket.QueueSizeAsync(key, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult Remove<T>(IDocument<T> document)
        {
            return bucket.Remove<T>(document);
        }

        /// <inheritdoc/>
        public IOperationResult Remove<T>(IDocument<T> document, TimeSpan timeout)
        {
            return bucket.Remove<T>(document, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return bucket.Remove<T>(document, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.Remove<T>(document, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Remove<T>(document, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Remove<T>(document, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult Remove(string key)
        {
            return bucket.Remove(key);
        }

        /// <inheritdoc/>
        public IOperationResult Remove(string key, TimeSpan timeout)
        {
            return bucket.Remove(key, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult Remove(string key, ulong cas)
        {
            return bucket.Remove(key, cas);
        }

        /// <inheritdoc/>
        public IOperationResult Remove(string key, ulong cas, TimeSpan timeout)
        {
            return bucket.Remove(key, cas, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult Remove(string key, ReplicateTo replicateTo)
        {
            return bucket.Remove(key, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public IOperationResult Remove(string key, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.Remove(key, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo)
        {
            return bucket.Remove(key, cas, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.Remove(key, cas, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult Remove(string key, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Remove(key, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult Remove(string key, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Remove(key, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Remove(key, cas, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Remove(key, cas, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult> Remove(IList<string> keys)
        {
            return bucket.Remove(keys);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult> Remove(IList<string> keys, TimeSpan timeout)
        {
            return bucket.Remove(keys, timeout);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult> Remove(IList<string> keys, ParallelOptions options)
        {
            return bucket.Remove(keys, options);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult> Remove(IList<string> keys, ParallelOptions options, TimeSpan timeout)
        {
            return bucket.Remove(keys, options, timeout);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult> Remove(IList<string> keys, ParallelOptions options, int rangeSize)
        {
            return bucket.Remove(keys, options, rangeSize);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult> Remove(IList<string> keys, ParallelOptions options, int rangeSize, TimeSpan timeout)
        {
            return bucket.Remove(keys, options, rangeSize, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document)
        {
            return bucket.RemoveAsync<T>(document);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, TimeSpan timeout)
        {
            return bucket.RemoveAsync<T>(document, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return bucket.RemoveAsync<T>(document, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.RemoveAsync<T>(document, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.RemoveAsync<T>(document, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.RemoveAsync<T>(document, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents)
        {
            return bucket.RemoveAsync<T>(documents);
        }

        /// <inheritdoc/>
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, TimeSpan timeout)
        {
            return bucket.RemoveAsync<T>(documents, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo)
        {
            return bucket.RemoveAsync<T>(documents, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.RemoveAsync<T>(documents, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.RemoveAsync<T>(documents, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.RemoveAsync<T>(documents, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync(string key)
        {
            return bucket.RemoveAsync(key);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync(string key, TimeSpan timeout)
        {
            return bucket.RemoveAsync(key, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync(string key, ulong cas)
        {
            return bucket.RemoveAsync(key, cas);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync(string key, ulong cas, TimeSpan timeout)
        {
            return bucket.RemoveAsync(key, cas, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo)
        {
            return bucket.RemoveAsync(key, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.RemoveAsync(key, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo)
        {
            return bucket.RemoveAsync(key, cas, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.RemoveAsync(key, cas, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.RemoveAsync(key, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.RemoveAsync(key, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.RemoveAsync(key, cas, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.RemoveAsync(key, cas, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Replace<T>(IDocument<T> document)
        {
            return bucket.Replace<T>(document);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, TimeSpan timeout)
        {
            return bucket.Replace<T>(document, timeout);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return bucket.Replace<T>(document, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.Replace<T>(document, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Replace<T>(document, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Replace<T>(document, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value)
        {
            return bucket.Replace<T>(key, value);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, uint expiration)
        {
            return bucket.Replace<T>(key, value, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, uint expiration, TimeSpan timeout)
        {
            return bucket.Replace<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, TimeSpan expiration)
        {
            return bucket.Replace<T>(key, value, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.Replace<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas)
        {
            return bucket.Replace<T>(key, value, cas);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration)
        {
            return bucket.Replace<T>(key, value, cas, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout)
        {
            return bucket.Replace<T>(key, value, cas, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration)
        {
            return bucket.Replace<T>(key, value, cas, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.Replace<T>(key, value, cas, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo)
        {
            return bucket.Replace<T>(key, value, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.Replace<T>(key, value, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo)
        {
            return bucket.Replace<T>(key, value, cas, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.Replace<T>(key, value, cas, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Replace<T>(key, value, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Replace<T>(key, value, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Replace<T>(key, value, cas, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Replace<T>(key, value, cas, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Replace<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Replace<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Replace<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Replace<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document)
        {
            return bucket.ReplaceAsync<T>(document);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(document, timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return bucket.ReplaceAsync<T>(document, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(document, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents)
        {
            return bucket.ReplaceAsync<T>(documents);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(documents, timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo)
        {
            return bucket.ReplaceAsync<T>(documents, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(documents, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.ReplaceAsync<T>(documents, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(documents, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.ReplaceAsync<T>(document, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(document, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value)
        {
            return bucket.ReplaceAsync<T>(key, value);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, uint expiration)
        {
            return bucket.ReplaceAsync<T>(key, value, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, uint expiration, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, TimeSpan expiration)
        {
            return bucket.ReplaceAsync<T>(key, value, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas)
        {
            return bucket.ReplaceAsync<T>(key, value, cas);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration)
        {
            return bucket.ReplaceAsync<T>(key, value, cas, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(key, value, cas, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration)
        {
            return bucket.ReplaceAsync<T>(key, value, cas, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(key, value, cas, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo)
        {
            return bucket.ReplaceAsync<T>(key, value, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(key, value, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo)
        {
            return bucket.ReplaceAsync<T>(key, value, cas, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(key, value, cas, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.ReplaceAsync<T>(key, value, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(key, value, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.ReplaceAsync<T>(key, value, cas, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(key, value, cas, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.ReplaceAsync<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.ReplaceAsync<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.ReplaceAsync<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IResult SetAdd(string key, string value, bool createSet)
        {
            return bucket.SetAdd(key, value, createSet);
        }

        /// <inheritdoc/>
        public IResult SetAdd(string key, string value, bool createSet, TimeSpan timeout)
        {
            return bucket.SetAdd(key, value, createSet, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult> SetAddAsync(string key, string value, bool createSet)
        {
            return bucket.SetAddAsync(key, value, createSet);
        }

        /// <inheritdoc/>
        public Task<IResult> SetAddAsync(string key, string value, bool createSet, TimeSpan timeout)
        {
            return bucket.SetAddAsync(key, value, createSet, timeout);
        }

        /// <inheritdoc/>
        public IResult<bool> SetContains(string key, string value)
        {
            return bucket.SetContains(key, value);
        }

        /// <inheritdoc/>
        public IResult<bool> SetContains(string key, string value, TimeSpan timeout)
        {
            return bucket.SetContains(key, value, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult<bool>> SetContainsAsync(string key, string value)
        {
            return bucket.SetContainsAsync(key, value);
        }

        /// <inheritdoc/>
        public Task<IResult<bool>> SetContainsAsync(string key, string value, TimeSpan timeout)
        {
            return bucket.SetContainsAsync(key, value, timeout);
        }

        /// <inheritdoc/>
        public IResult SetRemove<T>(string key, T value)
        {
            return bucket.SetRemove<T>(key, value);
        }

        /// <inheritdoc/>
        public IResult SetRemove<T>(string key, T value, TimeSpan timeout)
        {
            return bucket.SetRemove<T>(key, value, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult> SetRemoveAsync<T>(string key, T value)
        {
            return bucket.SetRemoveAsync<T>(key, value);
        }

        /// <inheritdoc/>
        public Task<IResult> SetRemoveAsync<T>(string key, T value, TimeSpan timeout)
        {
            return bucket.SetRemoveAsync<T>(key, value, timeout);
        }

        /// <inheritdoc/>
        public IResult<int> SetSize(string key)
        {
            return bucket.SetSize(key);
        }

        /// <inheritdoc/>
        public IResult<int> SetSize(string key, TimeSpan timeout)
        {
            return bucket.SetSize(key, timeout);
        }

        /// <inheritdoc/>
        public Task<IResult<int>> SetSizeAsync(string key)
        {
            return bucket.SetSizeAsync(key);
        }

        /// <inheritdoc/>
        public Task<IResult<int>> SetSizeAsync(string key, TimeSpan timeout)
        {
            return bucket.SetSizeAsync(key, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult Touch(string key, TimeSpan expiration)
        {
            return bucket.Touch(key, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult Touch(string key, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.Touch(key, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> TouchAsync(string key, TimeSpan expiration)
        {
            return bucket.TouchAsync(key, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> TouchAsync(string key, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.TouchAsync(key, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult Unlock(string key, ulong cas)
        {
            return bucket.Unlock(key, cas);
        }

        /// <inheritdoc/>
        public IOperationResult Unlock(string key, ulong cas, TimeSpan timeout)
        {
            return bucket.Unlock(key, cas, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> UnlockAsync(string key, ulong cas)
        {
            return bucket.UnlockAsync(key, cas);
        }

        /// <inheritdoc/>
        public Task<IOperationResult> UnlockAsync(string key, ulong cas, TimeSpan timeout)
        {
            return bucket.UnlockAsync(key, cas, timeout);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document)
        {
            return bucket.Upsert<T>(document);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, TimeSpan timeout)
        {
            return bucket.Upsert<T>(document, timeout);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return bucket.Upsert<T>(document, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.Upsert<T>(document, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Upsert<T>(document, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Upsert<T>(document, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value)
        {
            return bucket.Upsert<T>(key, value);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration)
        {
            return bucket.Upsert<T>(key, value, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration, TimeSpan timeout)
        {
            return bucket.Upsert<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration)
        {
            return bucket.Upsert<T>(key, value, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.Upsert<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas)
        {
            return bucket.Upsert<T>(key, value, cas);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration)
        {
            return bucket.Upsert<T>(key, value, cas, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout)
        {
            return bucket.Upsert<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration)
        {
            return bucket.Upsert<T>(key, value, cas, expiration);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.Upsert<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo)
        {
            return bucket.Upsert<T>(key, value, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.Upsert<T>(key, value, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Upsert<T>(key, value, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Upsert<T>(key, value, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Upsert<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Upsert<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Upsert<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Upsert<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Upsert<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.Upsert<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.Upsert<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items)
        {
            return bucket.Upsert<T>(items);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, TimeSpan timeout)
        {
            return bucket.Upsert<T>(items, timeout);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options)
        {
            return bucket.Upsert<T>(items, options);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options, TimeSpan timeout)
        {
            return bucket.Upsert<T>(items, options, timeout);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options, int rangeSize)
        {
            return bucket.Upsert<T>(items, options, rangeSize);
        }

        /// <inheritdoc/>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options, int rangeSize, TimeSpan timeout)
        {
            return bucket.Upsert<T>(items, options, rangeSize, timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document)
        {
            return bucket.UpsertAsync<T>(document);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(document, timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return bucket.UpsertAsync<T>(document, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(document, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.UpsertAsync<T>(document, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(document, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents)
        {
            return bucket.UpsertAsync<T>(documents);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(documents, timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo)
        {
            return bucket.UpsertAsync<T>(documents, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(documents, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.UpsertAsync<T>(documents, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(documents, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value)
        {
            return bucket.UpsertAsync<T>(key, value);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration)
        {
            return bucket.UpsertAsync<T>(key, value, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration)
        {
            return bucket.UpsertAsync<T>(key, value, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(key, value, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas)
        {
            return bucket.UpsertAsync<T>(key, value, cas);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration)
        {
            return bucket.UpsertAsync<T>(key, value, cas, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(key, value, cas, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration)
        {
            return bucket.UpsertAsync<T>(key, value, cas, expiration);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(key, value, cas, expiration, timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo)
        {
            return bucket.UpsertAsync<T>(key, value, Adjust(replicateTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(key, value, Adjust(replicateTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.UpsertAsync<T>(key, value, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(key, value, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.UpsertAsync<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.UpsertAsync<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.UpsertAsync<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(key, value, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return bucket.UpsertAsync<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo));
        }

        /// <inheritdoc/>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return bucket.UpsertAsync<T>(key, value, cas, expiration, Adjust(replicateTo), Adjust(persistTo), timeout);
        }

        /// <inheritdoc/>
        public string ExportDeferredAnalyticsQueryHandle<T>(IAnalyticsDeferredResultHandle<T> handle)
        {
            return bucket.ExportDeferredAnalyticsQueryHandle<T>(handle);
        }

        /// <inheritdoc/>
        public IAnalyticsDeferredResultHandle<T> ImportDeferredAnalyticsQueryHandle<T>(string encodedHandle)
        {
            return bucket.ImportDeferredAnalyticsQueryHandle<T>(encodedHandle);
        }
    }
}
