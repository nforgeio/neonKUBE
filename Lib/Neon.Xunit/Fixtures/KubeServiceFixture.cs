//-----------------------------------------------------------------------------
// FILE:	    KubeServiceFixture.cs
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Kube.Service;
using Neon.Net;
using Neon.Service;

namespace Neon.Xunit
{
    /// <summary>
    /// Fixture for testing a <see cref="KubeService"/>.
    /// </summary>
    public class KubeServiceFixture<TService> : TestFixture
        where TService : KubeService
    {
        private object                          syncLock = new object();
        private Task                            serviceTask;
        private Dictionary<string, HttpClient>  httpClientCache;
        private Dictionary<string, JsonClient>  jsonClientCache;

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public KubeServiceFixture()
        {
            httpClientCache = new Dictionary<string, HttpClient>();
            jsonClientCache = new Dictionary<string, JsonClient>();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~KubeServiceFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns the service instance.
        /// </summary>
        public TService Service { get; private set; }

        /// <summary>
        /// <b>DON'T USE THIS:</b> Use <see cref="Start(Func{TService})"/> instead for this fixture.
        /// </summary>
        /// <param name="action">The initialization action.</param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
        /// </returns>
        public override TestFixtureStatus Start(Action action = null)
        {
            throw new InvalidOperationException("Use the [Start(Func<TService> serviceCreator)] method instead of this.");
        }

        /// <summary>
        /// Starts the fixture including a <typeparamref name="TService"/> service instance if
        /// the fixture is not already running.
        /// </summary>
        /// <param name="serviceCreator">Callback that creates and returns the new service instance.</param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method first calls the <paramref name="serviceCreator"/> callback and expects it to
        /// return a new service instance that has been initialized by setting its environment variables
        /// and configuration files as required.  The callback should <b>not start</b> the service.
        /// </para>
        /// </remarks>
        public TestFixtureStatus Start(Func<TService> serviceCreator = null)
        {
            Covenant.Requires<ArgumentNullException>(serviceCreator != null);

            base.CheckDisposed();

            return base.Start(
                () =>
                {
                    StartAsComposed(serviceCreator);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="serviceCreator">Callback that creates and returns the new service instance.</param>
        public void StartAsComposed(Func<TService> serviceCreator = null)
        {
            Covenant.Requires<ArgumentNullException>(serviceCreator != null);

            base.CheckWithinAction();

            if (IsRunning)
            {
                return;
            }

            Service = serviceCreator();
            Covenant.Assert(Service != null);

            serviceTask = Service.RunAsync();

            IsRunning = true;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!base.IsDisposed)
                {
                    Reset();
                }

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Clears any instance caches.
        /// </summary>
        /// <param name="disposing">Optionally indicates that we're clearing because we're disposing the fixture.</param>
        private void ClearCaches(bool disposing = false)
        {
            if (httpClientCache != null)
            {
                lock (syncLock)
                {
                    foreach (var client in httpClientCache.Values)
                    {
                        client.Dispose();
                    }
                }

                if (disposing)
                {
                    httpClientCache = null;
                }
                else
                {
                    httpClientCache.Clear();
                }
            }

            if (jsonClientCache != null)
            {
                lock (syncLock)
                {
                    foreach (var client in jsonClientCache.Values)
                    {
                        client.Dispose();
                    }
                }

                if (disposing)
                {
                    jsonClientCache = null;
                }
                else
                {
                    jsonClientCache.Clear();
                }
            }
        }

        /// <summary>
        /// Restarts the service.
        /// </summary>
        /// <param name="serviceCreator">Callback that creates and returns the new service instance.</param>
        /// <remarks>
        /// <para>
        /// This method first calls the <paramref name="serviceCreator"/> callback and expects
        /// it to return a new service instance that has been initialized by setting its environment
        /// variables and configuration files as required.  The callback should not start thge service.
        /// </para>
        /// </remarks>
        public void Restart(Func<TService> serviceCreator = null)
        {
            Covenant.Requires<ArgumentNullException>(serviceCreator != null);
            Covenant.Requires<InvalidOperationException>(IsRunning);

            StopService();
            ClearCaches();

            Service = serviceCreator();
            Covenant.Assert(Service != null);

            serviceTask = Service.RunAsync();
        }

        /// <summary>
        /// Stops the service if it's running.
        /// </summary>
        private void StopService()
        {
            if (Service != null)
            {
                Service.Terminator.Signal();
                serviceTask.Wait();
                Service.Dispose();

                Service     = null;
                serviceTask = null;
            }
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            StopService();
            ClearCaches(disposing: true);
        }

        /// <summary>
        /// Returns a <see cref="HttpClient"/> instance configured to communicate with the
        /// service via the named HTTP/HTTPS endpoint.
        /// </summary>
        /// <param name="endpointName">Optionally specifies HTTP/HTTPS endpoint name as defined by the service description (defaults to <see cref="string.Empty"/>).</param>
        /// <param name="handler">Optionally specifies a custom HTTP handler.</param>
        /// <returns>The configured <see cref="HttpClient"/>.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the named endpoint doesn't exist.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the endpoint protocol is not <see cref="ServiceEndpointProtocol.Http"/>
        /// or <see cref="ServiceEndpointProtocol.Https"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The client returned will have it's <see cref="HttpClient.BaseAddress"/> initialized
        /// with the URL of the service including the path prefix defined by the endpoint.
        /// </para>
        /// <note>
        /// The client returned will be cached such that subsequent calls will return
        /// the same client instance for the endpoint.  This cache will be cleared if
        /// the service fixture is restarted.
        /// </note>
        /// <note>
        /// Do not dispose the client returned since it will be cached by the fixture and
        /// then be disposed when the fixture is restarted or disposed.  The optional 
        /// <paramref name="handler"/> passed will also be disposed when fixture will
        /// also be disposed automatically.
        /// </note>
        /// </remarks>
        public HttpClient GetHttpClient(string endpointName = "", HttpClientHandler handler = null)
        {
            Covenant.Requires<ArgumentNullException>(endpointName != null);

            if (!Service.Description.Endpoints.TryGetValue(endpointName, out var endpoint))
            {
                throw new KeyNotFoundException($"Endpoint [{endpointName}] not found.");
            }

            if (endpoint.Protocol != ServiceEndpointProtocol.Http && endpoint.Protocol != ServiceEndpointProtocol.Https)
            {
                throw new InvalidOperationException($"Cannot create an [{nameof(HttpClient)}] for an endpoint using the [{endpoint.Protocol}] protocol.");
            }

            lock (syncLock)
            {
                if (!httpClientCache.TryGetValue(endpointName, out var client))
                {
                    if (handler == null)
                    {
                        handler = new HttpClientHandler();
                    }

                    client = new HttpClient(handler, disposeHandler: true)
                    {
                        BaseAddress = endpoint.Uri
                    };

                    httpClientCache.Add(endpointName, client);
                }

                return client;
            }
        }

        /// <summary>
        /// Returns a <see cref="JsonClient"/> instance configured to communicate with the
        /// service via the named HTTP/HTTPS endpoint.
        /// </summary>
        /// <param name="endpointName">Optionally specifies HTTP/HTTPS endpoint name as defined by the service description (defaults to <see cref="string.Empty"/>).</param>
        /// <param name="handler">Optionally specifies a custom HTTP handler.</param>
        /// <returns>The configured <see cref="HttpClient"/>.</returns>
        /// <remarks>
        /// <para>
        /// The client returned will have it's <see cref="HttpClient.BaseAddress"/> initialized
        /// with the URL of the service including the path prefix defined by the endpoint.
        /// </para>
        /// <note>
        /// The client returned will be cached such that subsequent calls will return
        /// the same client instance for the endpoint.  This cache will be cleared if
        /// the service fixture is restarted.
        /// </note>
        /// <note>
        /// Do not dispose the client returned since it will be cached by the fixture and
        /// then be disposed when the fixture is restarted or disposed.  The optional 
        /// <paramref name="handler"/> passed will also be disposed when fixture will
        /// also be disposed automatically.
        /// </note>
        /// </remarks>
        public JsonClient GetJsonClient(string endpointName = "", HttpClientHandler handler = null)
        {
            Covenant.Requires<ArgumentNullException>(endpointName != null);

            if (!Service.Description.Endpoints.TryGetValue(endpointName, out var endpoint))
            {
                throw new KeyNotFoundException($"Endpoint [{endpointName}] not found.");
            }

            if (endpoint.Protocol != ServiceEndpointProtocol.Http && endpoint.Protocol != ServiceEndpointProtocol.Https)
            {
                throw new InvalidOperationException($"Cannot create a [{nameof(JsonClient)}] for an endpoint using the [{endpoint.Protocol}] protocol.");
            }

            lock (syncLock)
            {
                if (!jsonClientCache.TryGetValue(endpointName, out var client))
                {
                    if (handler == null)
                    {
                        handler = new HttpClientHandler();
                    }

                    client = new JsonClient(handler, disposeHandler: true)
                    {
                        BaseAddress = endpoint.Uri
                    };

                    jsonClientCache.Add(endpointName, client);
                }

                return client;
            }
        }
    }
}
