//-----------------------------------------------------------------------------
// FILE:	    ServiceDetails.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// <para>
    /// Holds the details describing a running Docker swarm service
    /// from the service list or inspection REST APIs.
    /// </para>
    /// <note>
    /// This type matches the Docker API v1.35.
    /// </note>
    /// </summary>
    public class ServiceDetails : INormalizable
    {
        /// <summary>
        /// The service ID.
        /// </summary>
        [JsonProperty(PropertyName = "ID", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string ID { get; set; }

        /// <summary>
        /// Service update version information.
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceVersion Version { get; set; }

        /// <summary>
        /// Time when the service was created.
        /// </summary>
        [JsonProperty(PropertyName = "CreatedAt", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string CreatedAt { get; set; }

        /// <summary>
        /// Time when the service was last updated.
        /// </summary>
        [JsonProperty(PropertyName = "UpdatedAt", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string UpdatedAt { get; set; }

        /// <summary>
        /// The service specification.
        /// </summary>
        [JsonProperty(PropertyName = "Spec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceSpec Spec { get; set; }

        /// <summary>
        /// Optionally describes the service's state before the last update.
        /// This is the state the service will revert to when it's rolled
        /// back.
        /// </summary>
        [JsonProperty(PropertyName = "PreviousSpec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceSpec PreviousSpec { get; set; }

        /// <summary>
        /// Describes the service's current endpoint state.
        /// </summary>
        [JsonProperty(PropertyName = "Endpoint", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceEndpointSpec Endpoint { get; set; }

        /// <summary>
        /// Describes the service update status.
        /// </summary>
        [JsonProperty(PropertyName = "UpdateStatus", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceUpdateStatus UpdateStatus { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Version      = Version ?? new ServiceVersion();
            Spec         = Spec ?? new ServiceSpec();
            Endpoint     = Endpoint ?? new ServiceEndpointSpec();
            UpdateStatus = UpdateStatus ?? new ServiceUpdateStatus();

            Version?.Normalize();
            Spec?.Normalize();
            PreviousSpec?.Normalize();
            Endpoint?.Normalize();
            UpdateStatus?.Normalize();
        }
    }
}
