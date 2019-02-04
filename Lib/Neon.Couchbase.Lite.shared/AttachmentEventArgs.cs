//-----------------------------------------------------------------------------
// FILE:	    AttachmentEventArgs.cs
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
    /// Holds important details about a document attachment when the
    /// <see cref="EntityDocument{TEntity}.AttachmentEvent"/> event
    /// is raised.
    /// </summary>
    public class AttachmentEventArgs : EventArgs
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal AttachmentEventArgs()
        {
        }

        /// <summary>
        /// The attachment name.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// <para>
        /// Returns the path to the attachment contents in the local file system.
        /// </para>
        /// <note>
        /// For persisted attachments, this will reference the attachment in the
        /// Couchbase Lite database.  For unsaved attachments, this will reference
        /// a temporary file and for deleted attachments, this will return <c>null</c>.
        /// </note>
        /// </summary>
        public string Path { get; internal set; }

        /// <summary>
        /// Set to <c>true</c> if an <see cref="INotifyPropertyChanged"/> event should
        /// be raised by the binder in response to this event.  (Defaults to <c>true</c>).
        /// </summary>
        public bool Notify { get; internal set; } = true;
    }
}
