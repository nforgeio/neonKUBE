//-----------------------------------------------------------------------------
// FILE:	    IEntityDocument.cs
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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.DynamicData;

namespace Couchbase.Lite
{
    /// <summary>
    /// Defines some low-level attributes of a <see cref="EntityDocument{TEntity}"/>.
    /// </summary>
    public interface IEntityDocument : IDynamicDocument
    {
        /// <summary>
        /// Returns the document content entity <see cref="Type"/>.  This information can
        /// be useful when implementing a global <see cref="CustomConflictPolicy"/> that
        /// handles conflicts for more than one type of application document.
        /// </summary>
        Type EntityType { get; }

        /// <summary>
        /// Returns the document properties.
        /// </summary>
        IDictionary<string, object> Properties { get; }

        /// <summary>
        /// Returns the document revision.
        /// </summary>
        Revision Revision { get; }

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Used to initialize the set of attachment names derived
        /// document classes will track via their <see cref="EntityDocument{TEntity}.AttachmentEvent"/>.
        /// </summary>
        /// <param name="attachmentNames">The case insenstive set of attachment names.</param>
        void SetAttachmentTracking(HashSet<string> attachmentNames);
    }
}
