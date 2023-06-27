//-----------------------------------------------------------------------------
// FILE:        CronJob.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;

using OpenTelemetry.Trace;

using Prometheus;

using Quartz;

namespace NeonClusterOperator
{
    /// <summary>
    /// 
    /// </summary>
    public class CronJob
    {
        private static readonly ILogger logger = TelemetryHub.CreateLogger<CronJob>();

        /// <summary>
        /// The name of the cron job.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The group.
        /// </summary>
        public string Group { get; set; } = "ClusterSettingsCron";

        /// <summary>
        /// The Job type.
        /// </summary>
        public Type Type { get; set; }   

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="type">The job type.</param>
        public CronJob(Type type)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));

            Type = type;
            Name = Type.Name;
        }

        /// <summary>
        /// Adds the cron job to a specified scheduler.
        /// </summary>
        /// <param name="scheduler">Specifies the scheduler.</param>
        /// <param name="k8s">Specifies the Kubernetes client.</param>
        /// <param name="cronSchedule">Specifies the schedule.</param>
        /// <param name="data">Optionally specifies a dictionary with additional data.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public Task AddToSchedulerAsync(
            IScheduler                  scheduler, 
            IKubernetes                 k8s, 
            string                      cronSchedule,
            Dictionary<string, object>  data = null)
        {
            Covenant.Requires<ArgumentNullException>(scheduler != null, nameof(scheduler));
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(cronSchedule), nameof(cronSchedule));

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("add-to-scheduler");

                var job = JobBuilder.Create(Type)
                    .WithIdentity(Name, Group)
                    .Build();

                job.JobDataMap.Put("Kubernetes", k8s);

                if (data != null)
                {
                    foreach (var item in data)
                    {
                        job.JobDataMap.Put(item.Key, item.Value);
                    }
                }

                // Trigger the job to run now, and then repeat every 10 seconds

                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity(Name, Group)
                    .WithCronSchedule(cronSchedule)
                    .StartNow()
                    .Build();

                // Tell quartz to schedule the job using our trigger

                return scheduler.ScheduleJob(job, trigger);
            }
        }

        /// <summary>
        /// Removes the job from the specified scheduler.
        /// </summary>
        /// <param name="scheduler">Specifies the scheduler.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DeleteFromSchedulerAsync(IScheduler scheduler)
        {
            Covenant.Requires<ArgumentNullException>(scheduler != null, nameof(scheduler));

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("delete-from-scheduler");

                try
                {
                    await scheduler.DeleteJob(new JobKey(Name, Group));
                }
                catch (NullReferenceException)
                {
                    return;
                }
            }
        }
    }
}
