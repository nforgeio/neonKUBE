//-----------------------------------------------------------------------------
// FILE:	    ArgDictionary.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Retry;

namespace Neon.Collections
{
    /// <summary>
    /// A dictionary of objects keyed by case insenstive strings used as a shorthand 
    /// way for passing optional arguments to other class' methods.
    /// </summary>
    public class ArgDictionary : Dictionary<string, object>
    {
    }
}
