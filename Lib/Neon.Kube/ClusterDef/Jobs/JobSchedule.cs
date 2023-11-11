// -----------------------------------------------------------------------------
// FILE:	    JobSchedule.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube.Resources.Cluster;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies the enhanced cron schedule for a cluster job as well as an
    /// indication of whether the job is enabled or disabled.
    /// </summary>
    public class JobSchedule
    {
        //---------------------------------------------------------------------
        // Static members

        private const string defaultSchedule = "R R 0 ? * *";

        /// <summary>
        /// Casts a <see cref="JobSchedule"/> into a <see cref="V1NeonClusterJobs.JobSchedule"/>.
        /// </summary>
        /// <param name="jobSchedule">Specifies the schedule being converted.</param>
        /// <returns>The converted schedule.</returns>
        public static implicit operator V1NeonClusterJobs.JobSchedule(JobSchedule jobSchedule)
        {
            Covenant.Requires<ArgumentNullException>(jobSchedule != null, nameof(jobSchedule));

            return new V1NeonClusterJobs.JobSchedule(jobSchedule.Enabled, jobSchedule.Schedule);
        }

        //---------------------------------------------------------------------
        // Instance members

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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(schedule), nameof(schedule));
            NeonExtendedHelper.FromEnhancedCronExpression(schedule);

            this.Enabled  = enabled;
            this.Schedule = schedule;
        }

        /// <summary>
        /// Indicates whether this job is enabled or disabled.  This defaults to <c>false</c>.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// The update schedule. This is enxtended Quartz cron expression.  This defaults
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
        /// An example of a complete cron expression is <code>0 0 15 ? * MON</code> which means
        /// every Monday at 3pm.
        /// </para>        /// <para>
        /// For the full documentation which describes special characters, see: 
        /// https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html#cron-expressions
        /// </para>
        /// <note>
        /// <para>
        /// In addition to the standard Quartz defined special characters, we also
        /// support the <b>R</b> character which picks a random value within the
        /// allow range for a field.  For example:
        /// </para>
        /// <para>
        /// 0 0 R R * *
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

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="jobName">Identifies the related job for error messages.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition, string jobName)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jobName), nameof(jobName));

            if (string.IsNullOrEmpty(Schedule))
            {
                Schedule = defaultSchedule;
            }

            // Validate the schedule.

            try
            {
                NeonExtendedHelper.FromEnhancedCronExpression(Schedule);
            }
            catch (Exception e)
            {
                throw new ClusterDefinitionException($"{jobName}: {e.Message}");
            }
        }
    }
}
