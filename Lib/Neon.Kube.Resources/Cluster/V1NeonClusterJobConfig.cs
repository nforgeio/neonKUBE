//-----------------------------------------------------------------------------
// FILE:        V1NeonClusterJobConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Text.Json.Serialization;

using k8s;
using k8s.Models;

using Neon.JsonConverters;
using Neon.Kube;
using Neon.Operator.Attributes;

namespace Neon.Kube.Resources.Cluster
{
    /// <summary>
    /// Specifies the cluster job settings.
    /// </summary>
    [KubernetesEntity(Group = KubeGroup, ApiVersion = KubeApiVersion, Kind = KubeKind, PluralName = KubePlural)]
    [EntityScope(EntityScope.Cluster)]
    public class V1NeonClusterJobConfig : IKubernetesObject<V1ObjectMeta>, ISpec<V1NeonClusterJobConfig.NeonClusterJobsSpec>, IStatus<V1NeonClusterJobConfig.NeonClusterJobsStatus>
    {
        /// <summary>
        /// Object API group.
        /// </summary>
        public const string KubeGroup = ResourceHelper.NeonKubeResourceGroup;

        /// <summary>
        /// Object API version.
        /// </summary>
        public const string KubeApiVersion = "v1alpha1";

        /// <summary>
        /// Object API kind.
        /// </summary>
        public const string KubeKind = "NeonClusterJob";

        /// <summary>
        /// Object plural name.
        /// </summary>
        public const string KubePlural = "neonclusterjobs";

        /// <summary>
        /// <para>
        /// Specifies the name for the <b>singular</b> jobs resource.
        /// </para>
        /// <note>
        /// <b>neon-cluster-operator</b> ignores any resources not named by this.
        /// </note>
        /// </summary>
        public const string SingularName = "jobconfig";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public V1NeonClusterJobConfig()
        {
            ApiVersion = $"{KubeGroup}/{KubeApiVersion}";
            Kind       = KubeKind;
        }

        /// <summary>
        /// Returns the schema version of this representation of an object.
        /// </summary>
        public string ApiVersion { get; set; }

        /// <summary>
        /// Returns the resource klind.
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// Gets or sets standard object metadata.
        /// </summary>
        public V1ObjectMeta Metadata { get; set; }

        /// <summary>
        /// The spec.
        /// </summary>
        public NeonClusterJobsSpec Spec { get; set; }

        /// <summary>
        /// The status.
        /// </summary>
        public NeonClusterJobsStatus Status { get; set; }

        /// <summary>
        /// Specifies the enhanced CRON schedule for a cluster job as well as an
        /// indication of whether the job is enabled or disabled.
        /// </summary>
        public class JobSchedule
        {
            private const string defaultSchedule = "R R 0 ? * *";

            /// <summary>
            /// Default constructor.
            /// </summary>
            public JobSchedule()
            {
            }

            /// <summary>
            /// Parameterized constructor.
            /// </summary>
            /// <param name="enabled">Indicates whether the job is enabled.</param>
            /// <param name="schedule">Specifies the enhanced Quartz job schedule.</param>
            public JobSchedule(bool enabled, string schedule)
            {
                this.Enabled  = enabled;
                this.Schedule = schedule;
            }

            /// <summary>
            /// Indicates whether this job is enabled or disabled.  This defaults to <c>false</c>.
            /// </summary>
            public bool Enabled { get; set; } = false;

            /// <summary>
            /// The update schedule. This is extended Quartz CRON expression.  This defaults
            /// to <b>"R R 0 ? * *"</b> which fires every day at a random minute and second
            /// between 12:00am and 1:00am.
            /// </summary>
            /// <remarks>
            /// <para>
            /// Cron expressions consist of seven sub-expressions that describe the details of the schedule.
            /// The sub expressions are:
            /// </para>
            /// <list type="bullet">
            ///     <item>Seconds (0..59)</item>
            ///     <item>Minutes (0..59)</item>
            ///     <item>Hours (0..23)</item>
            ///     <item>Day-of-Month (1..31)</item>
            ///     <item>Month (1..12) or MON-DEC</item>
            ///     <item>Day-of-Week (1..7) or SUN-SAT</item>
            ///     <item>Year (optional) (1970..2099)</item>
            /// </list>
            /// <para>
            /// An example of a complete CRON expression is <code>0 0 15 ? * MON</code> which means
            /// every Monday at 3pm.
            /// </para>        /// <para>
            /// For the full documentation which describes special characters, see: 
            /// https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html#cron-expressions
            /// </para>
            /// <note>
            /// <para>
            /// In addition to the standard Quartz defined special characters, we also
            /// support the <b>R</b> character which picks a random value within the
            /// allowed range for a field.  For example:
            /// </para>
            /// <para>
            /// 0 R R * * *
            /// </para>
            /// <para>
            /// schedules the job for a random hour and minute during the day.  This is useful
            /// for situations like uploading telemetry to a global service where you don't
            /// want a potentially large number of clients being scheduled to hit the
            /// service at the same time.
            /// </para>
            /// </note>
            /// </remarks>
            public string Schedule { get; set; } = defaultSchedule;
        }

