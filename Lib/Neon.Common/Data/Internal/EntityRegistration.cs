//-----------------------------------------------------------------------------
// FILE:	    EntityRegistration.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Data;

namespace Neon.Data.Internal
{
    /// <summary>
    /// The delegate registered by <see cref="IEntity"/> implementations that
    /// instantiates an entity using a parameterized constructor.
    /// </summary>
    /// <param name="jObject">The backing <see cref="JObject"/>.</param>
    /// <param name="context">The <see cref="IEntityContext"/> or <c>null</c>.</param>
    /// <returns>The new <see cref="IEntity"/>.</returns>
    public delegate IEntity EntityCreateDelegate(JObject jObject, IEntityContext context);

    /// <summary>
    /// An <see cref="IEntity"/> implementation's registration information.
    /// </summary>
    public struct EntityRegistration
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="typeIdentifier">The entity's application domain unique identifier string (or <c>null</c>).</param>
        /// <param name="creator">The entity creation delegate.</param>
        public EntityRegistration(Type entityType, string typeIdentifier, EntityCreateDelegate creator)
        {
            Covenant.Requires<ArgumentNullException>(entityType != null);
            Covenant.Requires<ArgumentNullException>(creator != null);

            this.EntityType     = entityType;
            this.TypeIdentifier = typeIdentifier;
            this.Creator        = creator;
        }

        /// <summary>
        /// Returns the entity type.
        /// </summary>
        public Type EntityType { get; private set; }

        /// <summary>
        /// Returns the entity's application domain unique identifier string or <c>null</c>.
        /// </summary>
        public string TypeIdentifier { get; private set; }

        /// <summary>
        /// Returns the entity creation delegate.
        /// </summary>
        public EntityCreateDelegate Creator { get; private set; }
    }
}
