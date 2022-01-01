//-----------------------------------------------------------------------------
// FILE:	    MetricsMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Retry;
using Neon.Windows;

namespace Neon.Service
{
    /// <summary>
    /// Used control how or whether a <see cref="NeonService"/>  publishes Prometheus metrics.
    /// </summary>
    public enum MetricsMode
    {
        /// <summary>
        /// Metrics publishing is disabled.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Metrics will be scraped by Prometheus.
        /// </summary>
        Scrape,

        /// <summary>
        /// <para>
        /// Metrics will scraped by Prometheus but any port conflicts or any endpoint
        /// registration errors thrown by <b>HttpListener</b> on Windows will be ignored.
        /// </para>
        /// <note>
        /// This mode is really intended for test environments where these errors aren't
        /// relavent.  We don't recommend this for production deployments.
        /// </note>
        /// </summary>
        ScrapeIgnoreErrors,

        /// <summary>
        /// Metrics will be pushed to a Prometheus <b>Pushgateway</b>.
        /// </summary>
        Push
    }
}
