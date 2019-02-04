//-----------------------------------------------------------------------------
// FILE:	    Entity.cs
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
using System.ComponentModel;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using Neon.Common;

namespace Neon.Data
{
    /// <summary>
    /// Common base implementation of <see cref="IEntity{T}"/>
    /// </summary>
    /// <typeparam name="T">The entity content type.</typeparam>
    public class Entity<T> : IEntity<T>
        where T : class, new()
    {
        /// <inheritdoc/>
        public virtual string GetKey()
        {
            throw new NotSupportedException($"[{this.GetType().FullName}] does not implement [{nameof(GetKey)}].");
        }

        /// <inheritdoc/>
        public virtual string GetRef()
        {
            throw new NotSupportedException($"[{this.GetType().FullName}] does not implement [{nameof(GetRef)}].");
        }

        /// <inheritdoc/>
        [JsonProperty(PropertyName = "__EntityType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "__EntityType", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string __EntityType { get; set; }

        /// <inheritdoc/>
        public virtual bool Equals(T other)
        {
            return NeonHelper.JsonEquals(this, other);
        }

        /// <inheritdoc/>
        public virtual void Normalize()
        {
        }
    }
}
