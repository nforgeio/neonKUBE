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
    // We'll replace this once GitHub as a fully functional REST API for this.

    /// <summary>
    /// Implments GitHub packages operations.
    /// </summary>
    public class GitHubPackageApi
    {
        internal GitHubPackageApi()
        {
            GitHub.GetCredentials();
            GitHub.EnsureCredentials();
        }

        /// <summary>
        /// Lists the packages for an organization.
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="pattern">The matching pattern (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        /// <param name="visibility">Optionally specifies the visibility of the package.  This defaults to <see cref="GitHubPackageVisibility.All"/></param>
        /// <returns>The list of package information as a list of <see cref="GitHubPackage"/> instance.</returns>
        public List<GitHubPackage> List(
            string                  organization,
            string                  pattern     = null,
            GitHubPackageType       packageType = GitHubPackageType.Container,
            GitHubPackageVisibility visibility  = GitHubPackageVisibility.All)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Lists the packages for an organization.
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="pattern">The matching pattern (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        /// <param name="visibility">Optionally specifies the visibility of the package.  This defaults to <see cref="GitHubPackageVisibility.All"/></param>
        /// <returns>The list of package information as a list of <see cref="GitHubPackage"/> instance.</returns>
        public async Task<List<GitHubPackage>> ListAsync(
            string                  organization,
            string                  pattern     = null,
            GitHubPackageType       packageType = GitHubPackageType.Container,
            GitHubPackageVisibility visibility  = GitHubPackageVisibility.All)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            GitHubPackageVisibility visibility  = GitHubPackageVisibility.All,
            GitHubPackageType       packageType = GitHubPackageType.Container)
        {
            SetVisibilityAsync(organization, nameOrPattern, visibility, packageType).Wait();
        }

        /// <summary>
        /// Makes public a specific named package or the packages that match a file pattern using
        /// <b>"*"</b> and <b>"?"</b> wildcards (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="nameOrPattern">The package name or matching pattern.</param>
        /// <param name="visibility">The visibility to set the package to.</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        public async Task SetVisibilityAsync(
            string                  organization,
            string                  nameOrPattern,
            GitHubPackageVisibility visibility = GitHubPackageVisibility.All,
            GitHubPackageType       packageType = GitHubPackageType.Container)
        {
            throw new NotImplementedException();
        }
    }
}
