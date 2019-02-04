//-----------------------------------------------------------------------------
// FILE:	    BinderAttachmentAttribute.cs
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
using System.Reflection;
using System.Text;

using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.DynamicData;

namespace Neon.DynamicData
{
    /// <summary>
    /// Used to tag the properties of a binder document's <c>interface</c> definition
    /// so that the <c>entity-gen</c> tool will be able to generate code that 
    /// implements the <see cref="INotifyPropertyChanged"/> pattern for Couchbase Lite
    /// document attachments.
    /// </summary>
    public class BinderAttachmentAttribute : Attribute
    {
        /// <summary>
        /// The optional case insensitve name for the Couchbase Lite
        /// document attachment to be associated with this property.
        /// Otherwise, this will default to the property name.
        /// </summary>
        public string AttachmentName { get; set; }
    }
}
