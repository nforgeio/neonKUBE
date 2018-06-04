//-----------------------------------------------------------------------------
// FILE:	    Wildcard.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Text;

namespace Neon.Cryptography
{
    /// <summary>
    /// Enumerates the possible wildcard certificate generation modes.
    /// </summary>
    public enum Wildcard
    {
        /// <summary>
        /// Do not create a wildcard certificate.
        /// </summary>
        None,

        /// <summary>
        /// Create a certificate that covers all subdomains <b>*.mydomain.com</b>.
        /// </summary>
        SubdomainsOnly,

        /// <summary>
        /// Create a certificate the covers both the root domain <b>mydomain.com</b>
        /// as well as all subdomains <b>*.mydomain.com</b>.
        /// </summary>
        RootAndSubdomains
    }
}
