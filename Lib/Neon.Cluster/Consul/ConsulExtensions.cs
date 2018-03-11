//-----------------------------------------------------------------------------
// FILE:	    ConsulExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Net;

// $todo(jeff.lill): Should I be verifying that all KV responses have no errors?

namespace Consul
{
    /// <summary>
    /// HashiCorp Consul extensions.
    /// </summary>
    public static class ConsulExtensions
    {
        //---------------------------------------------------------------------
        // IKVEndpoint extensions

        /// <summary>
        /// Determines whether a Consul key exists.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns><c>true</c> if the key exists.</returns>
        public static async Task<bool> Exists(this IKVEndpoint kv, string key, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key));

            return (await kv.Get(key, cancellationToken)).Response != null;
        }

        /// <summary>
        /// Writes a byte array value to a Consul key. 
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns><c>true</c> on success.</returns>
        public static async Task<bool> PutBytes(this IKVEndpoint kv, string key, byte[] value, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key));

            var p = new KVPair(key);

            p.Value = value ?? new byte[0];

            return (await kv.Put(p, cancellationToken)).Response;
        }

        /// <summary>
        /// Writes a string value to a Consul key.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns><c>true</c> on success.</returns>
        /// <remarks>
        /// This method writes an empty string for <c>null</c> values and writes
        /// the <see cref="object.ToString()"/> results otherwise.
        /// </remarks>
        public static async Task<bool> PutString(this IKVEndpoint kv, string key, object value, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key));

            var p = new KVPair(key);

            if (value == null)
            {
                value = string.Empty;
            }

            p.Value = Encoding.UTF8.GetBytes(value.ToString());

            return (await kv.Put(p, cancellationToken)).Response;
        }

        /// <summary>
        /// Writes a boolean value to a Consul key.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns><c>true</c> on success.</returns>
        public static async Task<bool> PutBool(this IKVEndpoint kv, string key, bool value, CancellationToken cancellationToken = default)
        {
            return await PutString(kv, key, value ? "true" : "false", cancellationToken);
        }

        /// <summary>
        /// Writes an integer value to a Consul key.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns><c>true</c> on success.</returns>
        public static async Task<bool> PutInt(this IKVEndpoint kv, string key, int value, CancellationToken cancellationToken = default)
        {
            return await PutString(kv, key, value.ToString(), cancellationToken);
        }

        /// <summary>
        /// Writes a long value to a Consul key.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns><c>true</c> on success.</returns>
        public static async Task<bool> PutLong(this IKVEndpoint kv, string key, long value, CancellationToken cancellationToken = default)
        {
            return await PutString(kv, key, value.ToString(), cancellationToken);
        }

        /// <summary>
        /// Writes a double value to a Consul key.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns><c>true</c> on success.</returns>
        public static async Task<bool> PutDouble(this IKVEndpoint kv, string key, double value, CancellationToken cancellationToken = default)
        {
            return await PutString(kv, key, value.ToString("R", NumberFormatInfo.InvariantInfo), cancellationToken);
        }

        /// <summary>
        /// Writes an object value as JSON to a Consul key.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="formatting">Optional JSON formatting (defaults to <b>None</b>).</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns><c>true</c> on success.</returns>
        public static async Task<bool> PutObject(this IKVEndpoint kv, string key, object value, Formatting formatting = Formatting.None, CancellationToken cancellationToken = default)
        {
            return await PutString(kv, key, NeonHelper.JsonSerialize(value, formatting), cancellationToken);
        }

        /// <summary>
        /// Reads a key as a byte array.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The byte array value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if <paramref name="key"/> could not be found.</exception>
        public static async Task<byte[]> GetBytes(this IKVEndpoint kv, string key, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key));

            var response = (await kv.Get(key, cancellationToken)).Response;

            if (response == null)
            {
                throw new KeyNotFoundException(key);
            }

            return response.Value;
        }

        /// <summary>
        /// Reads a key as a string.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The string value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if <paramref name="key"/> could not be found.</exception>
        public static async Task<string> GetString(this IKVEndpoint kv, string key, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key));

            var response = (await kv.Get(key, cancellationToken)).Response;

            if (response == null)
            {
                throw new KeyNotFoundException(key);
            }

            return Encoding.UTF8.GetString(response.Value);
        }

        /// <summary>
        /// Reads and parses a key as a <c>bool</c>.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if <paramref name="key"/> could not be found.</exception>
        /// <exception cref="FormatException">Thrown if the value is not valid.</exception>
        public static async Task<bool> GetBool(this IKVEndpoint kv, string key, CancellationToken cancellationToken = default)
        {
            var input = await GetString(kv, key, cancellationToken);

            switch (input.ToLowerInvariant())
            {
                case "0":
                case "no":
                case "false":

                    return false;

                case "1":
                case "yes":
                case "true":

                    return true;

                default:

                    throw new FormatException($"[{input}] is not a valid boolean.");
            }
        }

        /// <summary>
        /// Reads and parses a key as an <c>int</c>.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if <paramref name="key"/> could not be found.</exception>
        /// <exception cref="FormatException">Thrown if the value is not valid.</exception>
        public static async Task<int> GetInt(this IKVEndpoint kv, string key, CancellationToken cancellationToken = default)
        {
            var input = await GetString(kv, key, cancellationToken);

            if (int.TryParse(input, out var value))
            {
                return value;
            }
            else
            {
                throw new FormatException($"[{input}] is not a valid integer.");
            }
        }

        /// <summary>
        /// Reads and parses a key as a <c>long</c>.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if <paramref name="key"/> could not be found.</exception>
        /// <exception cref="FormatException">Thrown if the value is not valid.</exception>
        public static async Task<long> GetLong(this IKVEndpoint kv, string key, CancellationToken cancellationToken = default)
        {
            var input = await GetString(kv, key, cancellationToken);

            if (long.TryParse(input, out var value))
            {
                return value;
            }
            else
            {
                throw new FormatException($"[{input}] is not a valid long.");
            }
        }

        /// <summary>
        /// Reads and parses a key as a <c>double</c>.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if <paramref name="key"/> could not be found.</exception>
        /// <exception cref="FormatException">Thrown if the value is not valid.</exception>
        public static async Task<double> GetDouble(this IKVEndpoint kv, string key, CancellationToken cancellationToken = default)
        {
            var input = await GetString(kv, key, cancellationToken);

            if (double.TryParse(input, NumberStyles.AllowDecimalPoint, NumberFormatInfo.InvariantInfo, out var value))
            {
                return value;
            }
            else
            {
                throw new FormatException($"[{input}] is not a valid double.");
            }
        }

        /// <summary>
        /// Reads and deserializes a key with a JSON value as a specified type.
        /// </summary>
        /// <typeparam name="T">The type to be desearialized.</typeparam>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The parsed <typeparamref name="T"/>.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if <paramref name="key"/> could not be found.</exception>
        /// <exception cref="FormatException">Thrown if the value is not valid.</exception>
        public static async Task<T> GetObject<T>(this IKVEndpoint kv, string key, CancellationToken cancellationToken = default)
            where T : new()
        {
            var input = await GetString(kv, key, cancellationToken);

            try
            {
                return NeonHelper.JsonDeserialize<T>(input);
            }
            catch (Exception e)
            {
                throw new FormatException(e.Message, e);
            }
        }

        /// <summary>
        /// Watches a key for changes, invoking an asynchronous callback whenever
        /// a change is detected or a timeout has been exceeded.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="key">The key being watched.</param>
        /// <param name="action">The asynchronous action with a boolean parameter that will be passed as <c>true</c> if a change was detected.</param>
        /// <param name="throwOnError">
        /// Optionally specifies that an <see cref="HttpException"/> be thrown if the Consul
        /// request fails and that the <paramref name="action"/> not be called.  This defaults
        /// to <c>false</c>.
        /// </param>
        /// <param name="timeout">The optional timeout (defaults to <see cref="Timeout.InfiniteTimeSpan"/>).</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The <see cref="Task"/>.</returns>
        /// <exception cref="HttpException">
        /// Thrown if <paramref name="throwOnError"/> is set to <c>true</c> and the
        /// Consul request fails.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method provides an easy way to monitor a Consul key or key prefix for changes.
        /// <paramref name="key"/> specifies the key or prefix.  Prefixes are distinguished by
        /// a terminating forward slash (<b>/</b>).
        /// </para>
        /// <para>
        /// <paramref name="action"/> must be passed as an async handler with a single <c>byte[]</c> 
        /// parameter that will be called when a potential change is detected.  The current value of the
        /// key encoded as bytes will be passed as the parameter.
        /// </para>
        /// <note>
        /// Consul may invoke the action even though nothing has changed.  This occurs when the
        /// request times out (a maximum of 10 minutes) or when an idempotent operation has been
        /// performed (e.g. a transaction?).  Applications will need to take any necessary care 
        /// to verify that that the notification should actually trigger an action.
        /// </note>
        /// <para>
        /// <paramref name="timeout"/> specifies the maximum time to wait for Consul to respond.
        /// This defaults to <see cref="Timeout.InfiniteTimeSpan"/> which means the method will
        /// wait forever.  It can be useful to specify a different <paramref name="timeout"/>.
        /// With this, the method will call the <paramref name="action"/> whenever a change
        /// is detected or when the timeout has been exceeded.  The action parameter will be
        /// <c>false</c> for the latter case.
        /// </para>
        /// <para>
        /// Here's an example:
        /// </para>
        /// <code lang="c#">
        /// ConsulClient    consul;
        /// 
        /// await consul.KV.WatchKey("foo", 
        ///     async changed =>
        ///     {
        ///         if (changed)
        ///         {
        ///             // Do something when the key changed.
        ///         }
        ///         else
        ///         {
        ///             // Do something for timeouts.
        ///         }
        ///     },
        ///     TimeSpan.FromSeconds(30));
        /// </code> 
        /// </remarks>
        public static async Task WatchKey(this IKVEndpoint kv, string key, Func<byte[], Task> action, bool throwOnError = false, TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key));
            Covenant.Requires<ArgumentException>(!key.EndsWith("/"));
            Covenant.Requires<ArgumentNullException>(action != null);

            if (timeout <= TimeSpan.Zero)
            {
                timeout = Timeout.InfiniteTimeSpan;
            }

            await Task.Run(
                async () =>
                {
                    var response  = await kv.Get(key, cancellationToken);
                    var lastIndex = response.LastIndex;
                    var options   = new QueryOptions() { WaitTime = timeout };

                    if (response.StatusCode >= (HttpStatusCode)400 && throwOnError)
                    {
                        throw new HttpException(response.StatusCode);
                    }

                    await action(response.Response.Value);

                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        options.WaitIndex = lastIndex;
                        response          = await kv.Get(key, options, cancellationToken);

                        if (response.StatusCode >= (HttpStatusCode)400 && throwOnError)
                        {
                            throw new HttpException(response.StatusCode);
                        }

                        await action(response.Response.Value);

                        lastIndex = response.LastIndex;
                    }
                });
        }

        /// <summary>
        /// Watches a key prefix for changes, invoking an asynchronous callback whenever
        /// a change is detected or a timeout has been exceeded.
        /// </summary>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="keyPrefix">The key prefix being watched (ending with a forward slash (<b>/</b>).</param>
        /// <param name="action">The asynchronous action with a boolean parameter that will be passed as <c>true</c> if a change was detected.</param>
        /// <param name="throwOnError">
        /// Optionally specifies that an <see cref="HttpException"/> be thrown if the Consul
        /// request fails and that the <paramref name="action"/> not be called.  This defaults
        /// to <c>false</c>.
        /// </param>
        /// <param name="timeout">The optional timeout (defaults to <see cref="Timeout.InfiniteTimeSpan"/>).</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The <see cref="Task"/>.</returns>
        /// <exception cref="HttpException">
        /// Thrown if <paramref name="throwOnError"/> is set to <c>true</c> and the
        /// Consul request fails.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method provides an easy way to monitor a Consul key or key prefix for changes.
        /// <paramref name="keyPrefix"/> specifies the key or prefix.  Prefixes are distinguished by
        /// a terminating forward slash (<b>/</b>).
        /// </para>
        /// <para>
        /// <paramref name="action"/> must be passed as an async handler that will be called when
        /// a potential change is detected.
        /// </para>
        /// <note>
        /// Consul may invoke the action even though nothing has changed.  This occurs when the
        /// request times out (a maximum of 10 minutes) or when an idempotent operation has been
        /// performed (e.g. a transaction?).  Applications will need to take any necessary care 
        /// to verify that that the notification should actually trigger an action.
        /// </note>
        /// <para>
        /// <paramref name="timeout"/> specifies the maximum time to wait for Consul to respond.
        /// This defaults to <see cref="Timeout.InfiniteTimeSpan"/> which means the method will
        /// wait forever.  It can be useful to specify a different <paramref name="timeout"/>.
        /// With this, the method will call the <paramref name="action"/> whenever a change
        /// is detected or when the timeout has been exceeded.  The action parameter will be
        /// <c>false</c> for the latter case.
        /// </para>
        /// <para>
        /// Here's an example:
        /// </para>
        /// <code lang="c#">
        /// ConsulClient    consul;
        /// 
        /// await consul.KV.WatchPrefix("foo.", 
        ///     async changed =>
        ///     {
        ///         if (changed)
        ///         {
        ///             // Do something when the key changed.
        ///         }
        ///         else
        ///         {
        ///             // Do something for timeouts.
        ///         }
        ///     },
        ///     TimeSpan.FromSeconds(30));
        /// </code> 
        /// </remarks>
        public static async Task WatchPrefix(this IKVEndpoint kv, string keyPrefix, Func<Task> action, bool throwOnError = false, TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(keyPrefix));
            Covenant.Requires<ArgumentException>(keyPrefix.EndsWith("/"));
            Covenant.Requires<ArgumentNullException>(action != null);

            if (timeout <= TimeSpan.Zero)
            {
                timeout = Timeout.InfiniteTimeSpan;
            }

            await Task.Run(
                async () =>
                {
                    var response  = await kv.Keys(keyPrefix, cancellationToken);
                    var lastIndex = response.LastIndex;
                    var options   = new QueryOptions() { WaitTime = timeout };

                    if (response.StatusCode >= (HttpStatusCode)400 && throwOnError)
                    {
                        throw new HttpException(response.StatusCode);
                    }

                    await action();

                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        options.WaitIndex = lastIndex;
                        response          = await kv.Keys(keyPrefix, null, options, cancellationToken);

                        if (response.StatusCode >= (HttpStatusCode)400 && throwOnError)
                        {
                            throw new HttpException(response.StatusCode);
                        }

                        await action();

                        lastIndex = response.LastIndex;
                    }
                });
        }

        /// <summary>
        /// Lists the items beneath a path prefix and deserializes them as JSON.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="kv">The key/value endpoint.</param>
        /// <param name="keyPrefix">The path prefix.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The items.</returns>
        public static async Task<IEnumerable<T>> List<T>(this IKVEndpoint kv, string keyPrefix, CancellationToken cancellationToken = default)
            where T : new()
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(keyPrefix));

            var response = await kv.List(keyPrefix, cancellationToken);
            var rawItems = response.Response;
            var items    = new List<T>(rawItems.Length);

            foreach (var rawItem in rawItems)
            {
                items.Add(NeonHelper.JsonDeserialize<T>(Encoding.UTF8.GetString(rawItem.Value)));
            }

            return items;
        }
    }
}
