//-----------------------------------------------------------------------------
// FILE:	    LoggingExceptionFilter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Threading;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;

namespace Neon.Web
{
    /// <summary>
    /// Used for logging unhandled exceptions.
    /// </summary>
    internal class LoggingExceptionFilter : IExceptionFilter
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="hostingEnvironment">Specifies the hosting environment.</param>
        /// <param name="modelMetadataProvider">Specifies the model metadata provider.</param>
        public LoggingExceptionFilter(IWebHostEnvironment hostingEnvironment, IModelMetadataProvider modelMetadataProvider)
        {
        }

        /// <inheritdoc/>
        public void OnException(ExceptionContext context)
        {
            var controllerName = context.RouteData.Values["controller"];
            var log            = LogManager.Default.GetLogger("Web-" + controllerName);

            log.LogError(context.Exception);
        }
    }
}
