//-----------------------------------------------------------------------------
// FILE:	    Login.cshtml.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using Neon.Tasks;

namespace NeonDashboard.Pages
{
    public class LoginModel : PageModel
    {
        public async Task OnGet(string redirectUri)
        {
            await SyncContext.ClearAsync;

            await HttpContext.ChallengeAsync(
                "oidc", 
                new AuthenticationProperties 
                { 
                    RedirectUri = redirectUri,
                });
        }
    }
}
