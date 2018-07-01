//-----------------------------------------------------------------------------
// FILE:	    LogSources.cs
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
    /// Identifies some common applications that may emit logs to be processed
    /// by the neonHIVE log pipeline.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <b>IMPORTANT:</b> Do not change any of these values without really knowing what
    /// you're doing.  It's likely that these values have been literally embedded
    /// in hive configuration scripts as well as Docker images.  Any change is likely
    /// to break things.
    /// </note>
    /// <para>
    /// By default, neonHIVE log pipeline attempts to extract some fields such
    /// as the timestamp, log level, module, and the remaining message from log
    /// events passing through the pipeline.  This can work for many applications,
    /// but sometimes logs require custom processing.
    /// </para>
    /// <para>
    /// These identifiers are intended to be set as the leading TD-Agent tag 
    /// segment for events emitted to the pipeline.  The typical case will be
    /// to specify <b>--log-driver=fluentd</b> and <b>--log-opt tag=[value]</b> as one of
    /// these strings when deploying a Docker service or container.  This gives 
    /// the <b>neon-log-collector</b> service enough information to customize 
    /// event parsing for the specific application.
    /// </para>
    /// <para>
    /// These are the predefined identifiers supported by neonHIVE out of the box.
    /// You may specify custom tags and then extend the neon-log-collector image to 
    /// support other applications.
    /// </para>
    /// </remarks>
    public static class LogSources
    {
        /// <summary>
        /// Many NeonResearch applications emit a common log message format that
        /// include an optional timestamp, optional log-level, and optional module
        /// formatted as described in the remarks.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NeonResearch log messages may include timestamp, log level, and
        /// module sections in that order, followed by the message text.  Each
        /// of the optional sections are surrounded by square brackets and are
        /// formatted like:
        /// </para>
        /// <code language="none">
        /// [2017-01-27T19:04:11.000+00:00] [INFO] [module:main] The event message.
        /// </code> 
        /// </remarks>
        public const string NeonCommon = "neon-common";

        /// <summary>
        /// Elasticsearch cluster node.
        /// </summary>
        public const string ElasticSearch = "elasticsearch";
    }
}
