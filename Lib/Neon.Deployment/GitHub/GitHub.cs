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
using System.Net.Http.Headers;
using System.Security;

using Neon.Common;
using Neon.Net;

using Octokit;

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
        internal static Neon.Common.Credentials Credentials { get; private set; }

        /// <summary>
        /// Retrieves the necessary credentials from 1Password when necessary and 
        /// caches them locally as well as in environment variables.
        /// </summary>
        public static void GetCredentials()
        {
            if (AccessToken == null)
            {
                var gitHubPat      = Environment.GetEnvironmentVariable("GITHUB_PAT");
                var gitHubUsername = Environment.GetEnvironmentVariable("GITHUB_USERNAME");
                var gitHubPassword = Environment.GetEnvironmentVariable("GITHUB_PASSWORD");

                if (!string.IsNullOrEmpty(gitHubPat) &&
                    !string.IsNullOrEmpty(gitHubUsername) &&
                    !string.IsNullOrEmpty(gitHubPassword))
                {
                    AccessToken = gitHubPat;
                    Credentials = Neon.Common.Credentials.FromUserPassword(gitHubUsername, gitHubPassword);
                }
                else
                {
                    var profile = new ProfileClient();

                    AccessToken = profile.GetSecretPassword("GITHUB_PAT");
                    Credentials = Neon.Common.Credentials.FromUserPassword(profile.GetSecretPassword("GITHUB_LOGIN[username]"), profile.GetSecretPassword("GITHUB_LOGIN[password]"));

                    Environment.SetEnvironmentVariable("GITHUB_PAT", AccessToken);
                    Environment.SetEnvironmentVariable("GITHUB_USERNAME", Credentials.Username);
                    Environment.SetEnvironmentVariable("GITHUB_PASSWORD", Credentials.Password);
                }
            }
        }

        /// <summary>
        /// Clears any cached credentials.
        /// </summary>
        public static void ClearCredentials()
        {
            AccessToken = null;
            Credentials = null;
        }

        /// <summary>
        /// Returns the API class for managing GitHub packages.
        /// </summary>
        public static GitHubPackageApi Packages { get; private set; } = new GitHubPackageApi();

        /// <summary>
        /// Returns the API class for managing GitHub Actions.
        /// </summary>
        public static GitHubActionsApi Actions { get; private set; } = new GitHubActionsApi();

        /// <summary>
        /// Returns the API class for managing GitHub releases.
        /// </summary>
        public static GitHubReleaseApi Release { get; private set; } = new GitHubReleaseApi();

        /// <summary>
        /// Creates a REST client that can be used to manage GitHub.
        /// </summary>
        /// <param name="repo">Identifies the target repo.</param>
        /// <returns>The <see cref="GitHubClient"/> instance.</returns>
        internal static GitHubClient CreateGitHubClient(string repo)
        {
            GitHub.GetCredentials();

            var repoPath = GitHubRepoPath.Parse(repo);
            var client = new GitHubClient(new Octokit.ProductHeaderValue("neonkube.com"));    // $todo(jefflill): https://github.com/nforgeio/neonKUBE/issues/1214

            client.Credentials = new Octokit.Credentials(AccessToken);

            return client;
        }

        /// <summary>
        /// Creates a <see cref="JsonClient"/> that can be used to manage GitHub.
        /// </summary>
        /// <returns>The <see cref="JsonClient"/> instance.</returns>
        internal static JsonClient CreateJsonClient()
        {
            GitHub.GetCredentials();

            var client = new JsonClient()
            {
                BaseAddress = new Uri("https://api.github.com/"),
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", AccessToken);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("neonforge.com", "0"));       // $todo(jefflill): https://github.com/nforgeio/neonKUBE/issues/1214
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            return client;
        }
    }
}
