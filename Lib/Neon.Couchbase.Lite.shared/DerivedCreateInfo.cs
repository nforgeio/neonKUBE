//-----------------------------------------------------------------------------
// FILE:	    DerivedCreateInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.DynamicData;

using Couchbase.Lite.Store;

namespace Couchbase.Lite
{
    /// <summary>
    /// Holds information required to create a derived document.
    /// </summary>
    internal class DerivedCreateInfo
    {
        /// <summary>
        /// The document class' attached creator function.
        /// </summary>
        public Func<Document, IEntityDocument> AttachedCreator;

        /// <summary>
        /// The document class's detached creator function.
        /// </summary>
        public Func<IDictionary<string, object>, EntityDatabase, Revision, IEntityDocument> DetachedCreator;

        /// <summary>
        /// The case sensitive set with the names for the document 
        /// attachments we'll be tracking.
        /// </summary>
        public HashSet<string> AttachmentNames;
    }

}
