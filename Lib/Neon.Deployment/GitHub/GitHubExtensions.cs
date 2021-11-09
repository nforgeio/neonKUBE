//-----------------------------------------------------------------------------
// FILE:	    GitHubExtensions.cs
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
    /// Internal GitHub API related extension methods.
    /// </summary>
    internal static class GitHubExtensions
    {
        /// <summary>
        /// Executes a GET request on client, handling GitHub REST API pagination by collecting all of the
        /// <c>dynamic</c> result item values into the list returned.
        /// </summary>
        /// <param name="client">A <see cref="JsonClient"/> returned by <see cref="GitHub.CreateJsonClient"/>.</param>
        /// <param name="uri">The relative request URI.</param>
        /// <param name="maxItemsPerPage">Optionally overrides the default maximum items per page to be returned by GitHub.  This defaults to 100.</param>
        /// <returns>The list of items returned by the request.</returns>
        public static async Task<IReadOnlyList<dynamic>> GetPaginatedAsync(this JsonClient client, string uri, int maxItemsPerPage = 100)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri), nameof(uri));
            Covenant.Requires<ArgumentException>(maxItemsPerPage >= 1, nameof(maxItemsPerPage));

            // Append the [per_page] parameter to the URI query string.

            if (!uri.Contains('?'))
            {
                uri += '?';
            }

            if (uri.Last() != '?')
            {
                uri += '&';
            }

            uri += $"per_page={maxItemsPerPage}";

            // Here's the documentation describing how GitHub API paging works:
            //
            //      https://docs.github.com/en/rest/guides/traversing-with-pagination
            //
            // Our approach here is simply to follow the [rel=next] links in the response's
            // [Link] header until there is no [rel=next] link, indicating the last response.
            //
            // The link header will look something like this:
            //
            // Link: <https://api.github.com/search/code?q=addClass+user%3Amozilla&page=15>; rel="next",
            //       <https://api.github.com/search/code?q=addClass+user%3Amozilla&page=34>; rel="last",
            //       <https://api.github.com/search/code?q=addClass+user%3Amozilla&page=1>; rel="first",
            //       <https://api.github.com/search/code?q=addClass+user%3Amozilla&page=13>; rel="prev"
            //
            // We're going to look for the [next] link.  If there is one, we'll extract the URI and
            // use it to fetch the next page.  There are no more pages when there's no [next] link.
            //
            // NOTE: Requests that can be serviced with a single request WILL NOT include a
            //       [Link] header.

            var items = new List<dynamic>();

            while (true)
            {
                var response    = await client.GetAsync(uri);
                var nextPageUri = (string)null;

                if (response.HttpResponse.Headers.TryGetValues("Link", out var values))
                {
                    var value = values.First();
                    var links = value.Split(',');

                    foreach (var link in links)
                    {
                        if (link.Contains("rel=\"next\""))
                        {
                            var pStart = link.IndexOf('<') + 1;
                            var pEnd   = link.IndexOf('>');

                            nextPageUri = link.Substring(pStart, pEnd - pStart);
                            break;
                        }
                    }
                }

                foreach (var item in response.AsDynamic())
                {
                    items.Add(item);
                }

                if (nextPageUri == null)
                {
                    break;
                }

                uri = nextPageUri;
            }

            return items.AsReadOnly();
        }
    }
}
