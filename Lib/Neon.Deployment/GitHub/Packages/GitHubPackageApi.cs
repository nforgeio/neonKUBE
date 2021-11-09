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
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

using Neon.Common;
using Neon.Net;

namespace Neon.Deployment
{
    /// <summary>
    /// Implements GitHub Packages operations.
    /// </summary>
    public class GitHubPackageApi
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public GitHubPackageApi()
        {
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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(organization), nameof(organization));

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

            using (var client = GitHub.CreateJsonClient())
            {
                nameOrPattern = nameOrPattern ?? "*";

                var regex    = NeonHelper.FileWildcardRegex(nameOrPattern);
                var packages = new List<GitHubPackage>();
                var response = (await client.GetAsync($"/orgs/{organization}/packages?package_type={NeonHelper.EnumToString(packageType)}")).AsDynamic();

                foreach (var rawPackage in response)
                {
                    string packageName = rawPackage.name;

                    if (regex.IsMatch(packageName))
                    {
                        var package = new GitHubPackage()
                        {
                            Name       = packageName,
                            Type       = NeonHelper.ParseEnum<GitHubPackageType>((string)rawPackage.package_type),
                            Visibility = NeonHelper.ParseEnum<GitHubPackageVisibility>((string)rawPackage.visibility)
                        };

                        if (visibility == GitHubPackageVisibility.All || package.Visibility == visibility)
                        {
                            packages.Add(package);
                        }
                    }
                }

                return packages;
            }
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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(organization), nameof(organization));

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

            using (var client = GitHub.CreateJsonClient())
            {
                var packages = await ListAsync(organization, nameOrPattern, packageType);

                foreach (var package in packages)
                {
                    await client.DeleteAsync($"/orgs/{organization}/packages/{packageType}/{package.Name}");
                }
            }
        }

        /// <summary>
        /// <para>
        /// Makes public a specific named package or the packages that match a file pattern using
        /// <b>"*"</b> and <b>"?"</b> wildcards (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).
        /// </para>
        /// <note>
        /// This is not currently implemented due to the lack of a proper GitHub REST API.
        /// </note>
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="nameOrPattern">The package name or matching pattern.</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        /// <param name="visibility">The new package visibility.</param>
        /// <exception cref="NotImplementedException">Currently thrown always.</exception>
        public void SetVisibility(
            string                  organization,
            string                  nameOrPattern,
            GitHubPackageType       packageType = GitHubPackageType.Container,
            GitHubPackageVisibility visibility  = GitHubPackageVisibility.All)
        {
            SetVisibilityAsync(organization, nameOrPattern, packageType, visibility).Wait();
        }

        /// <summary>
        /// <para>
        /// Makes public a specific named package or the packages that match a file pattern using
        /// <b>"*"</b> and <b>"?"</b> wildcards (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).
        /// </para>
        /// <note>
        /// This is not currently implemented due to the lack of a proper GitHub REST API.
        /// </note>
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="nameOrPattern">The package name or matching pattern.</param>
        /// <param name="visibility">The visibility to set the package to.</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        /// <exception cref="NotImplementedException">Currently thrown always.</exception>
        public async Task SetVisibilityAsync(
            string                  organization,
            string                  nameOrPattern,
            GitHubPackageType       packageType = GitHubPackageType.Container,
            GitHubPackageVisibility visibility  = GitHubPackageVisibility.All)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(organization), nameof(organization));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrPattern), nameof(nameOrPattern));

            await Task.CompletedTask;
            throw new NotImplementedException($"[{nameof(SetVisibilityAsync)}()] is not implemented due to the lack of a proper GitHub REST API.");
        }
    }
}
