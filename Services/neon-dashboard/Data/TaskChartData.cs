//-----------------------------------------------------------------------------
// FILE:	    Home.razor.cs
// CONTRIBUTOR: Simon Zhang
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.ComponentModel;

using ChartJs.Blazor;
using ChartJs.Blazor.LineChart;

namespace NeonDashboard.Data
{
    public enum TaskType
    {
        [Description("Memory")]
        MEMORY,
        [Description("CPU")]
        CPU,
        [Description("Disk Space")]
        DISK
    }
    public enum HealthStatus
    {
        HEALTHY,
        WARNING,
        DANGER,
        ERROR
    }
    public class TaskChartData
    {
        /// <summary>
        /// Chart Task Type 
        /// </summary>
        public TaskType Type { get; set; }

        /// <summary>
        /// Chart Config
        /// </summary>
        public LineConfig Config { get; set; }

        /// <summary>
        /// return true if <see cref="Config"/> is populated
        /// </summary>
        public Boolean isNull
        {
            get
            {
                return Config != null;
            }
        }

        /// <summary>
        /// ChartJS instance
        /// </summary>
        public Chart Chart { get; set; }

        /// <summary>
        /// total available of this resource
        /// </summary>
        public decimal Total { get; set; } = 0;

        /// <summary>
        ///  resource currently used
        /// </summary>
        public decimal Used { get; set; } = 0;

        /// <summary>
        /// total available of this resource
        /// </summary>
        public decimal Available
        {
            get
            {
                return Total - Used;
            }
        }

        /// <summary>
        /// tuple of threshold values (warning, danger) to switch visuals <see cref="Health"/>
        /// </summary>
        public (double, double) Threshhold { get; set; } = (0.85, 0.97);
    
        /// <summary>
        /// return <see cref="HealthStatus"/> based on <see cref="Threshhold"/> values
        /// </summary>
        public HealthStatus Health
        {
            get
            {
                if (Total == 0) return HealthStatus.ERROR;

                double ratio = (double) (Used / Total);
                if (ratio > Threshhold.Item2 && ratio <= 1) return HealthStatus.DANGER;
                else if (ratio > Threshhold.Item1) return HealthStatus.WARNING;
                else if (ratio < Threshhold.Item1 && ratio >= 0) return HealthStatus.HEALTHY;
                else return HealthStatus.ERROR;
            }
        }
    }
}
