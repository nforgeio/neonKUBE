//-----------------------------------------------------------------------------
// FILE:	    ReverseProxy.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Net.Http.Server;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
	/// <summary>
	/// <para>
	/// Implements a reverse HTTP or proxy between an endpoint on the local machine
	/// and an endpoint on a remote machine.
	/// </para>
	/// <note>
	/// This is supported <b>only on Windows</b>.
	/// </note>
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use the <see cref="ReverseProxy"/> constructor to create a proxy.  You'll
	/// pass the local and remote endpoints and optional request and response 
	/// handlers.
	/// </para>
	/// <para>
	/// The request handler will be called when a request is received on the local
	/// endpoint give the handler a chance to modify the request before it is
	/// forwarded on to the remote endpoint.  The response handler is called when
	/// a response is received from the remote endpoint, giving the handler a
	/// chance to examine and possibly modify the response before it is returned
	/// to the caller.
	/// </para>
	/// </remarks>
	public sealed class ReverseProxy : IDisposable
	{
		//---------------------------------------------------------------------
		// Static members

		private const int BufferSize = 16 * 1024;

		private static HashSet<string>  ignoredRequestHeaders;
		private static HashSet<string>  ignoredResponseHeaders;

		/// <summary>
		/// Static constructor.
		/// </summary>
		static ReverseProxy()
		{
			// These headers will not be included from the client request
			// when we forward them to the remote endpoint.  We're letting
			// the HTTPClient manage these as required when transmitting
			// requests.

			ignoredRequestHeaders = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
			{
				"Connection",
				"Content-Length",
				"Content-Type",
				"Host",
				"Transfer-Encoding"
			};

			// These headers will not be included in the from the remote
			// response when we forward them back to the client.  We're
			// letting the WebListener manage these as required when 
			// transmitting responses.

			ignoredResponseHeaders = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
			{
				"Content-Encoding",
				"Transfer-Encoding"
			};
		}

		//---------------------------------------------------------------------
		// Instance members

		private readonly object			syncLock = new object();
		private int                     localPort;
		private int                     remotePort;
		private Action<RequestContext>  requestHandler;
		private Action<RequestContext>  responseHandler;
		private WebListener             listener;
		private HttpClient              client;
		private Queue<byte[]>           bufferPool;

		// I was unable to get [ReverseProxy] request forwarding to the
		// Kubernetes dashboard to work.  I believe it's a problem with
		// the self-signed certificate generated for the dashboard.  The
		// HTTP handler's certificate validation function is never called
		// either.
		//
		// I'm going to work around this by launching [kubectl proxy]
		// to manage the dashboard proxy, setting this to the process,
		// and then configuring the reverse proxy so that it forwards
		// requests to [kubectl proxy] which then forwards them on to
		// the dashboard service in the cluster.
		//
		// We still need the reverse proxy so we can inject a token
		// via an authentication header.

		private Process kubectlProxyProcess;

		/// <summary>
		/// Constructs a reverse proxy.
		/// </summary>
		/// <param name="localPort">The local port.</param>
		/// <param name="remotePort">The remote port.</param>
		/// <param name="remoteHost">Optionally specifies the remote hostname or IP address.</param>
		/// <param name="remoteTls">Optionally indicates that the remote endpoint required TLS.</param>
		/// <param name="validCertificate">
		/// Optionally specifies an acceptable server certificate.  This can be used 
		/// as a way to allow access for a specific self-signed certificate.  Passing 
		/// a certificate implies <paramref name="remoteTls"/><c>=true</c>.
		/// </param>
		/// <param name="clientCertificate">
		/// Optionally specifies a client certificate.  Passing a certificate implies
		/// <paramref name="remoteTls"/><c>=true</c>.
		/// </param>
		/// <param name="requestHandler">Optional request hook.</param>
		/// <param name="responseHandler">Optional response hook.</param>
		public ReverseProxy(
			int                     localPort,
			int                     remotePort,
			string                  remoteHost        = "localhost",
			bool                    remoteTls         = false,
			X509Certificate2        validCertificate  = null,
			X509Certificate2        clientCertificate = null,
			Action<RequestContext>  requestHandler    = null, 
			Action<RequestContext>  responseHandler   = null)
		{
			Covenant.Requires<ArgumentException>(NetHelper.IsValidPort(localPort), nameof(localPort));
			Covenant.Requires<ArgumentException>(NetHelper.IsValidPort(remotePort), nameof(remotePort));

			if (validCertificate != null || clientCertificate != null)
			{
				remoteTls = true;
			}

			if (!NeonHelper.IsWindows)
			{
				throw new NotSupportedException($"[{nameof(ReverseProxy)}] is supported only on Windows.");
			}

			this.localPort       = localPort;
			this.remotePort      = remotePort;
			this.requestHandler  = requestHandler;
			this.responseHandler = responseHandler;

			// Create the client.

			var remoteScheme = remoteTls ? "https" : "http";

			// $todo(jefflill):
			//
			// Enable this when we upgrade to .NET Standard 2.1
			//
			//      https://github.com/nforgeio/neonKUBE/issues/new

#if NETSTANDARD_21
			var httpHandler  =
				new SocketsHttpHandler()
				{
					AllowAutoRedirect           = false,
					AutomaticDecompression      = DecompressionMethods.All,
					ConnectTimeout              = TimeSpan.FromSeconds(5),
					MaxConnectionsPerServer     = 100,
					PooledConnectionIdleTimeout = TimeSpan.FromSeconds(10),
					PooledConnectionLifetime    = TimeSpan.FromSeconds(60),
					ResponseDrainTimeout        = TimeSpan.FromSeconds(10),
				};

			if (clientCertificate != null)
			{
				// This option lets the operating system decide what versions
				// of SSL/TLS and certificates/keys to allow.

				httpHandler.SslOptions.EnabledSslProtocols = SslProtocols.None;

				httpHandler.SslOptions.ClientCertificates = new X509CertificateCollection();
				httpHandler.SslOptions.ClientCertificates.Add(clientCertificate);

				httpHandler.SslOptions.LocalCertificateSelectionCallback =
					(sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) =>
					{
						return clientCertificate;
					};

			if (remoteTls)
			{
				httpHandler.SslOptions.RemoteCertificateValidationCallback =
					(request, remoteCertificate, chain, policyErrors) =>
					{
						var remoteCertificate2 = (X509Certificate2)remoteCertificate;

						// If this proxy instance was passed a server certificate,
						// we're going to require that the remote certificate
						// thumbprint matches that one.
						//
						// Otherwise, we're going to require that the remote
						// certificate has been validated by the operating
						// system.

						if (validCertificate != null)
						{
							return remoteCertificate2.Thumbprint == validCertificate.Thumbprint;
						}
						else
						{
							return policyErrors == SslPolicyErrors.None;
						}
					};
			}
#else
			var httpHandler = new HttpClientHandler()
			{
					AllowAutoRedirect       = false,
					AutomaticDecompression  = DecompressionMethods.Deflate | DecompressionMethods.GZip,
					MaxConnectionsPerServer = 100,
			};

			if (remoteTls)
			{
				httpHandler.ServerCertificateCustomValidationCallback =
					(request, remoteCertificate, chain, policyErrors) =>
					{
						var remoteCertificate2 = (X509Certificate2)remoteCertificate;

						// If this proxy instance was passed a server certificate,
						// we're going to require that the remote certificate
						// thumbprint matches that one.
						//
						// Otherwise, we're going to require that the remote
						// certificate has been validated by the operating
						// system.

						if (validCertificate != null)
						{
							return remoteCertificate2.Thumbprint == validCertificate.Thumbprint;
						}
						else
						{
							return policyErrors == SslPolicyErrors.None;
						}
					};
			}
#endif

			client = new HttpClient(httpHandler, disposeHandler: true)
			{
				 BaseAddress = new Uri($"{remoteScheme}://{remoteHost}:{remotePort}/")
			};

			// Initialize the buffer pool.  We're going to use this to share
			// bufferes across requests to reduce pressure on the garbage 
			// collector.

			bufferPool = new Queue<byte[]>();

			// Crank up the HTTP listener.

			var settings = new WebListenerSettings();

			settings.UrlPrefixes.Add($"http://localhost:{localPort}/");

			this.listener = new WebListener(settings);
			this.listener.Start();

			// Handle received requests in a background task.

			Task.Run(() => RequestProcessor());
		}

		/// <ingeritdoc/>
		public void Dispose()
		{
			lock (syncLock)
			{
				if (client != null)
				{
					client.Dispose();
					client = null;
				}

				if (listener != null)
				{
					listener.Dispose();
					listener = null;
				}

				if (kubectlProxyProcess != null)
				{
					kubectlProxyProcess.Kill();
					kubectlProxyProcess = null;
				}
			}
		}

		/// <summary>
		/// Returns a buffer from the pool or allocates a new buffer if
		/// the pool is empty.
		/// </summary>
		private byte[] GetBuffer()
		{
			byte[] buffer = null;

			lock (syncLock)
			{
				if (bufferPool.Count > 0)
				{
					buffer = bufferPool.Dequeue();
				}
			}

			return buffer ?? new byte[BufferSize];
		}

		/// <summary>
		/// Releases a buffer by adding it back to the pool.
		/// </summary>
		/// <param name="buffer">The buffer.</param>
		private void ReleaseBuffer(byte[] buffer)
		{
			Covenant.Requires<ArgumentNullException>(buffer != null, nameof(buffer));

			lock (syncLock)
			{
				bufferPool.Enqueue(buffer);
			}
		}

		/// <summary>
		/// Handles received requests.
		/// </summary>
		/// <returns>The tracking <see cref="Task"/>.</returns>
		private async Task RequestProcessor()
		{
			while (true)
			{
				try
				{
					var newContext = await listener.AcceptAsync();

					// Process the request in its own task.
					
					_ = Task.Factory.StartNew(
						async (object arg) =>
						{
							var context  = (RequestContext)arg;
							var request  = context.Request;
							var response = context.Response;

							using (context)
							{
								try
								{
									// Let the request handler have a look.

									requestHandler?.Invoke(context);

									// Copy the headers, body, and other state from the received request to the remote request. 

									var remoteRequest = new HttpRequestMessage(new HttpMethod(request.Method), $"{request.Path}{request.QueryString}");

									remoteRequest.Version = request.ProtocolVersion;

									foreach (var header in request.Headers
										.Where(h => !ignoredRequestHeaders.Contains(h.Key)))
									{
										remoteRequest.Headers.Add(header.Key, header.Value.ToArray());
									}

									if (request.ContentLength.HasValue && request.ContentLength > 0 || 
										request.Headers.TryGetValue("Transfer-Encoding", out var values))
									{
										// Looks like the client is transmitting content.

										remoteRequest.Content = new StreamContent(request.Body);

										// Copy the important content related headers.

										if (request.Headers.TryGetValue("Content-Length", out var requestContentLengthHeader) && 
											long.TryParse(requestContentLengthHeader.First(), out var requestContentLength))
										{
											remoteRequest.Content.Headers.ContentLength = requestContentLength;
										}

										if (request.Headers.TryGetValue("Content-Type", out var requestContentTypeHeader))
										{
											remoteRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(requestContentTypeHeader.First());
										}

										// $todo(jefflill): 
										//
										// Not going to worry about these for now.  This will probably
										// never be an issue.

										//remoteRequest.Content.Headers.ContentMD5
										//remoteRequest.Content.Headers.ContentRange
									}

									// Forward the request to the remote endpoint.

									var remoteResponse = await client.SendAsync(remoteRequest, HttpCompletionOption.ResponseHeadersRead);

									// Copy the remote response headers, body, and other state to the client response.
									//
									// Don't copy the "Server" header because the [WebListener] adds its own server
									// header and we'd end up with multiple values.

									response.StatusCode   = (int)remoteResponse.StatusCode;
									response.ReasonPhrase = remoteResponse.ReasonPhrase;
									
									foreach (var header in remoteResponse.Headers
										.Where(h => !ignoredResponseHeaders.Contains(h.Key)))
									{
										response.Headers.Add(header.Key, header.Value.ToArray());
									}

									foreach (var header in remoteResponse.Content.Headers)
									{
										response.Headers.Add(header.Key, header.Value.ToArray());
									}

									// Use a buffer from the pool write the data returned from the
									// remote endpoint to the client response.

									var buffer = GetBuffer();

									using (var remoteStream = await remoteResponse.Content.ReadAsStreamAsync())
									{
										try
										{
											while (true)
											{
												var cb = await remoteStream.ReadAsync(buffer, 0, buffer.Length);

												if (cb == 0)
												{
													break;
												}

												await response.Body.WriteAsync(buffer, 0, cb);
											}
										}
										finally
										{
											ReleaseBuffer(buffer);
										}
									}

									// Let the response handler have a look.

									responseHandler?.Invoke(context);
								}
								catch (Exception e)
								{
									response.StatusCode   = 503;
									response.ReasonPhrase = "service unavailable";
									response.ContentType  = "text/plain";

									response.Body.Write(Encoding.UTF8.GetBytes(NeonHelper.ExceptionError(e)));
								}
							}
						},
						newContext);
				}
				catch (ObjectDisposedException)
				{
					return; // We're going to use this as the signal to stop.
				}
			}
		}
	}
}
