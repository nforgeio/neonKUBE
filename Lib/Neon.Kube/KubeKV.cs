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
    /// This class is essentially a client for the <b>neon-cluster-api</b> which exposes
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

        private JsonClient          jsonClient = null;
        private string              dbConnectionString = null;
        private string              stateTable = null;
        /// <summary>
        /// Default constructor.  Use this to access the KV store via a REST API.
        /// </summary>
        public KubeKV()
        {
            jsonClient = new JsonClient();
            jsonClient.HttpClient.BaseAddress = new Uri("http://127.0.0.10:1234");
        }

        /// <summary>
        /// Constructs a client that will operate directly on the KV store within the system database.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="stateTable"></param>
        public KubeKV(string connectionString, string stateTable)
        {
            Covenant.Requires<ArgumentException>(!string.IsNullOrEmpty(connectionString), nameof(connectionString));

            this.dbConnectionString = connectionString;
            this.stateTable         = stateTable;
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
            jsonClient?.Dispose();
            jsonClient = null;

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
            Covenant.Requires<ArgumentException>(key.Length < maxKeyLength, nameof(key), $"Key is exceeds [{maxKeyLength}] characters.");

            if (!allowPattern)
            {
                Covenant.Requires<ArgumentException>(key.IndexOfAny(wildcards) == -1, nameof(key), "Key may not include wildcards: [*] or [?].");
            }
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

            if (string.IsNullOrEmpty(dbConnectionString))
            {
                if (value.GetType() == typeof(string))
                {
                    value = NeonHelper.JsonSerialize(value);
                }

                await jsonClient.PutAsync($"v1/kv/{key}", value);
            }
            else
            {
                var serializedValue = NeonHelper.JsonSerialize(value);
                await using var conn = new NpgsqlConnection(dbConnectionString);
                {
                    await conn.OpenAsync();
                    await using (var cmd = new NpgsqlCommand($@"
    INSERT
        INTO
        {stateTable} (KEY, value)
    VALUES (@k, @v) ON
    CONFLICT (KEY) DO
    UPDATE
    SET
        value = @v", conn))
                    {
                        cmd.Parameters.AddWithValue("k", key);
                        cmd.Parameters.AddWithValue("v", parameterType: NpgsqlTypes.NpgsqlDbType.Text, serializedValue);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
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

            if (string.IsNullOrEmpty(dbConnectionString))
            {
                var result = await jsonClient.GetUnsafeAsync($"v1/kv/{key}");

                if (result.IsSuccess)
                {
                    return result.As<TValue>();
                }
                else if (result.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new KubeKVException("Key/value doesn't exist.");
                }
                else
                {
                    throw new KubeKVException("Unknown error.");
                }
            }
            else
            {
                await using var conn = new NpgsqlConnection(dbConnectionString);
                {
                    await conn.OpenAsync();
                    await using (NpgsqlCommand cmd = new NpgsqlCommand($"SELECT value FROM {stateTable} WHERE key='{key}'", conn))
                    {
                        var result = await cmd.ExecuteScalarAsync();

                        if (result == null)
                        {
                            throw new KubeKVException("Key/value doesn't exist.");
                        }

                        return NeonHelper.JsonDeserialize<dynamic>(result.ToString());
                    }
                }
            }
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

            var result = await jsonClient.GetUnsafeAsync($"v1/kv/{key}");

            if (result.IsSuccess)
            {
                return result.As<TValue>();
            }
            else if (result.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return defaultValue;
            }
            else
            {
                throw new KubeKVException("Unknown error.");
            }
        }

        /// <summary>
        /// Removes matching keys if they exist.
        /// </summary>
        /// <param name="keyPattern">The key pattern.</param>
        /// <param name="regex">Whether regex matches will be applied.</param>
        /// <exception cref="ArgumentException">Thrown if the key pattern is not valid.</exception>
        /// <exception cref="ArgumentNullException">Thrown for <c>null</c> or empty key patterns.</exception>
        /// <exception cref="KubeKVException">Thrown for KV related errors.</exception>
        /// <remarks>
        /// <note>
        /// No exception is thrown when no keys were removed.
        /// </note>
        /// </remarks>
        public async Task RemoveAsync(string keyPattern, bool regex = false)
        {
            CheckKey(keyPattern, allowPattern: regex);

            if (string.IsNullOrEmpty(dbConnectionString))
            {
                var args = new ArgDictionary();
                args.Add("regex", regex);

                await jsonClient.DeleteAsync($"v1/kv/{keyPattern}", args: args);
            }
            else
            {
                var command = new StringBuilder();

                command.AppendLine($"DELETE FROM {stateTable}");

                if (keyPattern != "*")
                {
                    if (regex)
                    {
                        command.AppendLine($"WHERE KEY ~ '{keyPattern}'");
                    }
                    else
                    {
                        command.AppendLine($"WHERE KEY = '{keyPattern}'");
                    }
                }

                await using var conn = new NpgsqlConnection(dbConnectionString);
                {
                    await conn.OpenAsync();
                    await using (NpgsqlCommand cmd = new NpgsqlCommand(command.ToString(), conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Returns the of key/value pairs that match the key pattern.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="keyPattern">The key pattern.</param>
        /// <exception cref="ArgumentException">Thrown if the key pattern is not valid.</exception>
        /// <exception cref="ArgumentNullException">Thrown for <c>null</c> or empty key patterns.</exception>
        /// <exception cref="KubeKVException">Thrown for KV related errors.</exception>
        public async Task<Dictionary<string, TValue>> ListAsync<TValue>(string keyPattern)
        {
            CheckKey(keyPattern, allowPattern: true);

            if (string.IsNullOrEmpty(dbConnectionString))
            {
                var args = new ArgDictionary();
                args.Add("keyPattern", keyPattern);

                return await jsonClient.GetAsync<Dictionary<string, TValue>>($"v1/kv/", args: args);
            }
            else
            {
                var command = new StringBuilder();

                command.AppendLine($"SELECT KEY, value FROM {stateTable}");

                if (keyPattern != "*")
                {
                    command.AppendLine($"WHERE KEY ~ '{keyPattern}'");
                }

                var results = new Dictionary<string, TValue>();
                await using var conn = new NpgsqlConnection(dbConnectionString);
                {
                    await conn.OpenAsync();
                    await using (NpgsqlCommand cmd = new NpgsqlCommand(command.ToString(), conn))
                    {
                        await using (var reader = await cmd.ExecuteReaderAsync())
                            while (await reader.ReadAsync())
                                results.Add(reader.GetString(0), (TValue)reader.GetValue(1));
                    }
                }
                return results;
            }
        }
    }
}
