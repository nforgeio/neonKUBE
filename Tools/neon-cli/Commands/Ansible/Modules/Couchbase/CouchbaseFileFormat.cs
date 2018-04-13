//-----------------------------------------------------------------------------
// FILE:	    CouchbaseFileFormat.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Runtime.Serialization;

namespace NeonCli.Ansible.Couchbase
{
    /// <summary>
    /// Enumerates Couchbase inport/export file formats.
    /// </summary>
    public enum CouchbaseFileFormat
    {
        /// <summary>
        /// Format with one JSON document per line.
        /// </summary>
        [EnumMember(Value = "json-lines")]
        JsonLines = 0,

        /// <summary>
        /// Format as a JSON array of documents.
        /// </summary>
        [EnumMember(Value = "json-array")]
        JsonArray
    }
}
