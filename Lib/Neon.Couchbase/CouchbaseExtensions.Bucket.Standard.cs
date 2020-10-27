//-----------------------------------------------------------------------------
// FILE:	    CouchbaseExtensions.Bucket.Standard.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using Couchbase.N1QL;
using Neon.Common;
using Neon.Data;
using Neon.Retry;
using Neon.Time;

namespace Couchbase
{
    public static partial class CouchbaseExtensions
    {
        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value)
           where T : class, IPersistableType
        {
            return bucket.InsertAsync<T>(value.GetKey(), value);
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document with an expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value, uint expiration)
           where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.InsertAsync<T>(value.GetKey(), value, expiration);
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document with an expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value, uint expiration, TimeSpan timeout)
           where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.InsertAsync<T>(value.GetKey(), value, expiration, timeout);
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document with an expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value, TimeSpan expiration)
           where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.InsertAsync<T>(value.GetKey(), value, expiration);
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document with an expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value, TimeSpan expiration, TimeSpan timeout)
           where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.InsertAsync<T>(value.GetKey(), value, expiration, timeout);
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document with a replication constraint.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value, ReplicateTo replicateTo)
           where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.InsertAsync<T>(value.GetKey(), value, replicateTo);
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document with a replication constraint and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value, ReplicateTo replicateTo, TimeSpan timeout)
           where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.InsertAsync<T>(value.GetKey(), value, replicateTo, timeout);
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document with replication and perstance constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value, ReplicateTo replicateTo, PersistTo persistTo)
           where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.InsertAsync<T>(value.GetKey(), value, replicateTo, persistTo);
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document with replication and persistence constraints with a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
           where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.InsertAsync<T>(value.GetKey(), value, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document with replication and persistence constraints and an expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
           where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.InsertAsync<T>(value.GetKey(), value, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document with replication and persistence constraints and an expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
           where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.InsertAsync<T>(value.GetKey(), value, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document with replication and persitsence constraints with a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
           where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.InsertAsync<T>(value.GetKey(), value, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Inserts an <see cref="IPersistableType"/> document with replicatioon and persistence constraints and and expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> InsertAsync<T>(this IBucket bucket, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
           where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.InsertAsync<T>(value.GetKey(), value, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, uint expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, expiration);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, uint expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, expiration, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, TimeSpan expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, expiration);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, TimeSpan expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, expiration, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS and expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas, uint expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas, expiration);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas, uint expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas, expiration, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS and expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas, expiration);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas, expiration, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a replication constraint.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ReplicateTo replicateTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, replicateTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a replication, constraint and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ReplicateTo replicateTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, replicateTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS and replication constraint.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas, ReplicateTo replicateTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas, replicateTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS replication, constraint and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas, replicateTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with replication and persistence constraints with a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS and replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, rep[lication and persistence constraints with a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration and replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration, replication and persistence constraints with a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration, replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration, replication and persistence constraints with a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Replace<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Replace<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, uint expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, expiration);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, uint expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, expiration, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, TimeSpan expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, expiration);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, TimeSpan expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, expiration, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS and expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas, uint expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas, expiration);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas, uint expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas, expiration, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS and expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas, expiration);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration, and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas, expiration, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a replication constraint.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ReplicateTo replicateTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, replicateTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a replication constraint and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ReplicateTo replicateTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, replicateTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS and replication constraint.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas, ReplicateTo replicateTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas, replicateTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, replication constraint and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas, replicateTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document wsith replication and persistence constraints with a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS replication and persistence constraints with a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration, persistence constraints with a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration, persistence constraints with a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration, and replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration, and replication and persistence constraints with a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> ReplaceAsync<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.ReplaceAsync<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, uint expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, expiration);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration and a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, uint expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, expiration, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, TimeSpan expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, expiration);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, TimeSpan expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, expiration, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ulong cas)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, cas);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS and expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ulong cas, uint expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, cas, expiration);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ulong cas, uint expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, expiration, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS and expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, cas, expiration);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration, and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value), nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, expiration, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a replication constraint.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ReplicateTo replicateTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, replicateTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a replication constraint and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ReplicateTo replicateTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, replicateTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with replication and persistence constraints and a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration, replication and persistence constraints and a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, replication and persistence constraints and a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration replication and persistence constraints and a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration, and replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with an expiration, and replication and persistence constraints, and a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration, and replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces an <see cref="IPersistableType"/> document with a CAS, expiration, and replication and persistence constraints, and a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static IOperationResult<T> Upsert<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.Upsert<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with an expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, uint expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, expiration);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with an expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, uint expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, expiration, timeout);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with an expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, TimeSpan expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, expiration);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with an expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, TimeSpan expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, expiration, timeout);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with a CAS.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ulong cas)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, cas);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with a CAS and expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ulong cas, uint expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, cas, expiration);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with a CAS, expiration and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ulong cas, uint expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, cas, expiration, timeout);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with a CAS and expiration.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, cas, expiration);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with a CAS, expiration, and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, cas, expiration, timeout);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with a replication constraint.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ReplicateTo replicateTo)
            where T : class, IPersistableType
        {
            return bucket.UpsertAsync<T>(value.GetKey(), value, replicateTo);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with a replicatioin constraint and timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ReplicateTo replicateTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, replicateTo, timeout);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, replicateTo, persistTo);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with replication and persistence constraints and a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with an expiration and replication and peristence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with an expiration, replication and persistence constraints and a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with a CAS, expiration and replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with a CAS, expiration, replication and peristence constraints, and a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with an expiration and replication and persistene constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with an expiration, replication and peristence constraints and a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with a CAS, expiration, and replication and persistence constraints.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo);
        }

        /// <summary>
        /// Upserts an <see cref="IPersistableType"/> document with a CAS, expiration, and replication and persistence constraints, and a timeout.
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="bucket">The bucket.</param>
        /// <param name="value">The document.</param>
        /// <param name="cas">The compare and swap value.</param>
        /// <param name="expiration">Specifies the document lifetime.</param>
        /// <param name="replicateTo">Specifies the replication constraint.</param>
        /// <param name="persistTo">Specifies the persistence constraint.</param>
        /// <param name="timeout">Specifies the operation timeout.</param>
        /// <returns>The operation result.</returns>
        public static Task<IOperationResult<T>> UpsertAsync<T>(this IBucket bucket, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
            where T : class, IPersistableType
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            return bucket.UpsertAsync<T>(value.GetKey(), value, cas, expiration, replicateTo, persistTo, timeout);
        }
    }
}
