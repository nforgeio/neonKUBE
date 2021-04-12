//-----------------------------------------------------------------------------
// FILE:	    GitHubPackageApi.cs
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
using System.Net.Http;

using Neon.Common;

namespace Neon.Deployment
{
    //-------------------------------------------------------------------------
    // IMPLEMENTATION NOTE:
    //
    // GitHub Packages is still in beta at the time this class was created and
    // there doesn't appear to be an REST API wrapper class available at this
    // time, so we're going to create our own here.
    //
    // Here's the REST API documentation:
    //
    //      https://docs.github.com/en/rest/reference/packages
    //
    // Unfortunately, this looks like it's only partially implemented.  We need
    // to be able to list packages, make them public, and delete them.  Only
    // delete appears to be supported right now.

    /// <summary>
    /// Implments GitHub packages operations.
    /// </summary>
    public class GitHubPackageApi
    {
        private const string githubRestUri = "https://api.github.com/";

        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal GitHubPackageApi()
        {
        }

        /// <summary>
        /// Lists the packages for an organization.
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="pattern">The matching pattern (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        /// <returns>The list of package information as a list of <see cref="GitHubPackage"/> instance.</returns>
        public List<GitHubPackage> List(string organization, string pattern = null, string packageType = GitHubPackageType.Container)
        {
            GitHub.EnsureCredentials();

            // $todo(marcusbooyah): This will need to be screen-scraped.

            throw new NotImplementedException();
        }

        /// <summary>
        /// Deletes a specific named package or the packages that match a file pattern using
        /// <b>"*"</b> and <b>"?"</b> wildcards (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="nameOrPattern">The package name or matching pattern (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        public void Delete(string organization, string nameOrPattern, string packageType = GitHubPackageType.Container)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(organization), nameof(organization));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrPattern), nameof(nameOrPattern));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(packageType), nameof(packageType));

            GitHub.EnsureCredentials();

            // $todo(marcusbooyah): GitHub has a REST API for this:
            //
            //      https://docs.github.com/en/rest/reference/packages#delete-a-package-for-an-organization
            //
            // The examples don't show this but it looks like you'll just add the GITHUB_PAT in the
            // [Authentication] header as a bearer token.

            throw new NotImplementedException();
        }

        /// <summary>
        /// Makes public a specific named package or the packages that match a file pattern using
        /// <b>"*"</b> and <b>"?"</b> wildcards (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="nameOrPattern">The package name or matching pattern.</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        public void MakePublic(string organization, string nameOrPattern, string packageType = GitHubPackageType.Container)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(organization), nameof(organization));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrPattern), nameof(nameOrPattern));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(packageType), nameof(packageType));

            GitHub.EnsureCredentials();

            // $todo(marcusbooyah): This will need to be screen-scraped.

            throw new NotImplementedException();
        }
    }
}
