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

namespace Neon.Deployment
{
    /// <summary>
    /// Implements a named-pipe based server that will be used to receive
    /// requests from <see cref="ProfileClient"/>.  This server listens
    /// on a named pipe and only allows connections from other processes 
    /// running on behalf of the current user.
    /// </summary>
    /// <remarks>
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
    /// as the string after the prefix.  Future) operations may return a JSON result.
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
    ///     <term><b>GET-MASTER-PASSWORD</b></term>
    ///     <description>
    ///     <para><c>(no args)</c></para>
    ///     <para>
    ///     This requests the user's master 1Password.  No parameters are supported.
    ///     The password is returned as the response.
    ///     </para>
    ///     </description>
    /// </item>
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
    /// </list>
    /// </remarks>
    public sealed class ProfileServer : IDisposable
    {
        private readonly object             syncLock = new object();
        private string                      pipeName;
        private Thread[]                    threads;
        private NamedPipeServerStream[]     pipes;
        private bool                        disposing;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="pipeName">The server named pipe name.  This defaults to <see cref="DeploymentHelper.NeonProfileServicePipe"/>.</param>
        /// <param name="threadCount">Optionally specifies the number of threads to create to handle inbound requests.  This defaults to <b>1</b>.</param>
        public ProfileServer(string pipeName = DeploymentHelper.NeonProfileServicePipe, int threadCount = 1)
        {
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

            if (GetMasterPasswordHandler == null)
            {
                throw new InvalidOperationException($"The [{nameof(GetMasterPasswordHandler)}] is not initalized.");
            }

            if (GetProfileValueHandler == null)
            {
                throw new InvalidOperationException($"The [{nameof(GetProfileValueHandler)}] is not initalized.");
            }

            if (GetSecretPasswordHandler == null)
            {
                throw new InvalidOperationException($"The [{nameof(GetSecretPasswordHandler)}] is not initalized.");
            }

            if (GetMasterPasswordHandler == null)
            {
                throw new InvalidOperationException($"The [{nameof(GetSecretValueHandler)}] is not initalized.");
            }

            // Start the listening threads.

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(new ParameterizedThreadStart(ServerLoop));
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
            // when the underlying pipe id disposed.  I was hoping to catch an
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
        /// Callback to retrieve the master 1Password.
        /// </para>
        /// <note>
        /// This must be initalized before calling <see cref="Start()"/>.
        /// </note>
        /// </summary>
        public Func<ProfileHandlerResult> GetMasterPasswordHandler { get; set; }

        /// <summary>
        /// <para>
        /// Callback to retrieve a profile value.  The parameters is the profile value name.
        /// </para>
        /// <note>
        /// This must be initalized before calling <see cref="Start()"/>.
        /// </note>
        /// </summary>
        public Func<string, ProfileHandlerResult> GetProfileValueHandler { get; set; }

        /// <summary>
        /// <para>
        /// Callback to retrieve a secret password.  The parameters are the secret name
        /// optional vault and master password.
        /// </para>
        /// <note>
        /// This must be initalized before calling <see cref="Start()"/>.
        /// </note>
        /// </summary>
        public Func<string, string, string, ProfileHandlerResult> GetSecretPasswordHandler { get; set; }

        /// <summary>
        /// <para>
        /// Callback to retrieve a secret value.  The parameters are the secret name
        /// optional vault, and master password.
        /// </para>
        /// <note>
        /// This must be initalized before calling <see cref="Start()"/>.
        /// </note>
        /// </summary>
        public Func<string, string, string, ProfileHandlerResult> GetSecretValueHandler { get; set; }

        /// <summary>
        /// Handles incoming client connections on a background thread.
        /// </summary>
        /// <param name="pipeIndexObject">Passes as the index into the [pipes] array this thread will use for its server side pipe.</param>
        private void ServerLoop(object pipeIndexObject)
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

                    using (var reader = new StreamReader(pipe))
                    {
                        using (var writer = new StreamWriter(pipe))
                        {
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
                                }

                                continue;
                            }

                            request.Args.TryGetValue("name", out var name);
                            request.Args.TryGetValue("vault", out var vault);
                            request.Args.TryGetValue("masterpassword", out var masterPassword);

                            try
                            {
                                switch (request.Command)
                                {
                                    case "GET-MASTER-PASSWORD":

                                        handlerResult = GetMasterPasswordHandler();
                                        break;

                                    case "GET-PROFILE-VALUE":

                                        if (name == null)
                                        {
                                            handlerResult = ProfileHandlerResult.CreateError(ProfileStatus.MissingArg, $"GET-PROFILE-VALUE: [name] argument is required.");
                                            break;
                                        }

                                        handlerResult = GetProfileValueHandler(name);
                                        break;

                                    case "GET-SECRET-PASSWORD":

                                        if (name == null)
                                        {
                                            handlerResult = ProfileHandlerResult.CreateError(ProfileStatus.MissingArg, $"GET-SECRET-PASSWORD: [name] argument is required.");
                                            break;
                                        }

                                        handlerResult = GetSecretPasswordHandler(name, vault, masterPassword);
                                        break;

                                    case "GET-SECRET-VALUE":

                                        if (name == null)
                                        {
                                            handlerResult = ProfileHandlerResult.CreateError(ProfileStatus.MissingArg, $"GET-SECRET-VALUE: [name] argument is required.");
                                            break;
                                        }

                                        handlerResult = GetSecretValueHandler(name, vault, masterPassword);
                                        break;

                                    default:

                                        handlerResult = ProfileHandlerResult.CreateError(ProfileStatus.BadCommand, $"Unexpected command: {request.Command}");
                                        break;
                                }
                            }
                            catch (Exception e)
                            {
                                handlerResult = ProfileHandlerResult.CreateError(ProfileStatus.BadCommand, NeonHelper.ExceptionError(e));
                            }

                            writer.WriteLine(handlerResult.ToResponse());
                        }
                    }
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
