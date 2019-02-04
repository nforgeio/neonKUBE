//-----------------------------------------------------------------------------
// FILE:	    RevisionExtensions.cs
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
    /// Extensions methods for <see cref="Revision"/>, <see cref="SavedRevision"/>, and <see cref="UnsavedRevision"/>.
    /// </summary>
    public static class RevisionExtensions
    {
        /// <summary>
        /// Wraps the revision's properties and attachments with a <see cref="EntityDocument{TEntity}"/>.
        /// </summary>
        /// <typeparam name="TEntity">The document content type.</typeparam>
        /// <param name="revision">The revision.</param>
        /// <returns>The entity document.</returns>
        public static EntityDocument<TEntity> ToEntityDocument<TEntity>(this Revision revision)
            where TEntity : class, IDynamicEntity, new()
        {
            Covenant.Requires<ArgumentNullException>(revision != null);

            return new EntityDocument<TEntity>(Stub.Param, revision.Properties, EntityDatabase.From(revision.Database), revision);
        }

        /// <summary>
        /// Replaces the current revision's attachments with copies of the
        /// attachments from another revision.
        /// </summary>
        /// <param name="revision">The revision.</param>
        /// <param name="sourceRevision">The revision with the attachments to be copied.</param>
        public static void ReplaceAttachmentsFrom(this UnsavedRevision revision, Revision sourceRevision)
        {
            Covenant.Requires<ArgumentNullException>(revision != null);
            Covenant.Requires<ArgumentNullException>(sourceRevision != null);

            // Remove the current attachments.

            foreach (var name in revision.AttachmentNames.ToArray())
            {
                revision.RemoveAttachment(name);
            }

            // Copy the source attachments.

            foreach (var sourceAttachment in sourceRevision.Attachments)
            {
                // $todo(jeff.lill):
                //
                // I'm a little nervous about this call.  [Attachment.Content] is going to 
                // load the attachment into memory, which could be a significant overhead.
                // It's possible to pass a stream, but looking at the Couchbase Lite source,
                // it appears that the new attachment would try to take ownership of the
                // source attachment's stream (if I passed it), probably ending badly.

                revision.SetAttachment(sourceAttachment.Name, sourceAttachment.ContentType, sourceAttachment.Content);
            }
        }

        /// <summary>
        /// Merges the attachments from another revision.
        /// </summary>
        /// <param name="revision">The revision.</param>
        /// <param name="sourceRevision">The revision with the attachments to be merged.</param>
        /// <param name="keepMine">Controls what happens when the source and target have the same attachment (see remarks).</param>
        /// <remarks>
        /// <para>
        /// The <paramref name="keepMine"/> parameter determines what happens when the source and target
        /// revisions has an attachment with the same name.  When <paramref name="keepMine"/>=<c>true</c>
        /// (the default), then matching attachments from the source revision will be ignored.
        /// </para>
        /// <para>
        /// When <paramref name="keepMine"/>=<c>false</c>, matching attachments from the source
        /// revision will be copied to the current revision, overwriting the existion attachment.
        /// </para>
        /// </remarks>
        public static void MergeAttachmentsFrom(this UnsavedRevision revision, Revision sourceRevision, bool keepMine = true)
        {
            Covenant.Requires<ArgumentNullException>(revision != null);
            Covenant.Requires<ArgumentNullException>(sourceRevision != null);

            // Merge the source attachments.

            foreach (var sourceAttachment in sourceRevision.Attachments)
            {
                var existingAttachment = revision.GetAttachment(sourceAttachment.Name);

                if (existingAttachment != null && keepMine)
                {
                    continue;
                }

                // $todo(jeff.lill):
                //
                // I'm a little nervous about this call.  [Attachment.Content] is going to 
                // load the attachment into memory, which could be a significant overhead.
                // It's possible to pass a stream, but looking at the Couchbase Lite source,
                // it appears that the new attachment would try to take ownership of the
                // source attachment's stream (if I passed it), probably ending badly.

                revision.SetAttachment(sourceAttachment.Name, sourceAttachment.ContentType, sourceAttachment.Content);
            }
        }

        /// <summary>
        /// Marks the revision as deleted while optionally retaining non-reserved properties.
        /// </summary>
        /// <param name="revision">The revision.</param>
        /// <param name="keepProperties">
        /// Optionally indicates that the non-reserved properties should be retained.
        /// This defaults to <c>false</c>.
        /// </param>
        public static void Delete(this UnsavedRevision revision, bool keepProperties = false)
        {
            Covenant.Requires<ArgumentNullException>(revision != null);

            revision.IsDeletion = true;

            if (!keepProperties)
            {
                foreach (var nonReservedKey in revision.Properties.Keys.Where(k => !k.StartsWith("_")).ToArray())
                {
                    revision.Properties.Remove(nonReservedKey);
                }
            }
        }
    }
}
