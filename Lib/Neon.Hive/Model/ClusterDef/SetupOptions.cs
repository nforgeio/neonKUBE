//-----------------------------------------------------------------------------
// FILE:	    SetupOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Hive
{
    /// <summary>
    /// Specifies setup related options.
    /// </summary>
    public class SetupOptions
    {
        private const int defaultStepStaggerSeconds = 5;

        /// <summary>
        /// <para>
        /// Indicates that hive prepare and setup should be run in <b>debug mode</b>.
        /// This is intended to help debugging hive setup issues by having scripts
        /// uploaded multiple times at different stages of setup so that setup can
        /// be restarted with new scripts without having to restart setup from the
        /// beginning.  This defaults to <c>false</c>.
        /// </para>
        /// <note>
        /// This is intended for use by neonHIVE developers.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Debug", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        [Obsolete("Deprecated: Use the [neon-cli --debug] option instead.", error:true)]
        public bool Debug { get; set; } = false;

        /// <summary>
        /// <para>
        /// Specifies the maximum delay to be added between steps at strategic points 
        /// during hive preparation and setup to help mitigate potential problems 
        /// when mutiple hive nodes are trying to access the same Internet resources,
        /// potentially getting throttled by the remote endpoint.
        /// </para>
        /// <para>
        /// This defaults to <b>5 seconds</b> between these steps  Set this to 0 to disable
        /// the delay.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "StepStaggerSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultStepStaggerSeconds)]
        public int StepStaggerSeconds { get; set; } = defaultStepStaggerSeconds;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(HiveDefinition hiveDefinition)
        {
            if (StepStaggerSeconds < 0)
            {
                throw new HiveDefinitionException($"[{nameof(SetupOptions)}.{nameof(StepStaggerSeconds)}={StepStaggerSeconds}] cannot be negative.");
            }
        }
    }
}
