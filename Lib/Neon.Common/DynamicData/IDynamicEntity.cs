//-----------------------------------------------------------------------------
// FILE:	    IDynamicEntity.cs
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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.DynamicData;
using Neon.DynamicData.Internal;

namespace Neon.DynamicData
{
    /// <summary>
    /// Defines the implementation of a data entity that wraps a JSON.NET
    /// <see cref="JObject"/> to provide strongly typed properties.  This
    /// is used in the Couchbase Lite extensions but may be useful for
    /// other scenarios that require future proofing.
    /// </summary>
    public interface IDynamicEntity : INotifyPropertyChanged
    {
        /// <summary>
        /// Returns the entity type string or <c>null</c>.
        /// </summary>
        string _GetEntityType();

        /// <summary>
        /// Sets the entity's link string.
        /// </summary>
        /// <param name="link">The non-<c>null</c> link.</param>
        /// <remarks>
        /// <para>
        /// <see cref="_SetLink(string)"/> and <see cref="_GetLink()"/> are used to
        /// implement entity linking for environments that provide an <see cref="IDynamicEntityContext"/> 
        /// implementation.
        /// </para>
        /// <note>
        /// Entity links once assigned, are considered to be invariant.
        /// </note>
        /// </remarks>
        void _SetLink(string link);

        /// <summary>
        /// Returns the entity's link string.
        /// </summary>
        /// <returns>The link string or <c>null</c>.</returns>
        /// <remarks>
        /// <see cref="_SetLink(string)"/> and <see cref="_GetLink()"/> are used to
        /// implement entity linking for environments that provide an <see cref="IDynamicEntityContext"/> 
        /// implementation.
        /// </remarks>
        string _GetLink();

        /// <summary>
        /// Initializes the model's entity properties, collections, etc. so they
        /// map to the JSON data in the <see cref="JObject"/> passed.
        /// </summary>
        /// <param name="jObject">The dynamic model data.</param>
        /// <param name="reload">Optionally specifies that the model is being reloaded.</param>
        /// <param name="setType">Pass <c>true</c> to initialize the entity type properties.</param>
        /// <returns>
        /// <c>true</c> if the new object had differences from the existing object
        /// and the updates were applied.
        /// </returns>
        /// <remarks>
        /// <note>
        /// Pass <paramref name="reload"/>=<c>true</c> to reload data from a new 
        /// <see cref="JObject"/> into the model.  In this case, the implementation
        /// must ensure that all appropriate property and collection change notifications 
        /// are raised to ensure that any listening UX elements will be updated.
        /// </note>
        /// </remarks>
        bool _Load(JObject jObject, bool reload = false, bool setType = true);

        /// <summary>
        /// Attaches the entity to an <see cref="IDynamicEntity"/> parent.
        /// </summary>
        /// <param name="parent">The parent entity.</param>
        void _Attach(IDynamicEntity parent);

        /// <summary>
        /// Detaches the entity from its <see cref="IDynamicEntity"/> parent.
        /// </summary>
        void _Detach();

        /// <summary>
        /// Returns the dynamic <see cref="JObject"/> used to back the object properties.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        JObject JObject { get; }

        /// <summary>
        /// Raises the entity's property changed event.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        void _OnPropertyChanged(string propertyName);

        /// <summary>
        /// Raised when any part of the entity or its tree of sub-entities is
        /// modified.
        /// </summary>
        event EventHandler<EventArgs> Changed;

        /// <summary>
        /// Raises the <see cref="Changed"/> event.
        /// </summary>
        void _OnChanged();
    }
}
