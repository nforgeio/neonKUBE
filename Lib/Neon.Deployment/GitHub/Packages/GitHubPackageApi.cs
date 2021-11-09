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

// $todo(jefflill):
//
// OctoKit.net doesn't include a packages API at this time so we need to use low-level
// HTTP calls and parse the results ourselves, including handling pagination.  Let's
// revisit this periodically to check for OctoKit updates and upgrade this code as well.

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
        /// <param name="listVersions">Optionally queries for the package versions as well.  This defaults to <c>false</c>.</param>
        /// <returns>The list of package information as a list of <see cref="GitHubPackage"/> instance.</returns>
        public async Task<List<GitHubPackage>> ListAsync(
            string                  organization,
            string                  nameOrPattern = null,
            GitHubPackageType       packageType   = GitHubPackageType.Container,
            GitHubPackageVisibility visibility    = GitHubPackageVisibility.All,
            bool                    listVersions  = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(organization), nameof(organization));

            using (var client = GitHub.CreateJsonClient())
            {
                nameOrPattern = nameOrPattern ?? "*";

                // List the packages that match the name and visibility parameters.

                var regex       = NeonHelper.FileWildcardRegex(nameOrPattern);
                var packages    = new List<GitHubPackage>();
                var type        = NeonHelper.EnumToString(packageType);
                var rawPackages = await client.GetPaginatedAsync($"/orgs/{organization}/packages?package_type={type}");

                foreach (var rawPackage in rawPackages)
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

                // Fetch the package versions when requested.

                // $todo(jefflill):
                //
                // We should be using [Async.ParallelForEachAsync()) to speed this up. 

                if (listVersions)
                {
                    foreach (var package in packages)
                    {
                        var rawPackageVersions = await client.GetPaginatedAsync($"/orgs/{organization}/packages/{type}/{package.Name}/versions");

                        foreach (var rawVersion in rawPackageVersions)
                        {
                            // $hack(jefflill):
                            //
                            // We're special-casing container images by parsing the image
                            // tags from the package metadata.  We're also going to ignore
                            // versions without any tags for now.

                            var packageVersion = new GitHubPackageVersion()
                            {
                                Id   = (string)rawVersion.id,
                                Name = (string)rawVersion.name
                            };

                            if (packageType == GitHubPackageType.Container)
                            {
                                foreach (var rawTag in rawVersion.metadata.container.tags)
                                {
                                    packageVersion.Tags.Add((string)rawTag);
                                }
                            }

                            if (packageVersion.Tags.Count > 0)
                            {
                                package.Versions.Add(packageVersion);
                            }
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
