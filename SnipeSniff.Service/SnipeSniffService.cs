﻿/*
 * Copyright 2019 SnipeSniff Contributors (https://github.com/grimstoner/SnipeSniff)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using SnipeSniff.Service;
using System;
using Quartz;
using System.Collections.Specialized;
using Quartz.Impl;
using Serilog;

namespace SnipeSniff
{
    /// <summary>
    /// Locates and synchronizes system details with the configured <c>Snipe.It</c> 
    /// system. 
    /// </summary>
    public class SnipeSniffService : IDisposable
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="SnipeSniffService"/> class.
        /// </summary>
        public SnipeSniffService(int intervalInSeconds, bool isServerMode, string snipeApiAddress, 
            string snipeApiToken, string subnetString)
        {
            if (intervalInSeconds < 1)
                throw new ArgumentException("Invalid intervalInSeconds specified.");
            this.intervalInSeconds = intervalInSeconds;

            if (string.IsNullOrEmpty(snipeApiAddress))
                throw new ArgumentException("Invalid SnipeApiAddress specified.");
            this.snipeApiAddress = snipeApiAddress;

            if (string.IsNullOrEmpty(snipeApiToken))
                throw new ArgumentException("Invalid SnipeApiToken specified.");
            this.snipeApiToken = snipeApiToken;

            this.isServerMode = isServerMode;            
            this.subnetString = subnetString;

            Log.Information($"Configuration parameter IntervalInSeconds set to {intervalInSeconds}");
            Log.Information($"Configuration parameter ServerMode set to {isServerMode}");
            Log.Information($"Configuration parameter SnipeApiAddress set to {snipeApiAddress}");
            Log.Information($"Configuration parameter SnipeApiToken set to <not displayed>");
            Log.Information($"Configuration parameter SubnetsToScan set to {subnetString}");

            NameValueCollection props = new NameValueCollection
            {
                { "quartz.serializer.type", "binary" },
                { "quartz.scheduler.instanceName", "SnifferSchedule" },
                { "quartz.jobStore.type", "Quartz.Simpl.RAMJobStore, Quartz" },
                { "quartz.threadPool.threadCount", "1" }
            };
            StdSchedulerFactory factory = new StdSchedulerFactory(props);
            scheduler = factory.GetScheduler().ConfigureAwait(false).GetAwaiter().GetResult();

        }
       
        /// <summary>
        /// Starts the current service.
        /// </summary>
        internal void Start()
        {
            scheduler.Start().ConfigureAwait(false).GetAwaiter().GetResult();

            ScheduleJobs();
        }
        
        /// <summary>
        /// Stops the current service.
        /// </summary>
        internal void Stop()
        {
            scheduler.Shutdown().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Schedules the jobs for execution.
        /// </summary>
        public void ScheduleJobs()
        {
            // Create the job
            IJobDetail job = JobBuilder.Create<SnipeSniffJob>()
                .UsingJobData("ServerMode", isServerMode)
                .UsingJobData("SnipeApiAddress", snipeApiAddress)
                .UsingJobData("SnipeApiToken", snipeApiToken)
                .UsingJobData("SubnetString", subnetString)
                .WithIdentity("SniffJob", "MainGroup")
                .Build();

            // Create the trigger using intervalInSeconds
            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity("MainTrigger", "MainGroup")
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(intervalInSeconds)
                    .RepeatForever())
                .Build();

            // Tell quartz to schedule the job with the trigger
            scheduler.ScheduleJob(job, trigger).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        #region IDisposable Implementation

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        private void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    if (!scheduler.IsShutdown)
                        Stop();
                }

                this.isDisposed = true;
            }
        }

        #endregion

        #region Instance Fields

        /// <summary>
        /// Indicates if the current <see cref="SnipeSniffService"/> is disposed.
        /// </summary>
        private bool isDisposed;

        /// <summary>
        /// 
        /// </summary>
        private int intervalInSeconds;

        /// <summary>
        /// 
        /// </summary>
        private readonly IScheduler scheduler;

        /// <summary>
        /// 
        /// </summary>
        private bool isServerMode;

        /// <summary>
        /// 
        /// </summary>
        private string snipeApiAddress;

        /// <summary>
        /// 
        /// </summary>
        private string snipeApiToken;

        /// <summary>
        /// 
        /// </summary>
        private string subnetString;

        #endregion
    }
}
