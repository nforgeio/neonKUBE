//-----------------------------------------------------------------------------
// FILE:	    INeonAssistantClient.cs
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
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Deployment
{
    /// <summary>
    /// Defines the interface for the client used to communicate with the neonASSISTANT.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="NeonAssistantClient"/> is used to submit requests to the neonASSISTANT
    /// application running on the local workstation including retrieving the user's 
    /// master 1Password, secrets, as well as user profile values.
    /// </para>
    /// <para>
    /// Use the <see cref="Call(NeonAssistantRequest)"/> method to submit a generic request
    /// to the assistant and the <see cref="GetMasterPassword()"/>, <see cref="GetSecretPassword(string, string)"/>,
    /// <see cref="GetSecretValue(string, string)"/>, and <see cref="GetProfileValue(string)"/> methods
    /// to perform common operations.
    /// </para>
    /// <note>
    /// The underlying <see cref="Call(NeonAssistantRequest)"/> method works by establishing a
    /// named pipe connection to the assistant listing on the <see cref="DeploymentHelper.NeonAssistantPipe"/>
    /// pipe and then submiting a request and waiting for a response on that pipe.  Then the
    /// pipe is closed and a new connection will be established.
    /// </note>
    /// </remarks>
    public interface INeonAssistantClient
    {
        /// <summary>
        /// Submits a request to the neonASSISTANT and returns the response.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="NeonAssistantException">Thrown if the neonASSISTANT returns an error.</exception>
        NeonAssistantResponse Call(NeonAssistantRequest request);

        /// <summary>
        /// Requests the current developer's master 1password from the assistant.
        /// </summary>
        /// <returns>The master password.</returns>
        /// <exception cref="NeonAssistantException">Thrown if the neonASSISTANT returns an error.</exception>
        string GetMasterPassword();

        /// <summary>
        /// Requests the value of a secret password from 1Password via the assistant.
        /// </summary>
        /// <param name="name">Specifies the secret name.</param>
        /// <param name="vault">Optionally specifies the 1Password vault.  This defaults to the developer's neonFORGE user name as specified by the <c>NC_USER</c> environment variable.</param>
        /// <returns>The password value.</returns>
        /// <exception cref="NeonAssistantException">Thrown if the neonASSISTANT returns an error.</exception>
        string GetSecretPassword(string name, string vault = null);

        /// <summary>
        ///  Requests the value of a secret value from 1Password via the assistant.
        /// </summary>
        /// <param name="name">Specifies the secret name.</param>
        /// <param name="vault">Optionally specifies the 1Password vault.  This defaults to the developer's neonFORGE user name as specified by the <c>NC_USER</c> environment variable.</param>
        /// <returns>The password value.</returns>
        /// <exception cref="NeonAssistantException">Thrown if the neonASSISTANT returns an error.</exception>
        string GetSecretValue(string name, string vault = null);

        /// <summary>
        /// Requests a profile value from the assistant.
        /// </summary>
        /// <param name="name">Identifies the profile value.</param>
        /// <returns>The password value.</returns>
        /// <exception cref="NeonAssistantException">Thrown if the neonASSISTANT returns an error.</exception>
        string GetProfileValue(string name);
    }
}
