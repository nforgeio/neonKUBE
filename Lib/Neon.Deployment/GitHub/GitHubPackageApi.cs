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
using System.Threading.Tasks;
using System.Web;

using Neon.Common;
using Neon.Net;

using AngleSharp;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;
using AngleSharp.Dom;
using System.Text.RegularExpressions;

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

        private JsonClient jsonClient;

        private IBrowsingContext browsingContext;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal GitHubPackageApi()
        {
            GitHub.GetCredentials();
            GitHub.EnsureCredentials();

            // Create json client for interacting with the GitHub API.
            jsonClient = new JsonClient()
            {
                BaseAddress = new Uri(githubRestUri),
            };

            jsonClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", GitHub.PersonalAccessToken);
            jsonClient.DefaultRequestHeaders.Add("User-Agent", "neondevbot");

            // Create an AngleSharp client for screen scraping.
            
            // Load default configuration
            var config = Configuration.Default.WithDefaultLoader().WithDefaultCookies();
            
            // Create a new browsing context
            browsingContext = BrowsingContext.New(config);

            Login();
        }

        /// <summary>
        /// Logs in to GitHub.
        /// </summary>
        /// <returns></returns>
        public void Login()
        {
            var document = browsingContext.OpenAsync("https://github.com/login").Result;
            var form = browsingContext.Active.QuerySelector<IHtmlFormElement>("#login > div.auth-form-body.mt-3 > form");
            var resultDocument = form.SubmitAsync(new { login = GitHub.UserCredentials.Username, password = GitHub.UserCredentials.Password }).Result;

            if (resultDocument.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception("Login failed");
            }
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
            string organization, 
            string pattern                     = null,
            GitHubPackageType packageType      = GitHubPackageType.Container,
            GitHubPackageVisibility visibility = GitHubPackageVisibility.All)
        {
            var packages = new List<GitHubPackage>();

            var document = await browsingContext.OpenAsync($"https://github.com/orgs/{organization}/packages?type={packageType}&q={pattern}&visibility={visibility}");

            var packageslist = document.QuerySelector<IHtmlUnorderedListElement>("#org-packages > div.Box > ul");

            if (packageslist == null || packageslist.Children.Count() == 0)
            {
                return packages;
            }

            Regex regex = null;

            if (!string.IsNullOrEmpty(pattern))
            {
                regex = NeonHelper.FileWildcardRegex(pattern);
            }

            var done = false;

            while (!done)
            {
                foreach (var p in packageslist.Children)
                {
                    var name = p.QuerySelector<IHtmlAnchorElement>("div > div.flex-auto > a").Title;

                    if (regex == null || regex.IsMatch(name))
                    {
                        packages.Add(
                        new GitHubPackage()
                        {
                            Name = name,
                            Type = packageType
                        });
                    }
                }

                var next = document.QuerySelector<IHtmlAnchorElement>("#org-packages > div.paginate-container > div > a.next_page");

                if (next != null
                    && !next.ClassList.Contains("disabled"))
                {
                    document = await next.NavigateAsync();

                    packageslist = document.QuerySelector<IHtmlUnorderedListElement>("#org-packages > div.Box > ul");
                }
                else
                {
                    done = true;
                }
            }

            return packages;
        }

        /// <summary>
        /// Deletes a specific named package or the packages that match a file pattern using
        /// <b>"*"</b> and <b>"?"</b> wildcards (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).
        /// </summary>
        /// <param name="organization">The GitHub organization name.</param>
        /// <param name="nameOrPattern">The package name or matching pattern (see <see cref="NeonHelper.FileWildcardRegex(string)"/>).</param>
        /// <param name="packageType">Optionally specifies the package type.  This defaults to <see cref="GitHubPackageType.Container"/>.</param>
        public async Task DeleteAsync(
            string organization, 
            string nameOrPattern,
            GitHubPackageType packageType = GitHubPackageType.Container)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(organization), nameof(organization));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrPattern), nameof(nameOrPattern));

            GitHub.EnsureCredentials();

            var packages = await ListAsync(organization, nameOrPattern, packageType);

            foreach (var p in packages)
            {
                await jsonClient.DeleteAsync($"/orgs/{organization}/packages/{packageType}/{p.Name}");
            }
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
            string organization,
            string nameOrPattern,
            GitHubPackageVisibility visibility = GitHubPackageVisibility.All,
            GitHubPackageType packageType = GitHubPackageType.Container)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(organization), nameof(organization));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrPattern), nameof(nameOrPattern));

            GitHub.EnsureCredentials();

            var page = await browsingContext.OpenAsync($"https://github.com/orgs/{organization}/packages/container/{nameOrPattern}/settings");

            var form = page.QuerySelector<IHtmlFormElement>("#js-pjax-container > div > div.container-lg.p-responsive.clearfix > div.container-lg.d-flex > div.col-9 > div.Box.Box--danger > ul > li:nth-child(1) > div > details > details-dialog > div.Box-body.overflow-auto > form");

            var selector = form.QuerySelector<IHtmlInputElement>("#visibility_public");
            selector.IsChecked = visibility == GitHubPackageVisibility.Public;

            selector = form.QuerySelector<IHtmlInputElement>("#visibility_private");
            selector.IsChecked = visibility == GitHubPackageVisibility.Private;

            selector = form.QuerySelector<IHtmlInputElement>("#visibility_internal");
            selector.IsChecked = visibility == GitHubPackageVisibility.Internal;

            var resultDocument = await form.SubmitAsync(new { verify = nameOrPattern });

            if (resultDocument.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"Failed to make package {visibility}.");
            }
        }
    }
}