        /// <summary>
        /// The certificate update schedules.
        /// </summary>
        public class NeonClusterJobsSpec
        {
            /// <summary>
            /// CRON schedule for renewing the control plane certificate.
            /// </summary>
            public JobSchedule ControlPlaneCertificateRenewal { get; set; } = new JobSchedule();

            /// <summary>
            /// CRON schedule for updating for Node CA certificates.
            /// </summary>
            public JobSchedule NodeCaCertificateUpdate { get; set; } = new JobSchedule();

            /// <summary>
            /// CRON schedule for applying Linux security patches to the cluster nodes.
            /// </summary>
            public JobSchedule LinuxSecurityPatch { get; set; } = new JobSchedule();

            /// <summary>
            /// CRON schedule for ensuring that the required NeonKUBE container images
            /// are loaded into Harbor.
            /// </summary>
            public JobSchedule HarborImagePush { get; set; } = new JobSchedule();

            /// <summary>
            /// CRON schedule for sending telemerty pings to the headend.
            /// </summary>
            public JobSchedule TelemetryPing { get; set; } = new JobSchedule();

            /// <summary>
            /// CRON schedule for renewing the cluster certficate.
            /// </summary>
            public JobSchedule ClusterCertificateRenewal { get; set; } = new JobSchedule();

            /// <summary>
            /// CRON schedule for garbage collecting terminated pods.
            /// </summary>
            public JobSchedule TerminatedPodGc { get; set; } = new JobSchedule();

            /// <summary>
            /// Specifies the delay in milliseconds the terminated pod removal job
            /// will pause after scanning a namespace for terminated jobs and also
            /// after each job removal to reduce pressure on the API Server.  This
            /// defaults to <b>1000 milliseconds</b> (1 second).
            /// </summary>
            public int TerminatedPodGcDelayMilliseconds { get; set; } = 1000;

            /// <summary>
            /// Specifies the number of minutes after a pod terminates sucessfully or not before it
            /// becomes eligible for removal by the <b>neon-cluster-operator</b>.  This defaults to
            /// <b>720 minutes</b> (12 hours) to give operations teams a chance to investigate
            /// recent problems.
            /// </summary>
            public int TerminatedPodGcThresholdMinutes { get; set; } = 720;
        }

        /// <summary>
        /// The status.
        /// </summary>
        public class NeonClusterJobsStatus
        {
            /// <summary>
            /// Control plane certificate update status.
            /// </summary>
            public JobStatus ControlPlaneCertificateRenewal { get; set; } = new JobStatus();

            /// <summary>
            /// Node CA certificate update status.
            /// </summary>
            public JobStatus NodeCaCertificateUpdate { get; set; } = new JobStatus();

            /// <summary>
            /// Update spec for security status.
            /// </summary>
            public JobStatus LinuxSecurityPatch { get; set; } = new JobStatus();

            /// <summary>
            /// Container images push to Harbor update status.
            /// </summary>
            public JobStatus HarborImagePush { get; set; } = new JobStatus();

            /// <summary>
            /// Cluster telemetry update status.
            /// </summary>
            public JobStatus TelemetryPing { get; set; } = new JobStatus();

            /// <summary>
            /// Certificate update status.
            /// </summary>
            public JobStatus ClusterCertificateRenewal { get; set; } = new JobStatus();

            /// <summary>
            /// Worker VCPU check status.
            /// </summary>
            public JobStatus MinWorkerNodeVcpu { get; set; } = new JobStatus();

            /// <summary>
            /// Terminated pod GC status.
            /// </summary>
            public JobStatus TerminatedPodGc { get; set; } = new JobStatus();
        }

        /// <summary>
        /// Cluster job status.
        /// </summary>
        public class JobStatus
        {
            /// <summary>
            /// The original CRON schedule specified in the job spec.  This is used
            /// to detect when the job schedule has been changed.
            /// </summary>
            public string OriginalCronSchedule { get; set; }

            /// <summary>
            /// The CRON schedule for the job, with any <b>"R"</b> fields resolved to
            /// specific values.  When not <c>null</c> or empty, this will be used to
            /// schedule the job instead of the CRON schedule in the spec to avoid the
            /// possibility of having jobs run multiple times when controller leadership
            /// changes.
            /// </summary>
            public string ResolvedCronSchedule { get; set; }

            /// <summary>
            /// The time that the job last completed.
            /// </summary>
            [JsonConverter(typeof(JsonNullableDateTimeConverter))]
            public DateTime? LastCompleted { get; set; }
        }
    }
}
