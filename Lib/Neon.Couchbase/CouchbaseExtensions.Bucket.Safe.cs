//-----------------------------------------------------------------------------
// FILE:	    CouchbaseExtensions.Bucket.Safe.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Core;
using Couchbase.IO;
using Couchbase.Linq;
using Couchbase.Linq.Extensions;
using Couchbase.N1QL;

using Neon.Common;
using Neon.Data;
using Neon.Retry;
using Neon.Time;

namespace Couchbase
{
    public static partial class CouchbaseExtensions
    {
        // Implementation Notes:
        // ---------------------
        // The VerifySuccess() methods are used to examine the server responses
        // to determine whether a transient error has occurred and throw a
        // TransientException so that an upstack IRetryPolicy can handle things.
        //
        // There are family of edge cases around document mutations that make this
        // more complicated.  Here's one scenario:
        //
        //      1. A document is inserted with an IRetryPolicy with ReplicateTo > 0.
        //
        //      2. The document makes it to one cluster node but is not replicated
        //         in time to the other nodes before the operation times out.
        //
        //      3. Operation timeouts are considered transient, so the policy
        //         retries it.
        //
        //      4. Because the document did make it to a node, the retried insert
        //         fails because the key already exists and in a simple world,
        //         this would be reported back to the application as an exception
        //         (which would be really confusing).
        //
        // Similar situations will occur with remove as well as replace/upsert with CAS.
        //
        // I don't have a lot of experience with Couchbase yet, but I'll bet that
        // this issue is limited to operations where ReplicateTo and/or PersistTo
        // are greather than zero.  I don't think there's a transparent way to
        // handle these situations, so I'm going to avoid considering operation
        // timeouts as transient when there are replication/persistence constraints.

        /// <summary>
        /// Generates a globally unique document key.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <returns>A <see cref="Guid"/> formatted as a string.</returns>
        public static string GenKey(this IBucket bucket)
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }

        /// <summary>
        /// Determines whether a Couchbase response status code should be considered
        /// a transient error.
        /// </summary>
        /// <param name="status">The status code.</param>
        /// <param name="replicateOrPersist">Indicates whether the operation has replication or persistance constraints.</param>
        /// <returns><c>true</c> for a transient error.</returns>
        public static bool IsTransientStatus(ResponseStatus status, bool replicateOrPersist)
        {
            switch (status)
            {
                case ResponseStatus.OperationTimeout:

                    return !replicateOrPersist;

                case ResponseStatus.Busy:
                case ResponseStatus.NodeUnavailable:
                case ResponseStatus.TemporaryFailure:
                case ResponseStatus.TransportFailure:

                    return true;

                default:

                    return false;
            }
        }

        /// <summary>
        /// Throws an exception if an operation was not successful.
        /// </summary>
        /// <param name="result">The operation result.</param>
        /// <param name="replicateOrPersist">Indicates whether the operation has replication or persistance constraints.</param>
        public static void VerifySuccess(IOperationResult result, bool replicateOrPersist)
        {
            if (result.Success)
            {
                return;
            }

            if (result.ShouldRetry())
            {
                throw new TransientException(result.Message, result.Exception);
            }

            if (IsTransientStatus(result.Status, replicateOrPersist))
            {
                throw new TransientException($"Couchbase response status: {result.Status}", result.Exception);
            }

            result.EnsureSuccess();
        }

        /// <summary>
        /// Throws an exception if an operation was not successful.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="result">The operation result.</param>
        /// <param name="replicateOrPersist">Indicates whether the operation has replication or persistance constraints.</param>
        public static void VerifySuccess<T>(IOperationResult<T> result, bool replicateOrPersist)
        {
            if (result.Success)
            {
                return;
            }

            if (result.ShouldRetry())
            {
                throw new TransientException(result.Message, result.Exception);
            }

            if (IsTransientStatus(result.Status, replicateOrPersist))
            {
                throw new TransientException($"Couchbase response status: {result.Status}", result.Exception);
            }

            result.EnsureSuccess();
        }

        /// <summary>
        /// Throws an exception if a document operation was not successful.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="result">The operation result.</param>
        /// <param name="replicateOrPersist">Indicates whether the operation has replication or persistance constraints.</param>
        public static void VerifySuccess<T>(IDocumentResult<T> result, bool replicateOrPersist)
        {
            if (result.Success)
            {
                return;
            }

            if (result.ShouldRetry())
            {
                throw new TransientException(result.Message, result.Exception);
            }

            if (IsTransientStatus(result.Status, replicateOrPersist))
            {
                throw new TransientException($"Couchbase response status: {result.Status}", result.Exception);
            }

            result.EnsureSuccess();
        }

