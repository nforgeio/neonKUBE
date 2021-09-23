//-----------------------------------------------------------------------------
// FILE:	    KubeKV.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using k8s.Models;

using Neon.Collections;
using Neon.Common;
using Neon.Net;

using Npgsql;

namespace Neon.Kube
{
    /// <summary>
    /// Used to persist global cluster state as key/value pairs.  This is intended
    /// for use only by neonKUBE system components and services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sometimes it's necessary to be able to share a bit of state across system services.
    /// This class provides methods to persist, retrieve, list, and delete key/value pairs
    /// where the keys are strings and values are JSON text.
    /// </para>
    /// <para>
    /// This class is essentially a client for the <b>neon-kubekv-service</b> which exposes
    /// a REST API that actually manages access to the underlying Citus system database.
    /// </para>
    /// <para>
    /// Keys are strings consisting of one or more characters.  The <b>"*"</b> and <b>"?"</b>
    /// are reserved for use as filesystem-style wildcards and the period <b>"."</b> character
    /// it used by convention as a namespace separator.  Keys may have a maximum length of
    /// <b>256</b> characters.
    /// </para>
    /// </remarks>
    public class KubeKV : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        private const int maxKeyLength = 256;

        private static readonly char[] wildcards = new char[] { '*', '?' };

        //---------------------------------------------------------------------
        // Instance members

        private HttpClient          httpClient = null;
        private NpgsqlConnection    database   = null;

        /// <summary>
        /// Default constructor.  Use this to access the KV store via a REST API.
        /// </summary>
        public KubeKV()
        {
            httpClient = new HttpClient();
        }

        /// <summary>
        /// Constructs a client that will operate directly on the KV store within the system database.
        /// </summary>
        /// <param name="connectionString"></param>
        public KubeKV(string connectionString)
        {
            Covenant.Requires<ArgumentException>(!string.IsNullOrEmpty(connectionString), nameof(connectionString));

            // $todo(marcusbooyah): https://github.com/nforgeio/neonKUBE/issues/1263
            //
            // You'll need to connect the [database] field to the KV database here
            // and then modify the methods below to access the database directly when
            // this isn't NULL rather than going through the KV service.
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~KubeKV()
        {
            Dispose(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Handles the actually disposal.
        /// </summary>
        /// <param name="disposing">Passed as <c>true</c> if we're disposing, <c>false</c> when finalizing.</param>
        protected void Dispose(bool disposing)
        {
            httpClient?.Dispose();
            httpClient = null;

            database?.Dispose();
            database = null;

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Verifies that a key is valid. 
        /// </summary>
        /// <param name="key">The key being checked.</param>
        /// <param name="allowPattern">Optionally indicates that key may include pattern wildcards.</param>
        /// <exception cref="ArgumentException">Thrown if the key is not valid.</exception>
        /// <exception cref="ArgumentNullException">Thrown for <c>null</c> or empty keys.</exception>
        private void CheckKey(string key, bool allowPattern = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(key), nameof(key));
            Covenant.Requires<ArgumentException>(key.Length > maxKeyLength, nameof(key), $"Key is exceeds [{maxKeyLength}] characters.");
            Covenant.Requires<ArgumentException>(!allowPattern && key.IndexOfAny(wildcards) == -1, nameof(key), "Key may not include wildcards: [*] or [?].");
        }

        /// <summary>
        /// Adds or updates the named value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="ArgumentException">Thrown if the key is not valid.</exception>
        /// <exception cref="ArgumentNullException">Thrown for <c>null</c> or empty keys.</exception>
        public async Task SetAsync(string key, object value)
        {
            CheckKey(key);

            await Task.CompletedTask;
            throw new NotImplementedException("$todo(marcusbooyah)");
        }

        /// <summary>
        /// Retrieves the value foe a key.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="key">The key.</param>
        /// <returns>The value if the key exists.</returns>
        /// <exception cref="ArgumentException">Thrown if the key is not valid.</exception>
        /// <exception cref="ArgumentNullException">Thrown for <c>null</c> or empty keys.</exception>
        /// <exception cref="KubeKVException">Thrown if the named value doesn't exist or for other KV related errors.</exception>
        public async Task<TValue> GetAsync<TValue>(string key)
        {
            CheckKey(key);

            await Task.CompletedTask;
            throw new NotImplementedException("$todo(marcusbooyah)");
        }

        /// <summary>
        /// Retrieves a named value, returning a default value when the
        /// key doesn't exist.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="defaultValue">The value to be returned when the key doesn't exist.</param>
        /// <returns>The retrieved value or <paramref name="defaultValue"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if the key is not valid.</exception>
        /// <exception cref="ArgumentNullException">Thrown for <c>null</c> or empty keys.</exception>
        /// <exception cref="KubeKVException">Thrown for KV related errors.</exception>
        public async Task<TValue> GetAsync<TValue>(string key, TValue defaultValue)
        {
            CheckKey(key);

            await Task.CompletedTask;
            throw new NotImplementedException("$todo(marcusbooyah)");
        }

        /// <summary>
        /// Removes matching keys if they exist.
        /// </summary>
        /// <param name="keyPattern">The key pattern.</param>
        /// <exception cref="ArgumentException">Thrown if the key pattern is not valid.</exception>
        /// <exception cref="ArgumentNullException">Thrown for <c>null</c> or empty key patterns.</exception>
        /// <exception cref="KubeKVException">Thrown for KV related errors.</exception>
        /// <remarks>
        /// <note>
        /// No exception is thrown when no keys were removed.
        /// </note>
        /// </remarks>
        public async Task RemoveAsync(string keyPattern)
        {
            CheckKey(keyPattern, allowPattern: true);

            await Task.CompletedTask;
            throw new NotImplementedException("$todo(marcusbooyah)");
        }

        /// <summary>
        /// Returns the of key/value pairs that match the key pattern.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="keyPattern">The key pattern.</param>
        /// <exception cref="ArgumentException">Thrown if the key pattern is not valid.</exception>
        /// <exception cref="ArgumentNullException">Thrown for <c>null</c> or empty key patterns.</exception>
        /// <exception cref="KubeKVException">Thrown for KV related errors.</exception>
        public async Task<IEnumerable<KeyValuePair<string, TValue>>> ListAsync<TValue>(string keyPattern)
        {
            CheckKey(keyPattern, allowPattern: true);

            await Task.CompletedTask;
            throw new NotImplementedException("$todo(marcusbooyah)");
        }
    }
}
