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
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

using Neon.Common;
using Neon.Net;

namespace Neon.Deployment
{
    //-------------------------------------------------------------------------
    // $hack(jefflill):
    //
    // This API is actually implemented with neon-assistant right now due to
    // problems loading the AngleSharp assembly into Powershell.  We'll be 
    // sending commands via the profile client.
    //
    // The operation to performed will be specified as the [operation] argument.
    // These are the supported operations (corresponding to the methods below):
    //
    // Package-List(pat, username, password, organization, nameOrPattern, packageType, visibility)
    //
    //      Lists packages returning a JSON array of property objects
    //
    // Package-Delete(pat, username, password, organization, nameOrPattern, packageType)
    //
    //      Deletes packages
    //
    // Package-SetVisibility(pat, username, password, organization, nameOrPattern, packageType, visibility)
    //
    //      Changes the visibility of packages
    //
    // NOTE: We pass the GITHUB PAT as well as the username/password credentials
    //       along with each call.
    //
    // We'll replace this hack once GitHub as a fully functional REST API for this.

    /// <summary>
    /// Implements GitHub Packages operations.
    /// </summary>
    public class GitHubPackageApi
    {
        private ProfileClient   profileClient = new ProfileClient();

        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal GitHubPackageApi()
        {
            GitHub.GetCredentials();
            GitHub.EnsureCredentials();
        }

        /// <summary>
        /// Lists the packages for an organization.
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="nameOrPattern">The matching pattern (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        /// <param name="visibility">Optionally specifies the visibility of the package.  This defaults to <see cref="GitHubPackageVisibility.All"/></param>
        /// <returns>The list of package information as a list of <see cref="GitHubPackage"/> instance.</returns>
        public List<GitHubPackage> List(
            string                  organization,
            string                  nameOrPattern = null,
            GitHubPackageType       packageType   = GitHubPackageType.Container,
            GitHubPackageVisibility visibility    = GitHubPackageVisibility.All)
        {
            return ListAsync(organization, nameOrPattern, packageType, visibility).Result;
        }

        /// <summary>
        /// Lists the packages for an organization.
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="nameOrPattern">The matching pattern (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        /// <param name="visibility">Optionally specifies the visibility of the package.  This defaults to <see cref="GitHubPackageVisibility.All"/></param>
        /// <returns>The list of package information as a list of <see cref="GitHubPackage"/> instance.</returns>
        public async Task<List<GitHubPackage>> ListAsync(
            string                  organization,
            string                  nameOrPattern = null,
            GitHubPackageType       packageType   = GitHubPackageType.Container,
            GitHubPackageVisibility visibility    = GitHubPackageVisibility.All)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(organization), nameof(organization));

            var args = new Dictionary<string, string>();

            args["operation"]     = "Package-List";
            args["pat"]           = GitHub.AccessToken;
            args["username"]      = GitHub.Credentials.Username;
            args["password"]      = GitHub.Credentials.Password;
            args["organization"]  = organization;
            args["nameOrPattern"] = nameOrPattern ?? "*";
            args["packageType"]   = packageType.ToMemberString();
            args["visibility"]    = visibility.ToMemberString();

            var result = profileClient.Call(args);

            return await Task.FromResult(NeonHelper.JsonDeserialize<List<GitHubPackage>>(result));
        }

        /// <summary>
        /// Deletes a specific named package or the packages that match a file pattern using
        /// <b>"*"</b> and <b>"?"</b> wildcards (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="nameOrPattern">The package name or matching pattern (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        public void Delete(
            string              organization,
            string              nameOrPattern,
            GitHubPackageType   packageType = GitHubPackageType.Container)
        {
            DeleteAsync(organization, nameOrPattern, packageType).Wait();
        }

        /// <summary>
        /// Deletes a specific named package or the packages that match a file pattern using
        /// <b>"*"</b> and <b>"?"</b> wildcards (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="nameOrPattern">The package name or matching pattern (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        public async Task DeleteAsync(
            string              organization,
            string              nameOrPattern,
            GitHubPackageType   packageType = GitHubPackageType.Container)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(organization), nameof(organization));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrPattern), nameof(nameOrPattern));

            var args = new Dictionary<string, string>();

            args["operation"]     = "Package-Delete";
            args["pat"]           = GitHub.AccessToken;
            args["username"]      = GitHub.Credentials.Username;
            args["password"]      = GitHub.Credentials.Password;
            args["organization"]  = organization;
            args["nameOrPattern"] = nameOrPattern ?? "*";
            args["packageType"]   = packageType.ToMemberString();

            profileClient.Call(args);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Makes public a specific named package or the packages that match a file pattern using
        /// <b>"*"</b> and <b>"?"</b> wildcards (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="nameOrPattern">The package name or matching pattern.</param>
        /// <param name="visibility">The new package visibility.</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        public void SetVisibility(
            string                  organization,
            string                  nameOrPattern,
            GitHubPackageType       packageType = GitHubPackageType.Container,
            GitHubPackageVisibility visibility  = GitHubPackageVisibility.All)
        {
            SetVisibilityAsync(organization, nameOrPattern, packageType, visibility).Wait();
        }

        /// <summary>
        /// Makes public a specific named package or the packages that match a file pattern using
        /// <b>"*"</b> and <b>"?"</b> wildcards (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="nameOrPattern">The package name or matching pattern.</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        /// <param name="visibility">The visibility to set the package to.</param>
        public async Task SetVisibilityAsync(
            string                  organization,
            string                  nameOrPattern,
            GitHubPackageType       packageType = GitHubPackageType.Container,
            GitHubPackageVisibility visibility  = GitHubPackageVisibility.All)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(organization), nameof(organization));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrPattern), nameof(nameOrPattern));

            var args = new Dictionary<string, string>();

            args["operation"]     = "Package-SetVisibility";
            args["pat"]           = GitHub.AccessToken;
            args["username"]      = GitHub.Credentials.Username;
            args["password"]      = GitHub.Credentials.Password;
            args["organization"]  = organization;
            args["nameOrPattern"] = nameOrPattern ?? "*";
            args["packageType"]   = packageType.ToMemberString();
            args["visibility"]    = visibility.ToMemberString();

            profileClient.Call(args);

            await Task.CompletedTask;
        }
    }
}
