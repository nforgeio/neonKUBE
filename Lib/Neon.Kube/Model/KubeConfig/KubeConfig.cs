//-----------------------------------------------------------------------------
// FILE:	    KubeConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Holds the <b>sensitive</b> information required to remotely manage an operating
    /// cluster using the <b>neon-cli</b>.
    /// </para>
    /// <note>
    /// <b>WARNING:</b> The information serialized by this class must be carefully protected
    /// because it can be used to assume control over a cluster.
    /// </note>
    /// </summary>
    public class KubeConfig
    {
    }
}
