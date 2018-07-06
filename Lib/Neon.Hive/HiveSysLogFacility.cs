//-----------------------------------------------------------------------------
// FILE:	    HiveSysLogFacility.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Hive
{
    /// <summary>
    /// Maps neonHIVE services to <b>local#</b> SysLog facilities.  These mappings
    /// will be used when ingesting SysLog messages into the hive logging
    /// infrastructure.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <b>IMPORTANT:</b> Do not change any of these values without really knowing what
    /// you're doing.  It's likely that these values have been literally embedded
    /// in hive configuration scripts as well as Docker images.  Any change is likely
    /// to break things.
    /// </note>
    /// <note>
    /// <b>IMPORTANT:</b> These definitions must match those in the <b>$\Stack\Docker\Images\neonhive.sh</b>
    /// file.  You must manually update that file and then rebuild and push the containers
    /// as well as redeploy all hives from scratch.
    /// </note>
    /// </remarks>
    public static class HiveSysLogFacility
    {
        /// <summary>
        /// The syslog facility name used for traffic logs from the neonHIVE HAProxy based proxy
        /// services such as <b>neon-proxy-vault</b>, <b>neon-proxy-public</b>, and <b>neon-proxy-private</b>.
        /// This maps to syslog facility number 23.
        /// </summary>
        public const string ProxyName = "local7";

        /// <summary>
        /// The syslog facility number used for traffic logs from the neonHIVE HAProxy based proxy
        /// services such as <b>neon-proxy-vault</b>, <b>neon-proxy-public</b>, and <b>neon-proxy-private</b>.
        /// </summary>
        public const int ProxyNumber = 23;
    }
}
