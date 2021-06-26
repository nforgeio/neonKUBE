//-----------------------------------------------------------------------------
// FILE:	    ProfileServer.cs
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
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using System.Linq;

namespace Neon.Deployment
{
    /// <summary>
    /// Implements a named-pipe based server that will be used to receive
    /// requests from <see cref="ProfileClient"/>.  This server listens
    /// on a named pipe and only allows connections from other processes 
    /// running on behalf of the current user.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This class currently supports only Windows.
    /// </note>
    /// <para>
    /// This server implements simple string based request response protocol,
    /// where the client writes a line of text with the request and the server
    /// sends a line of text as the response.  Only one request/response per
    /// client pipe connection is allowed.  Requests are formatted like:
    /// </para>
    /// <example>
    /// <b>COMMAND:</b> [ ARG1=VALUE1, ARG2=VALUE2,... ]
    /// </example>
    /// <para>
    /// where <b>COMMAND</b> is one of the values below with one or more comma
    /// separated arguments formatted as name/value pairs.  Response lines are
    /// formatted like:
    /// </para>
    /// <example>
    /// <b>OK:</b>
    /// <b>OK: RESULT</b>
    /// <b>OK-JSON: JSON</b>
    /// <b>ERROR: MESSAGE</b>
    /// </example>
    /// <para>
    /// where the "OK:" and "OK-JSON:" prefixes indicate that the operation succeeded.
    /// Some operations like password or value lookups simply return the request result
    /// as the string after the prefix.  Future operations may return a JSON result.
    /// </para>
    /// <para>
    /// The <b>ERROR[STATUS]:</b> prefix indicates an error occured.  <b>STATUS</b> identifies
    /// the specific error and the response will typically include an message describing
    /// what happened.  The supported status codes are defined by <see cref="ProfileStatus"/>.
    /// </para>
    /// <para>
    /// Here are the supported commands:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>GET-SECRET-PASSWORD</b></term>
    ///     <description>
    ///     <para><c>(name, [vault], [masterpassword])</c></para>
    ///     <para>
    ///     This requests a password from 1Password by <b>name</b> and <b>vault</b>, which
    ///     is optional and defaults to the user name as defined by the <b>userVault</b>
    ///     Neon Assistant setting.  The password is returned as the response.
    ///     </para>
    ///     <para>
    ///     <b>masterpassword</b> is optional.  This is passed in circumstances where the
    ///     caller already knows the master password, such as for fully automated
    ///     CI/CD operations.
    ///     </para>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>GET-SECRET-VALUE</b></term>
    ///     <description>
    ///     <para><c>(name, [vault], [masterpassword])</c></para>
    ///     <para>
    ///     This requests a secret value from 1Password by <b>name</b> and <b>vault</b>, which
    ///     is optional and defaults to the user name as defined by the <b>userVault</b>
    ///     Neon Assistant setting.  The value is returned as the response.
    ///     </para>
    ///     <para>
    ///     <b>masterpassword</b> is optional.  This is passed in circumstances where the
    ///     caller already knows the master password, such as for fully automated
    ///     CI/CD operations.
    ///     </para>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>GET-PROFILE-VALUE</b></term>
    ///     <description>
    ///     <para><c>(name)</c></para>
    ///     <para>
    ///     This requests a profile value the user's local profile by <c>NAME</c>.
    ///     he value is returned as the response.
    ///     </para>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>CALL</b></term>
    ///     <description>
    ///     <para>
    ///     This submits an arbitrary operation to the server, passing arguments and
    ///     returning a result string.  We're using this to workaround some limitations
    ///     with the GHCR REST API by locating the implementation in neon-assistant.
    ///     </para>
    ///     <para>
    ///     We may use this in the future for other neon-assistant interactions.
    ///     </para>
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    public sealed class ProfileServer : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// <para>
        /// Parses a secret name by extracting the <b>name</b> and <b>property</b>
        /// components.  secret names can be formatted like: <b>NAME</b> or <b>NAME[PROPERTY]</b>.
        /// </para>
        /// <note>
        /// When the property syntax passed is malformed, we're just going to return the
        /// entire input string as the name rather than throwing an exception here.  This
        /// will probably result in a failed lookup which will be reported to the user who
        /// will have a good chance then of figuring out what happened.
        /// </note>
        /// </summary>
        /// <param name="secretName">The secret name.</param>
        /// <returns>An anonymous structure including the name and property (if specified).</returns>
        public static (string Name, string Property) ParseSecretName(string secretName)
        {
            Covenant.Requires<ArgumentNullException>(secretName != null, nameof(secretName));

            var pLeftBracket  = secretName.IndexOf('[');
            var pRightBracket = -1;

            if (pLeftBracket != -1)
            {
                if (secretName.Last() == ']')
                {
                    pRightBracket = secretName.Length - 1;
                }
            }

            if (pLeftBracket != -1 && pRightBracket != -1)
            {
                var name     = secretName.Substring(0, pLeftBracket);
                var property = secretName.Substring(pLeftBracket + 1, pRightBracket - pLeftBracket - 1);

                if (property == string.Empty)
                {
                    return (Name: name, Property: null);
                }
                else
                {
                    return (Name: name, Property: property);
                }
            }
            else
            {
                return (Name: secretName, Property: null);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly object             syncLock = new object();
        private string                      pipeName;
        private Thread[]                    threads;
        private NamedPipeServerStream[]     pipes;
        private bool                        disposing;

        /// <summary>
        /// <para>
        /// Constructor.
        /// </para>
        /// <note>
        /// <see cref="ProfileServer"/> currently supports only Windows.
        /// </note>
        /// </summary>
        /// <param name="pipeName">The server named pipe name.  This defaults to <see cref="DeploymentHelper.NeonProfileServicePipe"/>.</param>
        /// <param name="threadCount">Optionally specifies the number of threads to create to handle inbound requests.  This defaults to <b>10</b>.</param>
        public ProfileServer(string pipeName = DeploymentHelper.NeonProfileServicePipe, int threadCount = 10)
        {
            Covenant.Requires<NotSupportedException>(NeonHelper.IsWindows, $"[{nameof(ProfileServer)}] currently only supports Windows.");
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(pipeName));
            Covenant.Requires<ArgumentException>(threadCount > 0, nameof(threadCount));

            this.pipeName = pipeName;
            this.threads  = new Thread[threadCount];
            this.pipes    = new NamedPipeServerStream[threadCount];
        }

        /// <summary>
        /// Starts the server.  You should call this after configuring the handler callbacks.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if any of the handlers are not initialized.</exception>
        public void Start()
        {
            // Ensure that all of the handlers are initialized.

            if (GetProfileValueHandler == null)
            {
                throw new InvalidOperationException($"The [{nameof(GetProfileValueHandler)}] is not initalized.");
            }

            if (GetSecretPasswordHandler == null)
            {
                throw new InvalidOperationException($"The [{nameof(GetSecretPasswordHandler)}] is not initalized.");
            }

            // Start the listening threads.

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(new ParameterizedThreadStart(ServerThread));
                threads[i].Start(i);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (syncLock)
            {
                if (disposing)
                {
                    return;
                }

                disposing = true;

                for (int i = 0; i < pipes.Length; i++)
                {
                    if (pipes[i] != null)
                    {
                        pipes[i].Dispose();
                        pipes[i] = null;
                    }
                }

                // $hack(jefflill):
                //
                // The [NamedPipeStream.WaitForConnection()] doesn't throw an exception
                // when the underlying pipe id disposed.  I was hoping to catch an
                // [ObjectDisposedException] in the server threads as the signal for 
                // the thread to exit.
                //
                // The simple alternative is to establish a (fake) client connection
                // for each thread.
            }

            // $hack(jefflill):
            //
            // The [NamedPipeStream.WaitForConnection()] doesn't throw an exception
            // when the underlying pipe is disposed.  I was hoping to catch an
            // [ObjectDisposedException] in the server threads as the signal for 
            // the thread to exit.
            //
            // The simple alternative is to establish a (fake) client connection
            // for each thread.  This will cause the [WaitForConnection()] to return
            // and then the thread will use [disposing] to know when to exit.

            for (int i = 0; i < threads.Length; i++)
            {
                using (var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
                {
                    try
                    {
                        clientPipe.Connect(100);
                    }
                    catch (TimeoutException)
                    {
                        // Ignoring these
                    }
                }
            }

            // Wait for the threads to terminate.

            foreach (var thread in threads)
            {
                thread.Join();
            }

            threads = null;
        }

        /// <summary>
        /// Optional callback used to determine whether the profile server implementation
        /// is ready for requests.  The handler returns <c>null</c> when ready or the
        /// a <see cref="ProfileHandlerResult"/> error to be returned to the caller.
        /// </summary>
        public Func<ProfileHandlerResult> GetIsReady { get; set; }

        /// <summary>
        /// <para>
        /// Callback that retrieves a profile value.  The parameters is the profile value name.
        /// </para>
        /// <note>
        /// This must be initalized before calling <see cref="Start()"/>.
        /// </note>
        /// </summary>
        public Func<ProfileRequest, string, ProfileHandlerResult> GetProfileValueHandler { get; set; }

        /// <summary>
        /// <para>
        /// Callback that retrieves a secret password.  The parameters are the secret name
        /// optional vault and master password.
        /// </para>
        /// <note>
        /// This must be initalized before calling <see cref="Start()"/>.
        /// </note>
        /// </summary>
        public Func<ProfileRequest, string, string, string, ProfileHandlerResult> GetSecretPasswordHandler { get; set; }

        /// <summary>
        /// <para>
        /// Callback that retrieves a secret value.  The parameters are the secret name
        /// optional vault, and master password.
        /// </para>
        /// <note>
        /// This must be initalized before calling <see cref="Start()"/>.
        /// </note>
        /// </summary>
        public Func<ProfileRequest, string, string, string, ProfileHandlerResult> GetSecretValueHandler { get; set; }

        /// <summary>
        /// <para>
        /// Callback that performs an arbitrary operation.
        /// </para>
        /// <note>
        /// This must be initalized before calling <see cref="Start()"/>.
        /// </note>
        /// </summary>
        public Func<ProfileRequest, ProfileHandlerResult> CallHandler { get; set; }

        /// <summary>
        /// Handles incoming client connections on a background thread.
        /// </summary>
        /// <param name="pipeIndexObject">Passes as the index into the [pipes] array this thread will use for its server side pipe.</param>
        private void ServerThread(object pipeIndexObject)
        {
            try
            {
                var pipeIndex = (int)pipeIndexObject;

                while (true)
                {
                    lock (syncLock)
                    {
                        if (disposing)
                        {
                            // The server is shutting down, so exit the thread.

                            return;
                        }

                        if (pipes[pipeIndex] != null)
                        {
                            pipes[pipeIndex].Dispose();
                            pipes[pipeIndex] = null;
                        }

                        pipes[pipeIndex] = new NamedPipeServerStream(pipeName, PipeDirection.InOut, maxNumberOfServerInstances: threads.Length, PipeTransmissionMode.Message, PipeOptions.CurrentUserOnly);
                    }

                    var pipe = pipes[pipeIndex];

                    pipe.WaitForConnection();

                    if (disposing)
                    {
                        return;
                    }

                    var reader = new StreamReader(pipe);
                    var writer = new StreamWriter(pipe);

                    writer.AutoFlush = true;

                    var requestLine   = reader.ReadLine();
                    var request       = (ProfileRequest)null;
                    var handlerResult = (ProfileHandlerResult)null;

                    try
                    {
                        request = ProfileRequest.Parse(requestLine);
                    }
                    catch (FormatException)
                    {
                        // Report an malformed request to the client and then continue
                        // listening for the next request.

                        writer.WriteLine(ProfileResponse.CreateError(ProfileStatus.BadRequest, "Malformed request"));
                        return;
                    }

                    if (GetIsReady != null)
                    {
                        handlerResult = GetIsReady();

                        if (handlerResult != null)
                        {
                            writer.WriteLine(handlerResult.ToResponse());
                            pipe.WaitForPipeDrain();
                            pipe.Close();
                            continue;
                        }
                    }

                    request.Args.TryGetValue("name", out var name);
                    request.Args.TryGetValue("vault", out var vault);
                    request.Args.TryGetValue("masterpassword", out var masterPassword);

                    try
                    {
                        switch (request.Command)
                        {
                            case "GET-PROFILE-VALUE":

                                if (name == null)
                                {
                                    handlerResult = ProfileHandlerResult.CreateError(request, ProfileStatus.MissingArg, $"GET-PROFILE-VALUE: [name] argument is required.");
                                    break;
                                }

                                handlerResult = GetProfileValueHandler(request, name);
                                break;

                            case "GET-SECRET-PASSWORD":

                                if (name == null)
                                {
                                    handlerResult = ProfileHandlerResult.CreateError(request, ProfileStatus.MissingArg, $"GET-SECRET-PASSWORD: [name] argument is required.");
                                    break;
                                }

                                handlerResult = GetSecretPasswordHandler(request, name, vault, masterPassword);
                                break;

                            case "GET-SECRET-VALUE":

                                if (name == null)
                                {
                                    handlerResult = ProfileHandlerResult.CreateError(request, ProfileStatus.MissingArg, $"GET-SECRET-VALUE: [name] argument is required.");
                                    break;
                                }

                                handlerResult = GetSecretValueHandler(request, name, vault, masterPassword);
                                break;

                            case "CALL":

                                if (CallHandler == null)
                                {
                                    handlerResult = ProfileHandlerResult.CreateError(request, ProfileStatus.BadCommand, $"Server has no call handler.");
                                }
                                else
                                {
                                    handlerResult = CallHandler(request);
                                }
                                break;

                            default:

                                handlerResult = ProfileHandlerResult.CreateError(request, ProfileStatus.BadCommand, $"Unexpected command: {request.Command}");
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        handlerResult = ProfileHandlerResult.CreateError(request, ProfileStatus.BadCommand, NeonHelper.ExceptionError(e));
                    }

                    writer.WriteLine(handlerResult.ToResponse());
                    pipe.WaitForPipeDrain();
                    pipe.Disconnect();
                }
            }
            catch
            {
                // Handle all exceptions by exiting the thread.

                return;
            }
        }
    }
}
