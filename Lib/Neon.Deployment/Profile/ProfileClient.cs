//-----------------------------------------------------------------------------
// FILE:	    ProfileClient.cs
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
using System.Text;
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Deployment
{
    /// <inheritdoc/>
    public partial class ProfileClient : IProfileClient
    {
        private readonly string     pipeName;
        private readonly TimeSpan   connectTimeout;

        /// <summary>
        /// <para>
        /// Constructs a profile client with default parameters.  This is suitable for 
        /// constructing from Powershell scripts.
        /// </para>
        /// <note>
        /// <see cref="ProfileClient"/> currently supports only Windows.
        /// </note>
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when not running on Windows.</exception>
        public ProfileClient()
            : this(DeploymentHelper.NeonProfileServicePipe)
        {
        }

        /// <summary>
        /// <para>
        /// Constructor with optional client timeout.
        /// </para>
        /// <note>
        /// <see cref="ProfileClient"/> currently supports only Windows.
        /// </note>
        /// </summary>
        /// <param name="pipeName">Specifies the server pipe name.</param>
        /// <param name="connectTimeout">Optionally specifies the connection timeout.  This defaults to <b>10 seconds</b>.</param>
        /// <exception cref="NotSupportedException">Thrown when not running on Windows.</exception>
        public ProfileClient(string pipeName, TimeSpan connectTimeout = default)
        {
            Covenant.Requires<NotSupportedException>(NeonHelper.IsWindows, $"[{nameof(ProfileClient)}] currently only supports Windows.");
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(pipeName), nameof(pipeName));

            this.pipeName = pipeName;

            if (connectTimeout <= TimeSpan.Zero)
            {
                connectTimeout = TimeSpan.FromSeconds(10);
            }

            this.connectTimeout = connectTimeout;
        }

        /// <summary>
        /// Submits a request to the profile server and returns the response.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ProfileException">Thrown if the profile server returns an error.</exception>
        private IProfileResponse Call(IProfileRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
            {
                try
                {
                    pipe.Connect((int)connectTimeout.TotalMilliseconds);
                }
                catch (TimeoutException e)
                {
                    throw new ProfileException("Cannot connect to profile server.  Is [neon-assistant] running?", e);
                }

                var reader = new StreamReader(pipe);
                var writer = new StreamWriter(pipe);

                writer.AutoFlush = true;
                writer.WriteLine(request);

                var responseLine = reader.ReadLine();

                if (responseLine == null)
                {
                    throw new ProfileException("The profile server did not respond.");
                }

                var response = ProfileResponse.Parse(responseLine);

                pipe.Close();

                if (!response.Success)
                {
                    throw new ProfileException(response.Error);
                }

                return response;
            }
        }

        /// <inheritdoc/>
        public string GetProfileValue(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            var args = new Dictionary<string, string>();

            args.Add("name", name);

            return Call(ProfileRequest.Create("GET-PROFILE-VALUE", args)).Value;
        }

        /// <inheritdoc/>
        public string GetSecretPassword(string name, string vault = null, string masterPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            var args = new Dictionary<string, string>();

            args.Add("name", name);

            if (!string.IsNullOrEmpty(vault))
            {
                args.Add("vault", vault);
            }

            if (!string.IsNullOrEmpty(masterPassword))
            {
                args.Add("masterpassword", masterPassword);
            }

            return Call(ProfileRequest.Create("GET-SECRET-PASSWORD", args)).Value;
        }

        /// <inheritdoc/>
        public string GetSecretValue(string name, string vault = null, string masterPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            var args = new Dictionary<string, string>();

            args.Add("name", name);

            if (!string.IsNullOrEmpty(vault))
            {
                args.Add("vault", vault);
            }

            if (!string.IsNullOrEmpty(masterPassword))
            {
                args.Add("masterpassword", masterPassword);
            }

            return Call(ProfileRequest.Create("GET-SECRET-VALUE", args)).Value;
        }
    }
}
