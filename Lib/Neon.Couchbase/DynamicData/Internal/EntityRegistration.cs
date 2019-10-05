//-----------------------------------------------------------------------------
// FILE:	    EntityRegistration.cs
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
// limitations under the License.

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
using Neon.Couchbase.DynamicData;

namespace Neon.Couchbase.DynamicData.Internal
{
    /// <summary>
    /// The delegate registered by <see cref="IDynamicEntity"/> implementations that
    /// instantiates an entity using a parameterized constructor.
    /// </summary>
    /// <param name="jObject">The backing <see cref="JObject"/>.</param>
    /// <param name="context">The <see cref="IDynamicEntityContext"/> or <c>null</c>.</param>
    /// <returns>The new <see cref="IDynamicEntity"/>.</returns>
    public delegate IDynamicEntity EntityCreateDelegate(JObject jObject, IDynamicEntityContext context);

    /// <summary>
    /// An <see cref="IDynamicEntity"/> implementation's registration information.
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
            Covenant.Requires<ArgumentNullException>(entityType != null, nameof(entityType));
            Covenant.Requires<ArgumentNullException>(creator != null, nameof(creator));

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
