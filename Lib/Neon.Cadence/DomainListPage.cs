//-----------------------------------------------------------------------------
// FILE:	    DomainListPage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.ComponentModel;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Holds a page of domain information listed from Cadence.
    /// </summary>
    public class DomainListPage
    {
        /// <summary>
        /// Lists the domain information.
        /// </summary>
        public List<DomainDescription> Domains { get; set; }

        /// <summary>
        /// Indicates that there's at least one more page of domain information
        /// to be returned from Cadence when this is not <c>null</c>.  Otherwise,
        /// this is an opaque token that may be passed to <see cref="CadenceClient.ListDomainsAsync(int, byte[])"/>
        /// to retrieve the next page of domain information.
        /// </summary>
        public byte[] NextPageToken { get; set; }
    }
}
