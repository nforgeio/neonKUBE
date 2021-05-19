//-----------------------------------------------------------------------------
// FILE:	    GitHub.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Security;
using Neon.Common;

namespace Neon.Deployment
{
    /// <summary>
    /// Implements common GitHub operations via the GitHub REST API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To use this class, first call <see cref="GetCredentials"/> to load the necessary
    /// credentials from 1Password and the call the desired APIs.  When you're done, it's
    /// a good practice to call <see cref="ClearCredentials()"/>.
    /// </para>
    /// <note>
    /// This class currently requires that the <b>GITHUB_PAT</b> (personal access token) 
    /// and <b>GITHUB_LOGIN</b> variables be available via 1Password for the current user.
    /// We need <b>GITHUB_LOGIN</b> right now so we can login and screen-scrap the GitHub
    /// website for package operations that don't have REST endpoints yet.
    /// </note>
    /// </remarks>
    public static partial class GitHub
    {
        /// <summary>
        /// Returns the GitHub PAT (personal access token) or <c>null</c>.
        /// </summary>
        internal static string AccessToken { get; private set; }

        /// <summary>
        /// Returns the GitHub user credentials or <c>null</c>.
        /// </summary>
        internal static Credentials Credentials { get; private set; }

        /// <summary>
        /// Retrieves the necessary credentials from 1Password and caches them.
        /// </summary>
        public static void GetCredentials()
        {
            var profile = new ProfileClient();

            AccessToken   = profile.GetSecretPassword("GITHUB_PAT");
            Credentials = Credentials.FromUserPassword(profile.GetSecretPassword("GITHUB_LOGIN[username]"), profile.GetSecretPassword("GITHUB_LOGIN[password]"));
        }

        /// <summary>
        /// Clears any cached credentials.
        /// </summary>
        public static void ClearCredentials()
        {
            AccessToken   = null;
            Credentials = null;
        }

        /// <summary>
        /// Ensures that the necessary credentials are loaded.
        /// </summary>
        /// <exception cref="SecurityException">Thrown when the credentials are not available.</exception>
        internal static void EnsureCredentials()
        {
            if (string.IsNullOrEmpty(AccessToken) || Credentials == null)
            {
                throw new SecurityException("GitHub PAT and/or user credentials are not loaded.");
            }
        }

        /// <summary>
        /// Returns the API class for managing GitHub packages.
        /// </summary>
        public static GitHubPackageApi Packages { get; private set; } = new GitHubPackageApi();

        /// <summary>
        /// Returns the API class for managing GitHub Actions.
        /// </summary>
        public static GitHubActionsApi Actions { get; private set; } = new GitHubActionsApi();
    }
}
