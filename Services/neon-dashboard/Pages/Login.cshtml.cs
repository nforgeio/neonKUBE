//-----------------------------------------------------------------------------
// FILE:        Login.cshtml.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Tasks;

namespace NeonDashboard.Pages
{
    /// <summary>
    /// Handles login.
    /// </summary>
    public class LoginModel : PageModel
    {
        private Service           neonDashboardService;
        private ILogger           logger;
        private IDistributedCache cache;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="neonDashboardService"></param>
        /// <param name="logger"></param>
        /// <param name="cache"></param>
        public LoginModel(
            Service                 neonDashboardService,
            ILogger                 logger,
            IDistributedCache       cache)
        {
            this.neonDashboardService = neonDashboardService;
            this.logger               = logger;
            this.cache                = cache;
        }

        /// <summary>
        /// Forwards the client to the upstream provider.
        /// </summary>
        /// <param name="redirectUri"></param>
        /// <returns></returns>
        public async Task OnGet(string redirectUri)
        {
            await HttpContext.ChallengeAsync(
                OpenIdConnectDefaults.AuthenticationScheme, 
                new AuthenticationProperties 
                { 
                    RedirectUri = redirectUri,
                });
        }
    }
}
