// -----------------------------------------------------------------------------
// FILE:	    JobOptions.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies enhanced Quartz cron schedules for NeonKUBE cluster jobs performed by
    /// cluster operators such as <b>neon-cluster-operator</b>.
    /// </summary>
    public class JobOptions
    {
        private const string Random                = "R R 0 ? * TUE";
        private const string OnceAWeek12amSchedule = "0 0 0 ? * TUE";
        private const string OnceAWeek2amSchedule  = "0 0 2 ? * TUE";
        private const string DailyRandomSchedule   = "R R 0 ? * *";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public JobOptions()
        {
        }

        /// <summary>
        /// Schedules renewal of the Kubernetes cluster certificate.  This defaults to a random
        /// time between 12:00am and 1:00am (UTC) on Tuesdays.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterCertificateRenewal", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterCertificateRenewal", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public JobSchedule ClusterCertificateRenewal { get; set; }

        /// <summary>
        /// Schedules the persisting of NeonKUBE cluster container images from
        /// cluster nodes to Harbor as required.  This defaults to a random
        /// time between 12:00am and 1:00am (UTC) on Tuesdays.
        /// </summary>
        [JsonProperty(PropertyName = "HarborImagePush", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "harborImagePush", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public JobSchedule HarborImagePush { get; set; }

        /// <summary>
        /// Schedules Kubernetes control plane certificate renewal.   This defaults to a random
        /// time between 12:00am and 1:00am (UTC) on Tuesdays.
        /// </summary>
        [JsonProperty(PropertyName = "ControlPlaneCertificateRenewal", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "controlPlaneCertificateRenewal", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public JobSchedule ControlPlaneCertificateRenewal { get; set; }

        /// <summary>
        /// Schedules updates of the public certificate authorities on cluster nodes.  This defaults to a random
        /// time between 12:00am and 1:00am (UTC) on Tuesdays.
        /// </summary>
        [JsonProperty(PropertyName = "NodeCaCertificateRenewal", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "nodeCaCertificateRenewal", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public JobSchedule NodeCaCertificateRenewal { get; set; }

        /// <summary>
        /// Schedules the application of Linux security patches on the cluster nodes.  This defaults to a random
        /// time between 12:00am and 1:00am (UTC) on Tuesdays.
        /// </summary>
        [JsonProperty(PropertyName = "LinuxSecurityPatches", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "linuxSecurityPatches", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public JobSchedule LinuxSecurityPatches { get; set; }

        /// <summary>
        /// Schedules the transmission of cluster telemetry to NEONFORGE.  This defaults to a random
        /// time between 12:00am and 1:00am (UTC) daily.
        /// </summary>
        [JsonProperty(PropertyName = "TelemetryPing", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "telemetryPing", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public JobSchedule TelemetryPing { get; set; }

        /// <summary>
        /// <para>
        /// Schedules the deletion of pods that have been terminated for at least <see cref="TerminatedPodGcThresholdMinutes"/>.
        /// This defaults to every 15 minutes.
        /// </para>>
        /// <note>
        /// To avoid a potential race condition, <b>neon-cluster-operator</b> only removes
        /// pods with what looks like a generated name, including a unique ID suffix.  This
        /// avoids situations where the operator identifies a pod to be deleted but before
        /// the operator has a chance to delete the pod, something else deletes and then
        /// recreates the pod with the same name, resulting in the wrong pod being removed.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "TerminatedPodGc", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "terminatedPodGc", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public JobSchedule TerminatedPodGc { get; set; }

        /// <summary>
        /// Specifies the delay in milliseconds that the terminated pod removal job
        /// will pause after scanning a namespace for terminated jobs and also
        /// after each job removal to reduce pressure on the API Server.  This
        /// defaults to <b>1000 milliseconds</b> (1 second).
        /// </summary>
        [JsonProperty(PropertyName = "TerminatedPodGcDelayMilliseconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "terminatedPodGcDelayMilliseconds", ApplyNamingConventions = false)]
        [DefaultValue(1000)]
        public int TerminatedPodGcDelayMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Specifies the number of minutes after a pod terminates sucessfully or not before it
        /// becomes eligible for removal by the <b>neon-cluster-operator</b>.  This defaults to
        /// <b>720 minutes</b> (12 hours) to give operations teams a chance to investigate
        /// potential recent problems.
        /// </summary>
        [JsonProperty(PropertyName = "TerminatedPodGcThresholdMinutes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "terminatedPodGcThresholdMinutes", ApplyNamingConventions = false)]
        [DefaultValue(720)]
        public int TerminatedPodGcThresholdMinutes { get; set; } = 720;

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            ClusterCertificateRenewal ??= new JobSchedule(enabled: true, schedule: OnceAWeek12amSchedule);
            ClusterCertificateRenewal.Validate(clusterDefinition, $"{nameof(JobOptions)}.{nameof(ClusterCertificateRenewal)}");

            if (HarborImagePush == null)
            {
                // We're going to disable harbor pushing by default for DESKTOP clusters,
                // but we will honor the schedule in the cluster definition if present.

                HarborImagePush = new JobSchedule(enabled: !clusterDefinition.IsDesktop, schedule: OnceAWeek12amSchedule);
            }
            else
            {
                HarborImagePush = new JobSchedule(enabled: true, schedule: Random);
            }

            HarborImagePush.Validate(clusterDefinition, $"{nameof(JobOptions)}.{nameof(HarborImagePush)}");

            ControlPlaneCertificateRenewal ??= new JobSchedule(enabled: true, schedule: OnceAWeek12amSchedule);
            ControlPlaneCertificateRenewal.Validate(clusterDefinition, $"{nameof(JobOptions)}.{nameof(ControlPlaneCertificateRenewal)}");

            LinuxSecurityPatches ??= new JobSchedule(enabled: true, schedule: OnceAWeek2amSchedule);
            LinuxSecurityPatches.Validate(clusterDefinition, $"{nameof(JobOptions)}.{nameof(LinuxSecurityPatches)}");

            NodeCaCertificateRenewal ??= new JobSchedule(enabled: true, schedule: OnceAWeek12amSchedule);
            NodeCaCertificateRenewal.Validate(clusterDefinition, $"{nameof(JobOptions)}.{nameof(NodeCaCertificateRenewal)}");

            TelemetryPing ??= new JobSchedule(enabled: true, schedule: DailyRandomSchedule);
            TelemetryPing.Validate(clusterDefinition, $"{nameof(JobOptions)}.{nameof(TelemetryPing)}");

            TerminatedPodGc ??= new JobSchedule(enabled: true, schedule: "0 0/15 * ? * *");
            TerminatedPodGc.Validate(clusterDefinition, $"{nameof(JobOptions)}.{nameof(TerminatedPodGc)}");

            if (TerminatedPodGcDelayMilliseconds < 0)
            {
                throw new ClusterDefinitionException($"[{nameof(JobOptions)}.{nameof(TerminatedPodGcDelayMilliseconds)}={TerminatedPodGcDelayMilliseconds}] cannot be negative.");
            }

            if (TerminatedPodGcThresholdMinutes < 0)
            {
                throw new ClusterDefinitionException($"[{nameof(JobOptions)}.{nameof(TerminatedPodGcThresholdMinutes)}={TerminatedPodGcThresholdMinutes}] cannot be negative.");
            }
        }
    }
}
