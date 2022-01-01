//-----------------------------------------------------------------------------
// FILE:	    IProfileClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    /// <para>
    /// Defines the interface for the client used to communicate with the Neon Assistant
    /// or a custom service.  These services provides access to user and workstation specific 
    /// settings including secrets and general properties.  This is used for activities such as 
    /// CI/CD automation and integration testing.  This solves the following problems:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <para>
    /// Gaining access to secrets.  neonFORGE has standardized on 1Password for password management
    /// and the Neon Profile Service abstracts the details of authenticating with 1Password and
    /// accessing secrets.
    /// </para>
    /// <para>
    /// This interface supports two kinds of secrets: passwords and values.  These are somewhat 
    /// of an artifact of how we implemented this using 1Password.  Secret passwords are values
    /// retrieved from a 1Password item's <b>password field</b> and secret values correspond to
    /// a 1Password item <b>value field.</b>  We found this distinction useful because 1Password
    /// reports when passwords are insecure or duplicated but we have other secrets where these
    /// checks can be distracting.  Custom implementation can choose to follow this pattern or
    /// just treat both types of secret the same.
    /// </para>
    /// <para>
    /// You can also obtain a specific property from a secret password or value by using this syntax:
    /// </para>
    /// <example>
    /// SECRETNAME[PROPERTY]
    /// </example>
    /// <para>
    /// This is useful for obtaining both the username and password from a login, or all of the different
    /// properties from a credit card, etc.  This blurs the difference between secret passwords and secret
    /// values a bit but we're going to retain both concepts anyway.
    /// </para>
    /// </item>
    /// <item>
    /// <para>
    /// Profile values are also supported.  These are non-secret name/value pairs used for describing
    /// the local environment as required for CI/CD.  For example, we use this for describing the
    /// IP addresses available for deploying a test neonKUBE cluster.  Each developer will often
    /// need distict node IP addresses that work on the local LAN and also don't conflict with
    /// addresses assigned to other developers.
    /// </para>
    /// <para>
    /// neonFORGE's internal implementation simply persists profile values on the local workstation
    /// as a YAML file which is referenced by our profile service.
    /// </para>
    /// </item>
    /// <item>
    /// Abstracting access to the user's master password.  neonFORGE has implemented an internal  
    /// Windows application that implements a profile service that prompts the developer for their
    /// master 1Password, optionally caching it for a period of time so the user won't be prompted
    /// as often.  This server also handles profile and secret lookup.
    /// </item>
    /// </list>
    /// <b>Caching:</b>
    /// <para>
    /// <see cref="IProfileClient"/> implementations should implement caching of secret and profile
    /// values and should enable this by default.  Callers can disable caching by setting <see cref="CacheEnabled"/>
    /// to <c>false</c> and the cached can be cleared via <see cref="ClearCache()"/>
    /// </para>
    /// <para>
    /// The <b>Neon.Deployment.ProfileClient</b> implementation communicates with the <b>neon-assistant</b>
    /// to retrieve profile values and secrets.  <b>neon-assistant</b> manages profile values directly but
    /// communicates with 1Password.com to obtain secrets, which can take a second or two.  Caching will
    /// improve performance and also take some load off of 1Password. 
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="IProfileClient"/> is used to submit requests to the Neon Assistant
    /// application running on the local workstation including retrieving the user's 
    /// master 1Password, secrets, as well as user profile values.
    /// </para>
    /// </remarks>
    public interface IProfileClient
    {
        /// <summary>
        /// Controls whether the client caches secrets and profile values.  This should
        /// enabled by default by all implementations.
        /// </summary>
        bool CacheEnabled { get; set; }

        /// <summary>
        /// Requests the value of a secret password from 1Password via the assistant.
        /// </summary>
        /// <param name="name">Specifies the secret name.</param>
        /// <param name="vault">Optionally specifies the 1Password vault.  This defaults to the developer's neonFORGE user name as specified by the <c>NC_USER</c> environment variable.</param>
        /// <param name="masterPassword">Optionally specifies the master 1Password when it is already known.</param>
        /// <param name="nullOnNotFound">Optionally specifies that <c>null</c> should be returned rather than throwing an exception when the secret does not exist.</param>
        /// <returns>The password value.</returns>
        /// <exception cref="ProfileException">Thrown if the profile server returns an error.</exception>
        string GetSecretPassword(string name, string vault = null, string masterPassword = null, bool nullOnNotFound = false);

        /// <summary>
        ///  Requests the value of a secret value from 1Password via the assistant.
        /// </summary>
        /// <param name="name">Specifies the secret name.</param>
        /// <param name="vault">Optionally specifies the 1Password vault.  This defaults to the developer's neonFORGE user name as specified by the <c>NC_USER</c> environment variable.</param>
        /// <param name="masterPassword">Optionally specifies the master 1Password when it is already known.</param>
        /// <param name="nullOnNotFound">Optionally specifies that <c>null</c> should be returned rather than throwing an exception when the secret does not exist.</param>
        /// <returns>The password value.</returns>
        /// <exception cref="ProfileException">Thrown if the profile server returns an error.</exception>
        string GetSecretValue(string name, string vault = null, string masterPassword = null, bool nullOnNotFound = false);

        /// <summary>
        /// Requests a profile value from the assistant.
        /// </summary>
        /// <param name="name">Identifies the profile value.</param>
        /// <param name="nullOnNotFound">Optionally specifies that <c>null</c> should be returned rather than throwing an exception when the profile value does not exist.</param>
        /// <returns>The password value.</returns>
        /// <exception cref="ProfileException">Thrown if the profile server returns an error.</exception>
        string GetProfileValue(string name, bool nullOnNotFound = false);

        /// <summary>
        /// Clears any cached values.
        /// </summary>
        void ClearCache();

        /// <summary>
        /// <para>
        /// Submits a low-level request to the profile provider, passing a command and optional arguments.
        /// This is temporarily used by the <c>Neon.Deployment.GitHub</c> APIs to workaround the lack of
        /// a complete REST API for GHCR.
        /// </para>
        /// <note>
        /// Implementation of this is optional and you may throw a <see cref="NotImplementedException"/>.
        /// </note>
        /// </summary>
        /// <param name="args">The request arguments.</param>
        /// <returns>The command result.</returns>
        string Call(Dictionary<string, string> args);
    }
}
