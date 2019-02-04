//-----------------------------------------------------------------------------
// FILE:	    ConflictPolicy.cs
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
    /// Base class for resolving Couchbase Lite document conflicts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class has references to several built-in conflict resolution policies:
    /// <see cref="Fail"/>, <see cref="Ignore"/>, <see cref="KeepOther"/>, and
    /// <see cref="KeepThis"/>.  You may pass any of these to <see cref="EntityDocument{TEntity}.Save(ConflictPolicy)"/>
    /// or set as the <see cref="Default"/> policy.
    /// </para>
    /// <para>
    /// <see cref="Default"/> is the global default policy.  This is set to <see cref="IgnoreConflictPolicy"/>
    /// by default, which means that conflicted revisions will be saved to the database, leaving Couchbase
    /// Lite to choose the winner.  You can set this to another built-in policy or one of your own that
    /// derives from <see cref="CustomConflictPolicy"/>, implementing application specific logic.
    /// </para>
    /// <note>
    /// Applications can implement custom policies that handle multiple types by examining the
    /// <see cref="IEntityDocument.EntityType"/> property.  This can be a 
    /// powerful way to centralize conflict resolution logic in your code.
    /// </note>
    /// <para>
    /// Custom conflict policies need to implement the <see cref="Resolve(ConflictDetails)"/> method.
    /// This accepts a <see cref="ConflictDetails"/> argument including the low-level Couchbase Lite
    /// <see cref="Document"/>, the entity document as a <see cref="IEntityDocument"/> and the
    /// <see cref="UnsavedRevision"/> we're trying to save.
    /// </para>
    /// <para>
    /// The policy should do what's required to resolve the conflict and persist the change to the
    /// database.  This includes handling any additional document conflicts.  <see cref="ConflictDetails.SavedRevision"/>
    /// should be set to the revision actually saved.  Set <see cref="ConflictDetails.SavedRevision"/>
    /// to <c>null</c> when the conflict could not be resolved.
    /// </para>
    /// </remarks>
    public abstract class ConflictPolicy
    {
        //---------------------------------------------------------------------
        // Static members

        private static ConflictPolicy defaultPolicy = new IgnoreConflictPolicy();

        /// <summary>
        /// The global default conflict resolution policy.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This policy defaults to <see cref="FailConflictPolicy"/> which means that conflicted
        /// document save operations will not be allowed to complete.
        /// </para>
        /// <para>
        /// You may set this to any of the built-in polices referenced by the class
        /// or as a <see cref="CustomConflictPolicy"/> containing custom application
        /// logic.
        /// </para>
        /// </remarks>
        public static ConflictPolicy Default
        {
            get { return defaultPolicy; }

            set
            {
                Covenant.Requires<ArgumentNullException>(value != null);

                if (value != null)
                {
                    throw new ArgumentNullException("Unable to set a NULL default conflict policy.");
                }

                defaultPolicy = value;
            }
        }

        /// <summary>
        /// Returns a policy that throws a <see cref="CouchbaseLiteException"/> with the
        /// <see cref="StatusCode.Conflict"/> error when a document cannot be saved due
        /// to a conflict.
        /// </summary>
        public static FailConflictPolicy Fail { get; private set; } = new FailConflictPolicy();

        /// <summary>
        /// Returns a policy that persists conflicts to the database.
        /// </summary>
        public static IgnoreConflictPolicy Ignore { get; private set; } = new IgnoreConflictPolicy();

        /// <summary>
        /// Conflicts will be resolved by keeping the revision already in the database.
        /// </summary>
        public static KeepOtherConflictPolicy KeepOther { get; private set; } = new KeepOtherConflictPolicy();

        /// <summary>
        /// Conflicts will be resolved in favor of the revision being saved.
        /// </summary>
        public static KeepThisConflictPolicy KeepThis { get; private set; } = new KeepThisConflictPolicy();

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Returns the <see cref="ConflictPolicyType"/>.
        /// </summary>
        public abstract ConflictPolicyType Type { get; }

        /// <summary>
        /// Attempts to resolve the document conflict.
        /// </summary>
        /// <param name="details">The conflict details.</param>
        /// <exception cref="ConflictException">Thrown if the conflict could not be resolved.</exception>
        /// <remarks>
        /// <para>
        /// Custom conflict policies need to implement the <see cref="Resolve(ConflictDetails)"/> method.
        /// This accepts a <see cref="ConflictDetails"/> argument including the low-level Couchbase Lite
        /// <see cref="Document"/>, the entity document as a <see cref="IEntityDocument"/> and the
        /// <see cref="UnsavedRevision"/> we're trying to save.
        /// </para>
        /// <para>
        /// The policy should do what's required to resolve the conflict and persist the change to the
        /// database.  <see cref="ConflictDetails.SavedRevision"/> should be set to the revision
        /// actually saved.  Set <see cref="ConflictDetails.SavedRevision"/> to <c>null</c> when
        /// the conflict could not be resolved.
        /// </para>
        /// <note>
        /// Resolve methods should not throw <see cref="CouchbaseLiteException"/>s with <see cref="StatusCode.Conflict"/>.
        /// They should continue to try to resolve the conflict until successful, throw a <see cref="ConflictException"/>
        /// or return with <see cref="ConflictDetails.SavedRevision"/>=<c>null</c> to signal an error.
        /// </note>
        /// </remarks>
        public abstract void Resolve(ConflictDetails details);
    }
}
