//-----------------------------------------------------------------------------
// FILE:	    NeonPropertyNames.cs
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.DynamicData;

namespace Couchbase.Lite
{
    /// <summary>
    /// Defines the property names used by the Neon Couchbase Lite document
    /// extensions.
    /// </summary>
    /// <remarks>
    /// This class defines the property names used for <see cref="EntityDocument{TEntity}"/>
    /// related values.  By default, these names all have leading plus signs (<b>+</b>) and
    /// are abbreviated to help reduce the network, memory and disk footprint of documents.
    /// </remarks>
    public static class NeonPropertyNames
    {
        // WARNING:
        //
        // Ensure that the property name definitions below do not conflict with
        // the Entity property names

            /// <summary>
        /// <b>Content</b> property name.  Defaults to <b>"+c"</b>.
        /// </summary>
        public const string Content = "+c";

        /// <summary>
        /// <b>Channels</b> property name.  Defaults to <b>"+ch"</b>.
        /// </summary>
        public const string Channels = "+ch";

        /// <summary>
        /// <b>Type</b> property name.  Defaults to <b>"+t"</b>.
        /// </summary>
        public const string Type = "+t";

        /// <summary>
        /// <b>Timestamp</b> property name.  Defaults to <b>"+ts"</b>.
        /// </summary>
        public const string Timestamp = "+ts";
    }
}
