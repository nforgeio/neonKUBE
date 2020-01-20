//-----------------------------------------------------------------------------
// FILE:	    Wildcard.cs
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
