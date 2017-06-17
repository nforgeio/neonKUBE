//-----------------------------------------------------------------------------
// FILE:	    IEntity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Data
{
    /// <summary>
    /// Defines standard entity properties that can be used for organizing and querying
    /// documents in NoSQL databases.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is very common to need to be able to identify entities by type and also to  
    /// organize entities into sets within a database.  The <see cref="Type"/> string
    /// property can be set to a string identifying the entity type.  This string is
    /// case sensitive and by convention, should always be lowercase.  Complex databases
    /// should use a simple dot notation to organize entity types into namespaces to
    /// avoid conflicts.
    /// </para>
    /// <para>
    /// The <see cref="Table"/> property can be used to organize entities into sets.
    /// This is a case senstitive string and by convention should be lowercase and 
    /// a simple dot notation should be use to organize table names.  Any single entity
    /// may belong to no table or a single table.
    /// </para>
    /// <note>
    /// Stictly speaking, I could have called this property something like <b>EntitySet</b>
    /// which would be more accurate, but <see cref="Table"/> is easier to say and
    /// most database developers will immediately understand the concept.
    /// </note>
    /// <para>
    /// This interface is implemented by <see cref="Entity"/> which will be defined 
    /// as the base class for most entities.  This implementation serializes the 
    /// properties as <b>_type</b> and <b>_table</b> in an effort to avoid conflicts
    /// with your entity properties.  You may use your own implementation of 
    /// <see cref="IEntity"/> to use different field names if necessary.
    /// </para>
    /// <para>
    /// The <see cref="GetTypeProperty"/> and <see cref="GetTableProperty"/> methods
    /// return the actual property names used to serialize the properties.  You can
    /// use these methods to avoid hardcoding <b>_type</b> and <b>_table</b> strings
    /// into your database queries.
    /// </para>
    /// </remarks>
    public interface IEntity
    {
        /// <summary>
        /// Identifies the entity type.  By convention, this is a lowercase string
        /// using a simple dot notation to define namespaces.
        /// </summary>
        string Type { get; set; }

        /// <summary>
        /// Identifies the entity table.  By convention, this is a lowercase string
        /// using a simple dot notation to define namespaces.
        /// </summary>
        string Table { get; set; }

        /// <summary>
        /// Returns the property name used to serialize <see cref="Type"/>.
        /// </summary>
        /// <returns>The property name.</returns>
        string GetTypeProperty();

        /// <summary>
        /// Returns the property name used to serialize <see cref="Table"/>.
        /// </summary>
        /// <returns>The property name.</returns>
        string GetTableProperty();
    }
}
