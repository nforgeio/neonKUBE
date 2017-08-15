//-----------------------------------------------------------------------------
// FILE:	    WebHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;

using Neon.Common;
using Neon.Diagnostics;

namespace Neon.AspNetCore
{
    /// <summary>
    /// Utility methods for <b>AspNetCore</b> applications.
    /// </summary>
    public static class WebHelper
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
    }
}
