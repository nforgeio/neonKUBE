//-----------------------------------------------------------------------------
// FILE:	    Extensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by neonFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with neonFORGE, LLC.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Reflection;

using Neon.Common;
using Neon.Deployment;

namespace Neon.Deployment
{
    /// <summary>
    /// Handy deployment related extension methods.
    /// </summary>
    public static class Extensions
    {
        //---------------------------------------------------------------------
        // IProfileClient extensions

        /// <summary>
        /// <para>
        /// Retrieves the AWS-CLI NEON_OP_AWS_ACCESS_KEY_ID and NEON_OP_AWS_SECRET_ACCESS_KEY
        /// credentials from 1Password and sets these enviroment variables:
        /// </para>
        /// <list type="bullet">
        ///     <item><c>AWS_ACCESS_KEY_ID</c></item>
        ///     <item><c>AWS_SECRET_ACCESS_KEY</c></item>
        /// </list>
        /// </summary>
        public static void GetAwsCredentials(this IProfileClient profileClient)
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", profileClient.GetSecretPassword("NEON_OP_AWS_ACCESS_KEY_ID"));
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", profileClient.GetSecretPassword("NEON_OP_AWS_SECRET_ACCESS_KEY"));
        }

        /// <summary>
        /// <para>
        /// Removes the AWS-CLI credential environment variables if present:
        /// </para>
        /// <list type="bullet">
        ///     <item><c>AWS_ACCESS_KEY_ID</c></item>
        ///     <item><c>AWS_SECRET_ACCESS_KEY</c></item>
        /// </list>
        /// </summary>
        public static void ClearAwsCredentials(this IProfileClient profileClient)
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);
        }
    }
}
