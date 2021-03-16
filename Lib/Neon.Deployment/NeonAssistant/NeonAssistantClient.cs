//-----------------------------------------------------------------------------
// FILE:	    NeonAssistantClient.cs
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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Deployment
{
    /// <inheritdoc/>
    public class NeonAssistantClient : INeonAssistantClient
    {
        /// <inheritdoc/>
        public NeonAssistantResponse Call(NeonAssistantRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            using (var pipe = new NamedPipeClientStream(".", DeploymentHelper.NeonAssistantPipe, PipeDirection.InOut))
            {
                pipe.Connect(30000);

                using (var reader = new StreamReader(pipe))
                {
                    using (var writer = new StreamWriter(pipe))
                    {
                        writer.WriteLine(request);
                        writer.Flush();

                        var response = NeonAssistantResponse.Parse(reader.ReadLine());

                        if (!response.Success)
                        {
                            throw new NeonAssistantException(response.Error);
                        }

                        return response;
                    }
                }
            }
        }

        /// <inheritdoc/>
        public string GetMasterPassword()
        {
            return Call(NeonAssistantRequest.Create("GET-MASTER-PASSWORD")).Value;
        }

        /// <inheritdoc/>
        public string GetProfileValue(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            var args = new Dictionary<string, string>();

            args.Add("name", name);

            return Call(NeonAssistantRequest.Create("GET-PROFILE-VALUE", args)).Value;
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

            return Call(NeonAssistantRequest.Create("GET-SECRET-PASSWORD", args)).Value;
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

            return Call(NeonAssistantRequest.Create("GET-SECRET-VALUE", args)).Value;
        }
    }
}
