//-----------------------------------------------------------------------------
// FILE:	    BinderAttachmentAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
