//-----------------------------------------------------------------------------
// FILE:	    DbCluster.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using ICSharpCode.SharpZipLib.Zip;
using Neon.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Neon.Cluster
{
    /// <summary>
    /// Used to persist information and status for a NeonCluster database to Consul at <b>neon/databases/DBNAME</b>.
    /// </summary>
    public class DbCluster
    {
    }
}
