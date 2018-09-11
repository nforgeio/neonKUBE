//-----------------------------------------------------------------------------
// FILE:	    HiveUpdateAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Used to discover <see cref="HiveUpdate"/> derived classes embedded in
    /// <b>neon-cli</b> so we don't need to main the update list manually.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HiveUpdateAttribute : Attribute
    {
    }
}