        /// <summary>
        /// Throws an exception if a query operation was not successful.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="result">The operation result.</param>
        /// <exception cref="CouchbaseResponseException">Thrown for errors.</exception>
        /// <exception cref="TransientException">Thrown if the error is potentially transient and the operation should be retried.</exception>
        /// <remarks>
        /// <para>
        /// This method is similar to the built-in Couchbase
        /// <see cref="ResponseExtensions.EnsureSuccess{T}(IQueryResult{T})"/>
        /// method, but may be better for many situations for these reasons:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     This method includes information about the specific errors detected.
        ///     <see cref="ResponseExtensions.EnsureSuccess{T}(IQueryResult{T})"/>
        ///     only returns a generic <b>Fatal Error</b> message and expects you
        ///     to examine the <see cref="CouchbaseQueryResponseException.Errors"/> 
        ///     property in your code.  This methods does that for you by including
        ///     the errors in the exception message so that that they will be included
        ///     in any diagnostic logging your doing without any additional effort.
        ///     </item>
        ///     <item>
        ///     This method throws a <see cref="TransientException"/> if the
        ///     error indicates that it should be retried.  This makes it easy
        ///     to use a Neon <see cref="IRetryPolicy"/> to perform retries.
        ///     </item>
        /// </list>
        /// </remarks>
        public static void VerifySuccess<T>(IQueryResult<T> result)
        {
            if (result.Success)
            {
                return;
            }

            // Build a better exception message that includes the actual error
            // codes and messages.

            var message = string.Empty;

            foreach (var error in result.Errors)
            {
                message += $"Couchbase Error [code={error.Code}]: {error.Message}{NeonHelper.LineEnding}";
            }

            if (result.Exception != null)
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = result.Exception.Message;
                }
                else
                {
                    message += ": " + result.Exception.Message;
                }
            }

            if (result.ShouldRetry())
            {
                throw new TransientException(message, result.Exception);
            }

            try
            {
                result.EnsureSuccess();
            }
            catch (CouchbaseQueryResponseException e)
            {
                if (e.InnerException == null)
                {
                    message = e.Message;
                }
                else
                {
                    message = e.InnerException.Message;
                }

                throw new CouchbaseQueryResponseException(message, e.Status, e.Errors.ToList());
            }
        }

        /// <summary>
        /// Performs small read/query operations to verify that the database connection 
        /// is healthy.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CouchbaseResponseException">Thrown if the bucket is not ready.</exception>
        public static async Task CheckAsync(this IBucket bucket)
        {
            // Note that it doesn't matter if this key actually exists 
            // in the database.  We're just verifying that the database
            // is ready to handle the operation.

            await bucket.FindSafeAsync<string>("neon-healthcheck");
        }

        /// <summary>
        /// Waits until the bucket is ready.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="timeout">Optionally specifies the maximum time to wait (defaults to 60 seconds).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TimeoutException">Thrown if the operation timed out.</exception>
        public static async Task WaitUntilReadyAsync(this IBucket bucket, TimeSpan timeout = default)
        {
            if (timeout == TimeSpan.Zero)
            {
                timeout = TimeSpan.FromSeconds(60);
            }

            // Perform some small read operations until we see no exceptions.

            var timer     = new PolledTimer(timeout);
            var exception = (Exception)null;

            while (!timer.HasFired)
            {
                try
                {
                    await bucket.CheckAsync();

                    // It also appears to take some more time for the bucket/cluster
                    // to be able to honor durability constraints.  We'll wait a
                    // bit longer if these constraints aren't disabled.

                    var neonBucket = bucket as NeonBucket;

                    if (neonBucket == null || !neonBucket.IgnoreDurability)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }

                    return;
                }
                catch (CouchbaseResponseException e)
                {
                    // Ignore these until the bucket is ready or we timeout.

                    exception = e;
                }
                catch (AggregateException e)
                {
                    if (e.Find<CouchbaseQueryResponseException>() != null ||
                        e.Find<CouchbaseResponseException>() != null ||
                        e.Find<TransientException>() != null)
                    {
                        // Ignore these until the bucket is ready or we timeout.

                        exception = e;
                    }
                    else
                    {
                        throw;
                    }
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(500));
            }

            throw new TimeoutException($"Timeout waiting for [bucket={bucket.Name}] to become ready within [{timeout}].", exception);
        }

        /// <summary>
        /// <para>
        /// Waits for any pending database updates to be indexed.  This can be used to
        /// implement <b>read your own writes.</b>.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> This is intended for use only for databases with a
        /// <b>#primary</b> index.
        /// </note>
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        public static void WaitForIndexer(this IBucket bucket)
        {
            var queryRequest = QueryRequest.Create($"select * from `{bucket.Name}` limit 0")
                .ScanConsistency(ScanConsistency.RequestPlus);

            bucket.Query<dynamic>(queryRequest);
        }

        /// <summary>
        /// <para>
        /// Waits for any pending database updates to be indexed.  This can be used to
        /// implement <b>read your own writes.</b>.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> This is intended for use only for databases with a
        /// <b>#primary</b> index.
        /// </note>
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <param name="bucket">The bucket.</param>
        public static async Task WaitForIndexerAsync(this IBucket bucket)
        {
            var queryRequest = QueryRequest.Create($"select * from `{bucket.Name}` limit 1")
                .ScanConsistency(ScanConsistency.RequestPlus);

            await bucket.QueryAsync<dynamic>(queryRequest);
        }

        /// <summary>
        /// Appends a byte array to a key, throwing an exception on failures.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<byte[]>> AppendSafeAsync(this IBucket bucket, string key, byte[] value)
        {
            var result = await bucket.AppendAsync(key, value);

            VerifySuccess<byte[]>(result, replicateOrPersist: false);

            return result;
        }

        /// <summary>
        /// Appends a string to a key, throwing an exception on failures.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<string>> AppendAsync(this IBucket bucket, string key, string value)
        {
            var result = await bucket.AppendAsync(key, value);

            VerifySuccess<string>(result, replicateOrPersist: false);

            return result;
        }

        /// <summary>
        /// Decrements the value of a key by one.  If the key doesn't exist, it will be
        /// created and initialized to <paramref name="initial"/>.  This method will throw
        /// an exception on failures.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="delta">The amount to decrement by (defaults to <b>1</b>).</param>
        /// <param name="initial">The initial value to use if the key doesn't already exist (defaults to <b>1</b>).</param>
        /// <param name="expiration">The expiration TTL (defaults to none).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<ulong>> DecrementSafeAsync(this IBucket bucket, string key, ulong delta = 1, ulong initial = 1, TimeSpan expiration = default)
        {
            IOperationResult<ulong> result;

            if (expiration > TimeSpan.Zero)
            {
                result = await bucket.DecrementAsync(key, delta, initial, expiration);
            }
            else
            {
                result = await bucket.DecrementAsync(key, delta, initial);
            }

            VerifySuccess<ulong>(result, replicateOrPersist: false);

            return result;
        }

        /// <summary>
        /// Checks for the existance of a key, throwing an exception on failures.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <returns><c>true</c> if the key exists.</returns>
        public static async Task<bool> ExistsSafeAsync(IBucket bucket, string key)
        {
            // This doesn't actually return a testable result but we'll still
            // implement the "safe" version to be consistent.

            return await bucket.ExistsAsync(key);
        }

        /// <summary>
        /// Attempts to retrieve a key value, returning <c>null</c> if it doesn't exist rather
        /// than throwing an exception.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <returns>The value or <c>null</c>.</returns>
        public static async Task<T> FindSafeAsync<T>(this IBucket bucket, string key)
            where T : class
        {
            var result = await bucket.GetAsync<T>(key);

            if (result.Exception is DocumentDoesNotExistException)
            {
                return null;
            }

            VerifySuccess<T>(result, replicateOrPersist: false);

            return result.Value;
        }

        /// <summary>
        /// Attemps to retrieve a document, returning <c>null</c> if it doesn't exist rather
        /// than throwing an exception.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <returns>The value or <c>null</c>.</returns>
        public static async Task<IDocument<T>> FindDocumentSafeAsync<T>(this IBucket bucket, string key)
            where T : class
        {
            var result = await bucket.GetDocumentAsync<T>(key);

            if (result.Exception is DocumentDoesNotExistException)
            {
                return null;
            }

            VerifySuccess<T>(result, replicateOrPersist: false);

            return result.Document;
        }

        /// <summary>
        /// Gets a key and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="expiration">The interval after which the document will be locked.  This defaults to 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<T>> GetAndLockSafeAsync<T>(this IBucket bucket, string key, TimeSpan expiration = default)
        {
            if (expiration <= TimeSpan.Zero)
            {
                expiration = TimeSpan.FromSeconds(15);
            }

            var result = await bucket.GetAndLockAsync<T>(key, expiration);

            VerifySuccess<T>(result, replicateOrPersist: false);

            return result;
        }

        /// <summary>
        /// Gets a key and updates its expiry with a new value.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="expiration">The optional new expiry timespan.</param>
        /// <returns>The value.</returns>
        public static async Task<T> GetAndTouchSafeAsync<T>(this IBucket bucket, string key, TimeSpan expiration)
        {
            var result = await bucket.GetAndTouchAsync<T>(key, expiration);

            VerifySuccess<T>(result, replicateOrPersist: false);

            return result.Value;
        }

        /// <summary>
        /// Gets a document and updates its expiry with a new value.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="expiration">The optional new expiry timespan.</param>
        /// <returns>The document.</returns>
        public static async Task<Document<T>> GetAndTouchDocumentSafeAsync<T>(this IBucket bucket, string key, TimeSpan expiration)
        {
            var result = await bucket.GetAndTouchDocumentAsync<T>(key, expiration);

            VerifySuccess<T>(result, replicateOrPersist: false);

            return result.Document;
        }

        /// <summary>
        /// Gets a key value from the database, throwing an exception if the key does not exist
        /// or there was another error.  
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <returns>The value.</returns>
        public static async Task<T> GetSafeAsync<T>(this IBucket bucket, string key)
        {
            var result = await bucket.GetAsync<T>(key);

            VerifySuccess<T>(result, replicateOrPersist: false);

            return result.Value;
        }

        /// <summary>
        /// Gets a document, throwing an exception if the document does not exist or there
        /// was another error.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="keys">The key.</param>
        /// <param name="expiration">The optional new expiry timespan.</param>
        /// <returns>The document.</returns>
        public static async Task<Document<T>> GetDocumentSafeAsync<T>(this IBucket bucket, string keys, TimeSpan expiration)
        {
            var result = await bucket.GetDocumentAsync<T>(keys);

            VerifySuccess<T>(result, replicateOrPersist: false);

            return result.Document;
        }

        /// <summary>
        /// Gets a set of documents, throwing an exception if any document does not exist or there
        /// was another error.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="keys">The keys.</param>
        /// <returns>The documents.</returns>
        public static async Task<IDocument<T>[]> GetDocumentSafeAsync<T>(this IBucket bucket, IEnumerable<string> keys)
        {
            var results = await bucket.GetDocumentsAsync<T>(keys);

            foreach (var result in results)
            {
                VerifySuccess<T>(result, replicateOrPersist: false);
            }

            var documents = new IDocument<T>[results.Length];

            for (int i = 0; i < results.Length; i++)
            {
                documents[i] = results[i].Document;
            }

            return documents;
        }

        /// <summary>
        /// Gets a key value from a Couchbase replica node, throwing an exception if the key does
        /// not exist or there was another error.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <returns>The value.</returns>
        public static async Task<T> GetFromReplicaSafeAsync<T>(this IBucket bucket, string key)
        {
            var result = await bucket.GetFromReplicaAsync<T>(key);

            VerifySuccess<T>(result, replicateOrPersist: false);

            return result.Value;
        }

        /// <summary>
        /// Increments the value of a key by one.  If the key doesn't exist, it will be
        /// created and initialized to <paramref name="initial"/>.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="delta">The amount to increment by (defaults to <b>1</b>).</param>
        /// <param name="initial">The initial value to use if the key doesn't already exist (defaults to <b>1</b>).</param>
        /// <param name="expiration">The expiration TTL (defaults to none).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<ulong>> IncrementSafeAsync(this IBucket bucket, string key, ulong delta = 1, ulong initial = 1, TimeSpan expiration = default)
        {
            IOperationResult<ulong> result;

            if (expiration > TimeSpan.Zero)
            {
                result = await bucket.IncrementAsync(key, delta, initial, expiration);
            }
            else
            {
                result = await bucket.IncrementAsync(key, delta, initial);
            }

            VerifySuccess<ulong>(result, replicateOrPersist: false);

            return result;
        }

        /// <summary>
        /// Inserts a key, throwing an exception if the key already exists or there
        /// was another error.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result</returns>
        public static async Task<IOperationResult<T>> InsertSafeAsync<T>(this IBucket bucket, string key, T value, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var result = await bucket.InsertAsync<T>(key, value, replicateTo, persistTo);

            VerifySuccess<T>(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Inserts a key with an expiration TTL, throwing an exception if the key already exists or there
        /// was another error.  Note that 30 seconds is the maximum expiration TTL supported by the
        /// server.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="expiration">The expiration TTL.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result</returns>
        public static async Task<IOperationResult<T>> InsertSafeAsync<T>(this IBucket bucket, string key, T value, TimeSpan expiration, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var result = await bucket.InsertAsync<T>(key, value, expiration, replicateTo, persistTo);

            VerifySuccess<T>(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Inserts a document, throwing an exception if the document already exists or there
        /// was another error.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="document">The document.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result</returns>
        public static async Task<IDocumentResult<T>> InsertSafeAsync<T>(this IBucket bucket, IDocument<T> document, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var result = await bucket.InsertAsync<T>(document, replicateTo, persistTo);

            VerifySuccess<T>(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Inserts multiple documents, throwing an exception if any of the documents already exists or there
        /// was another error.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation results.</returns>
        public static async Task<IDocumentResult<T>[]> InsertSafeAsync<T>(this IBucket bucket, List<IDocument<T>> documents, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var results = await bucket.InsertAsync<T>(documents, replicateTo, persistTo);

            foreach (var result in results)
            {
                VerifySuccess<T>(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            }

            return results;
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document, throwing an exception if the document already exists or there
        /// was another error.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="persistable">The document.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result</returns>
        public static async Task<IOperationResult<T>> InsertSafeAsync<T>(this IBucket bucket, T persistable, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
            where T: class, IPersistableType
        {
            var result = await bucket.InsertAsync<T>(persistable.GetKey(), persistable, replicateTo, persistTo);

            VerifySuccess<T>(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document, with an expiration TTL, throwing an exception if the key already exists or there
        /// was another error.  Note that 30 seconds is the maximum expiration TTL supported by the
        /// server.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="persistable">The document.</param>
        /// <param name="expiration">The expiration TTL.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<T>> InsertSafeAsync<T>(this IBucket bucket, T persistable, TimeSpan expiration, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
            where T: class, IPersistableType
        {
            var result = await bucket.InsertAsync<T>(persistable.GetKey(), persistable, expiration, replicateTo, persistTo);

            VerifySuccess<T>(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Executes a query request, throwing an exception if there were any errors.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="queryRequest">The query request.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The list of results.</returns>
        public static async Task<List<T>> QuerySafeAsync<T>(this IBucket bucket, IQueryRequest queryRequest, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(queryRequest != null, nameof(queryRequest));

            // $todo(jefflill): This is a horrible hack!
            //
            // My [Test_AnsibleCouchbaseImport] unit tests were failing due to what
            // looks like a transient query exception that doesn't happen for the 
            // first test but then fails for most of the subsequent tests in any 
            // given run.
            //
            // The weird thing is that I currently have the [CouchbaseFixture] perform
            // the exect same query to ensure that the query service is ready before
            // the test actually runs, so I'm not sure what the problem is.
            //
            // I've tried monkeying with timeouts and also trying very hard to ensure
            // that I close/dispose the Couchbase client cluster and bucket between
            // runs to no avail.
            //
            // This hack examines the query statement and will retry SELECT queries 
            // which are naturally idempotent.  Other query types won't be retried.
            //
            // I need to move on to some other tasks now, but I do need to come back 
            // some point and really try to nail this down.

            var qr = queryRequest as QueryRequest;

            if (qr != null && qr.GetOriginalStatement().Trim().StartsWith("select ", StringComparison.InvariantCultureIgnoreCase))
            {
                var retry = new LinearRetryPolicy(typeof(CouchbaseQueryResponseException), maxAttempts: 5, retryInterval: TimeSpan.FromSeconds(1));

                return await retry.InvokeAsync(
                    async () =>
                    {
                        var result = await bucket.QueryAsync<T>(queryRequest, cancellationToken);

                        VerifySuccess<T>(result);

                        return result.Rows;
                    });
            }
            else
            {
                var result = await bucket.QueryAsync<T>(queryRequest, cancellationToken);

                VerifySuccess<T>(result);

                return result.Rows;
            }
        }

        /// <summary>
        /// Executes a N1QL string query, throwing an exception if there were any errors.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="query">The N1QL query string.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The list of results.</returns>
        public static async Task<List<T>> QuerySafeAsync<T>(this IBucket bucket, string query, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(query), nameof(query));

            return await QuerySafeAsync<T>(bucket, new QueryRequest(query), cancellationToken);
        }

        /// <summary>
        /// Executes a query request after ensuring that the indexes have caught
        /// up to the specified mutation state, throwing an exception if there were
        /// any errors.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="queryRequest">The query request.</param>
        /// <param name="mutationState">
        /// Specifies the required index mutation state that must be satisfied before
        /// the query will be executed.
        /// </param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The list of results.</returns>
        public static async Task<List<T>> QuerySafeAsync<T>(this IBucket bucket, IQueryRequest queryRequest, MutationState mutationState, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(queryRequest != null, nameof(queryRequest));
            Covenant.Requires<ArgumentNullException>(mutationState != null, nameof(mutationState));

            // $todo(jefflill): This is a horrible hack!
            //
            // My [Test_AnsibleCouchbaseImport] unit tests were failing due to what
            // looks like a transient query exception that doesn't happen for the 
            // first test but then fails for most of the subsequent tests in any 
            // given run.
            //
            // The weird thing is that I currently have the [CouchbaseFixture] perform
            // the exect same query to ensure that the query service is ready before
            // the test actually runs, so I'm not sure what the problem is.
            //
            // I've tried monkeying with timeouts and also trying very hard to ensure
            // that I close/dispose the Couchbase client cluster and bucket between
            // runs to no avail.
            //
            // This hack examines the query statement and will retry SELECT queries 
            // which are naturally idempotent.  Other query types won't be retried.
            //
            // I need to move on to some other tasks now, but I do need to come back 
            // some point and really try to nail this down.

            var qr = queryRequest as QueryRequest;

            qr.ConsistentWith(mutationState);

            if (qr != null && qr.GetOriginalStatement().Trim().StartsWith("select ", StringComparison.InvariantCultureIgnoreCase))
            {
                var retry = new LinearRetryPolicy(typeof(CouchbaseQueryResponseException), maxAttempts: 5, retryInterval: TimeSpan.FromSeconds(1));

                return await retry.InvokeAsync(
                    async () =>
                    {
                        var result = await bucket.QueryAsync<T>(queryRequest, cancellationToken);

                        VerifySuccess<T>(result);

                        return result.Rows;
                    });
            }
            else
            {
                var result = await bucket.QueryAsync<T>(queryRequest, cancellationToken);

                VerifySuccess<T>(result);

                return result.Rows;
            }
        }

        /// <summary>
        /// Executes a N1QL string query, after ensuring that the indexes have caught
        /// up to the specified mutation state, throwing an exception if there were
        /// any errors.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="query">The N1QL query string.</param>
        /// <param name="mutationState">
        /// Specifies the required index mutation state that must be satisfied before
        /// the query will be executed.
        /// </param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The list of results.</returns>
        public static async Task<List<T>> QuerySafeAsync<T>(this IBucket bucket, string query, MutationState mutationState, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(query), nameof(query));

            return await QuerySafeAsync<T>(bucket, new QueryRequest(query), mutationState, cancellationToken);
        }

        /// <summary>
        /// Removes a document throwning an exception if there were any errors.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="document">The document to be deleted.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult> RemoveSafeAsync(this IBucket bucket, IDocument<Task> document, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var result = await bucket.RemoveAsync(document, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Removes multiple documents, throwing an exception if there were any errors.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="documents">The document to be deleted.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation results.</returns>
        public static async Task<IOperationResult[]> RemoveSafeAsync(this IBucket bucket, List<IDocument<Task>> documents, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var results = await bucket.RemoveAsync(documents, replicateTo, persistTo);

            foreach (var result in results)
            {
                VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            }

            return results;
        }

        /// <summary>
        /// Removes a key, throwning an exception if there were any errors.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key to be deleted.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult> RemoveSafeAsync(this IBucket bucket, string key, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var result = await bucket.RemoveAsync(key, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Removes an <see cref="IPersistableType"/> document,  throwning an exception if there were any errors.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="persistable">The document to be deleted.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult> RemoveSafeAsync(this IBucket bucket, IPersistableType persistable, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            Covenant.Requires<ArgumentNullException>(persistable != null, nameof(persistable));

            var result = await bucket.RemoveAsync(persistable.GetKey(), replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Replaces an existing document, throwing an exception if there were any errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="document">The replacement document.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IDocumentResult<T>> ReplaceSafeAsync<T>(this IBucket bucket, IDocument<T> document, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var result = await bucket.ReplaceAsync<T>(document, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Replaces multiple documents, throwing an exception if there were any errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="documents">The replacement documents.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation results.</returns>
        public static async Task<IDocumentResult<T>[]> ReplaceSafeAsync<T>(this IBucket bucket, List<IDocument<T>> documents, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var results            = await bucket.ReplaceAsync<T>(documents, replicateTo, persistTo);
            var replicateOrPersist = replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero;

            foreach (var result in results)
            {
                VerifySuccess(result, replicateOrPersist);
            }

            return results;
        }

        /// <summary>
        /// Replaces a key value, throwing an exception if there were any errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The replacement value.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<T>> ReplaceSafeAsync<T>(this IBucket bucket, string key, T value, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var result = await bucket.ReplaceAsync<T>(key, value, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Replaces a key value, optionally specifying a CAS value and throwing an exception
        /// if there were any errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The replacement value.</param>
        /// <param name="cas">The optional CAS value.</param>
        /// <param name="expiration">Optional expiration TTL.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task ReplaceSafeAsync<T>(this IBucket bucket, string key, T value, ulong? cas = null, TimeSpan? expiration = null, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            IOperationResult<T> result;
            var                 replicateOrPersist = replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero;

            if (cas.HasValue && expiration.HasValue)
            {
                result = await bucket.ReplaceAsync<T>(key, value, cas.Value, expiration.Value, replicateTo, persistTo);
            }
            else if (cas.HasValue)
            {
                result = await bucket.ReplaceAsync<T>(key, value, cas.Value, replicateTo, persistTo);
            }
            else if (expiration.HasValue)
            {
                // $todo(jefflill):
                //
                // There doesn't appear to be a way to do this in one API call because
                // there isn't an override that doesn't include a CAS parameter.  Research
                // whether it's possible to pass something like 0 or -1 as the CAS to
                // disable CAS behavior.

                var result1 = await bucket.ReplaceAsync<T>(key, value, replicateTo, persistTo);

                VerifySuccess<T>(result1, replicateOrPersist);

                var result2 = await bucket.TouchAsync(key, expiration.Value);

                VerifySuccess(result2, replicateOrPersist);
                return;
            }
            else
            {
                result = await bucket.ReplaceAsync<T>(key, value, replicateTo, persistTo);
            }

            VerifySuccess<T>(result, replicateOrPersist);
        }

        /// <summary>
        /// Removes an <see cref="IPersistableType"/> document, throwing an exception if there were any errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="persistable">The replacement document.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<T>> ReplaceSafeAsync<T>(this IBucket bucket, T persistable, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(persistable != null, nameof(persistable));

            var result = await bucket.ReplaceAsync<T>(persistable.GetKey(), persistable, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Removes an <see cref="IPersistableType"/> document, optionally specifying a CAS value and throwing an exception
        /// if there were any errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="persistable">The replacement document.</param>
        /// <param name="cas">The optional CAS value.</param>
        /// <param name="expiration">Optional expiration TTL.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task ReplaceSafeAsync<T>(this IBucket bucket, T persistable, ulong? cas = null, TimeSpan? expiration = null, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(persistable != null, nameof(persistable));

            IOperationResult<T> result;

            var replicateOrPersist = replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero;
            var key                = persistable.GetKey();

            if (cas.HasValue && expiration.HasValue)
            {
                result = await bucket.ReplaceAsync<T>(key, persistable, cas.Value, expiration.Value, replicateTo, persistTo);
            }
            else if (cas.HasValue)
            {
                result = await bucket.ReplaceAsync<T>(key, persistable, cas.Value, replicateTo, persistTo);
            }
            else if (expiration.HasValue)
            {
                // $todo(jefflill):
                //
                // There doesn't appear to be a way to do this in one API call because
                // there isn't an override that doesn't include a CAS parameter.  Research
                // whether it's possible to pass something like 0 or -1 as the CAS to
                // disable CAS behavior.

                var result1 = await bucket.ReplaceAsync<T>(key, persistable, replicateTo, persistTo);

                VerifySuccess<T>(result1, replicateOrPersist);

                var result2 = await bucket.TouchAsync(key, expiration.Value);

                VerifySuccess(result2, replicateOrPersist);
                return;
            }
            else
            {
                result = await bucket.ReplaceAsync<T>(key, persistable, replicateTo, persistTo);
            }

            VerifySuccess<T>(result, replicateOrPersist);
        }

        /// <summary>
        /// Touches a key and updates its expiry, throwing an exception if there were errors.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="expiration"></param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult> TouchSafeAsync(this IBucket bucket, string key, TimeSpan expiration)
        {
            var result = await bucket.TouchAsync(key, expiration);

            VerifySuccess(result, replicateOrPersist: false);
            return result;
        }

        /// <summary>
        /// Touches an <see cref="IPersistableType"/> document and updates its expiry, throwing an exception if there were errors.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="persistable">The document.</param>
        /// <param name="expiration"></param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult> TouchSafeAsync(this IBucket bucket, IPersistableType persistable, TimeSpan expiration)
        {
            Covenant.Requires<ArgumentNullException>(persistable != null, nameof(persistable));

            var result = await bucket.TouchAsync(persistable.GetKey(), expiration);

            VerifySuccess(result, replicateOrPersist: false);
            return result;
        }

        /// <summary>
        /// Unlocks a key, throwing an exception if there were errors.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="cas">The CAS value.</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult> UnlockSafeAsync(this IBucket bucket, string key, ulong cas)
        {
            var result = await bucket.UnlockAsync(key, cas);

            VerifySuccess(result, replicateOrPersist: false);
            return result;
        }

        /// <summary>
        /// Unlocks an <see cref="IPersistableType"/> document, throwing an exception if there were errors.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="persistable">The document.</param>
        /// <param name="cas">The CAS value.</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult> UnlockSafeAsync(this IBucket bucket, IPersistableType persistable, ulong cas)
        {
            Covenant.Requires<ArgumentNullException>(persistable != null, nameof(persistable));

            var result = await bucket.UnlockAsync(persistable.GetKey(), cas);

            VerifySuccess(result, replicateOrPersist: false);
            return result;
        }

        /// <summary>
        /// Inserts or updates a document, throwing an exception if there are errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="document">The document.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IDocumentResult<T>> UpsertSafeAsync<T>(this IBucket bucket, IDocument<T> document, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var result = await bucket.UpsertAsync<T>(document, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Inserts or updates a key, throwing an exception if there are errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<T>> UpsertSafeAsync<T>(this IBucket bucket, string key, T value, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var result = await bucket.UpsertAsync<T>(key, value, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Inserts or updates an <see cref="IPersistableType"/> document, throwing an exception if there are errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="persistable">The document.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<T>> UpsertSafeAsync<T>(this IBucket bucket, T persistable, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
            where T: class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(persistable != null, nameof(persistable));

            var result = await bucket.UpsertAsync<T>(persistable.GetKey(), persistable, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Inserts or updates a key using a CAS, throwing an exception if there are errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="cas">The CAS.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<T>> UpsertSafeAsync<T>(this IBucket bucket, string key, T value, ulong cas, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var result = await bucket.UpsertAsync<T>(key, value, cas, uint.MaxValue, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document, using a CAS, throwing an exception if there are errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="persistable">The document.</param>
        /// <param name="cas">The CAS.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<T>> UpsertSafeAsync<T>(this IBucket bucket, T persistable, ulong cas, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
            where T : class, IPersistableType
        {
            // $todo(jefflill):
            //
            // Not so sure about setting [uint.MaxValue] as the expiration here.

            var result = await bucket.UpsertAsync<T>(persistable.GetKey(), persistable, cas, uint.MaxValue, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Inserts or updates a key setting an expiration, throwing an exception if there are errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="expiration">The expiration.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<T>> UpsertSafeAsync<T>(this IBucket bucket, string key, T value, TimeSpan expiration, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            // $todo(jefflill):
            //
            // Not so sure about setting [uint.MaxValue] as the expiration here.

            var result = await bucket.UpsertAsync<T>(key, value, expiration, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Inserts or updates an <see cref="IPersistableType"/> document, setting an expiration and throwing an exception if there are errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="persistable">The document.</param>
        /// <param name="expiration">The expiration.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<T>> UpsertSafeAsync<T>(this IBucket bucket, T persistable, TimeSpan expiration, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(persistable != null, nameof(persistable));

            // $todo(jefflill):
            //
            // Not so sure about setting [uint.MaxValue] as the expiration here.

            var result = await bucket.UpsertAsync<T>(persistable.GetKey(), persistable, expiration, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Inserts or updates a key using a CAS and setting an expiration, throwing an exception if there are errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="cas">The CAS.</param>
        /// <param name="expiration">The expiration.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<T>> UpsertSafeAsync<T>(this IBucket bucket, string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
        {
            var result = await bucket.UpsertAsync<T>(key, value, cas, expiration, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Inserts or updates an <see cref="IPersistableType"/> document, using a CAS and setting an expiration, throwing an exception if there are errors.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="persistable">The document.</param>
        /// <param name="cas">The CAS.</param>
        /// <param name="expiration">The expiration.</param>
        /// <param name="replicateTo">Optional replication factor (defaults to <see cref="ReplicateTo.Zero"/>).</param>
        /// <param name="persistTo">Optional persistance factor (defaults to <see cref="PersistTo.Zero"/>).</param>
        /// <returns>The operation result.</returns>
        public static async Task<IOperationResult<T>> UpsertSafeAsync<T>(this IBucket bucket, T persistable, ulong cas, TimeSpan expiration, ReplicateTo replicateTo = ReplicateTo.Zero, PersistTo persistTo = PersistTo.Zero)
            where T : class, IPersistableType
        {
            var result = await bucket.UpsertAsync<T>(persistable.GetKey(), persistable, cas, expiration, replicateTo, persistTo);

            VerifySuccess(result, replicateOrPersist: replicateTo != ReplicateTo.Zero || persistTo != PersistTo.Zero);
            return result;
        }

        /// <summary>
        /// Lists the indexes for the test bucket.
        /// </summary>
        /// <param name="bucket">The Couchbase bucket.</param>
        /// <returns>The list of index information.</returns>
        public static async Task<List<CouchbaseIndex>> ListIndexesAsync(this IBucket bucket)
        {
            var list    = new List<CouchbaseIndex>();
            var indexes = await bucket.QuerySafeAsync<dynamic>(new QueryRequest($"select * from system:indexes where keyspace_id={CouchbaseHelper.Literal(bucket.Name)}"));

            foreach (var index in indexes)
            {
                list.Add(new CouchbaseIndex(index));
            }

            return list;
        }

        /// <summary>
        /// Returns information about a named Couchbase index for the test bucket.
        /// </summary>
        /// <param name="bucket">The Couchbase bucket.</param>
        /// <param name="name">The index name.</param>
        /// <returns>
        /// The index information as a <c>dynamic</c> or <c>null</c> 
        /// if the index doesn't exist.
        /// </returns>
        public static async Task<CouchbaseIndex> GetIndexAsync(this IBucket bucket, string name)
        {
            var indexes = await ListIndexesAsync(bucket);

            return indexes.SingleOrDefault(index => index.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Waits for a named index to enter a specific state (defaults to <b>online</b>).
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="name">The index name.</param>
        /// <param name="state">Optionally specifies the desire state (defaults to <b>online</b>).</param>
        public static async Task WaitForIndexAsync(this IBucket bucket, string name, string state = "online")
        {
            await NeonHelper.WaitForAsync(
                async () =>
                {
                    var index = await GetIndexAsync(bucket, name);

                    if (index == null)
                    {
                        return false;
                    }

                    return index.State == state;
                },
                timeout: TimeSpan.FromDays(365),
                pollTime: TimeSpan.FromMilliseconds(250));
        }
    }
}
