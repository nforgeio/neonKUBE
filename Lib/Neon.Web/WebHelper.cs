//-----------------------------------------------------------------------------
// FILE:	    WebHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;

namespace Neon.Web
{
    /// <summary>
    /// Utility methods for <b>AspNetCore</b> applications.
    /// </summary>
    public static partial class WebHelper
    {
        /// <summary>
        /// Performs common web app initialization including setting the correct
        /// working directory.  Call this method from your application's main
        /// entrypoint method.
        /// </summary>
        public static void Initialize()
        {
            // AspNetCore expects the current working directory to be where
            // the main executable is located.

            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        }

        /// <summary>
        /// Generates an opaque globally unique activity ID.
        /// </summary>
        /// <returns>The activity ID string.</returns>
        public static string GenerateActivityId()
        {
            return NeonHelper.UrlTokenEncode(Guid.NewGuid().ToByteArray());
        }
    }
}
