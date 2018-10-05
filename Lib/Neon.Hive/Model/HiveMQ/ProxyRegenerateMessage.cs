//-----------------------------------------------------------------------------
// FILE:	    ProxyRegenerateMessage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// Published to the <see cref="HiveMQChannels.ProxyNotify"/> channel to notify
    /// <b>neon-proxy-manager</b> that the proxy configuration has changed and
    /// that it should regenerate the configuration artifacts required by the
    /// other proxy related components.
    /// </summary>
    public class ProxyRegenerateMessage
    {
    }
}
