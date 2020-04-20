//-----------------------------------------------------------------------------
// FILE:	    NamespaceListPage.cs
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
using System.ComponentModel;

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Holds a page of namespace information listed from Temporal.
    /// </summary>
    public class NamespaceListPage
    {
        /// <summary>
        /// Lists the namespace information.
        /// </summary>
        public List<NamespaceDescription> Namespaces { get; set; }

        /// <summary>
        /// Indicates that there's at least one more page of namespace information
        /// to be returned from Temporal when this is not <c>null</c>.  Otherwise,
        /// this is an opaque token that may be passed to <see cref="TemporalClient.ListNamespacesAsync(int, byte[])"/>
        /// to retrieve the next page of namespace information.
        /// </summary>
        public byte[] NextPageToken { get; set; }
    }
}
