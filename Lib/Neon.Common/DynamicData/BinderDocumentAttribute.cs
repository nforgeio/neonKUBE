//-----------------------------------------------------------------------------
// FILE:	    BinderDocumentAttribute.cs
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
    /// Used to tag an <c>interface</c> indicating that the <b>entity-gen</b> should
    /// generate a Couchbase Lite entity document class that that implements the 
    /// <see cref="INotifyPropertyChanged"/> pattern for the document attachments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Entity document classes that implement the  <see cref="INotifyPropertyChanged"/>
    /// pattern for the attachments are referred to as <b>binder documents</b>.
    /// You'll need to define an <c>interface</c> that includes zero or more properties
    /// that will be mapped to document attachments.  You must also define a separate
    /// <c>interface</c> that defines the document contents.
    /// </para>
    /// <para>
    /// The binder document interface must be tagged with the <see cref="BinderDocumentAttribute"/>,
    /// passing the type of the entity interface definition and the binder document
    /// properties must be tagged with the <see cref="BinderAttachmentAttribute"/>.
    /// </para>
    /// </remarks>
    public class BinderDocumentAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="entityType">The document content entity type <c>interface</c> definition.</param>
        public BinderDocumentAttribute(Type entityType)
        {
            this.EntityType = entityType;
        }

        /// <summary>
        /// Returns the document content entity type <c>interface</c> definition.
        /// </summary>
        public Type EntityType { get; private set; }

        /// <summary>
        /// Optional name for the generated class; otherwise the name will
        /// default to the interface name with the leading "I" character
        /// removed (if present).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optional namespace for the generated class; otherwise the namespace
        /// will default to the namespace of the tagged interface.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Optionally indicates that the generated class will be declared as <c>internal</c>
        /// rather than <c>public</c>, the default.
        /// </summary>
        public bool IsInternal { get; set; }
    }
}
