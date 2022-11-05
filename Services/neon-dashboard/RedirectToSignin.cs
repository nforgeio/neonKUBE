//-----------------------------------------------------------------------------
// FILE:	    RedirectToSignin.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace NeonDashboard
{
    public class RedirectToSignin : ComponentBase
    {
        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        [Inject]
        protected IHttpContextAccessor Context { get; set; }

        protected override void OnInitialized()
        {
            if (!this.Context.HttpContext.User.Identity.IsAuthenticated)
            {
                var returnUrl = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
                NavigationManager.NavigateTo($"login?redirectUri=/{returnUrl}", forceLoad: true);
            }
        }
    }
}
