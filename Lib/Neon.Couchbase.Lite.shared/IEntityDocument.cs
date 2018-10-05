//-----------------------------------------------------------------------------
// FILE:	    IEntityDocument.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
