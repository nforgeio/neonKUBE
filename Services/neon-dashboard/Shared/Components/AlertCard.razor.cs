using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;

namespace NeonDashboard.Shared.Components

{
     public enum StyleType
    {
        Default,
        Outline,
        Error,
        Success,
        Warning
    };

 

    public partial class AlertCard: ComponentBase, IDisposable
    {
        [Parameter]
        public StyleType Type { get; set; } = StyleType.Default;

        [Parameter]
        public string Title { get; set; }

        [Parameter]
        public RenderFragment? Left { get; set; }

        [Parameter]
        public RenderFragment? Right { get; set; }

        [Parameter]
        public RenderFragment? Body { get; set; }

        private static  Dictionary<StyleType, string> CardStyle = new Dictionary<StyleType, string>()
        {
            {StyleType.Default,"text-slate-50 bg-card " },
            {StyleType.Outline,"text-slate-50 border border-slate-500" },
            {StyleType.Error,"" },
            {StyleType.Success,"" },
            {StyleType.Warning,"" },
        };


        public void Dispose()
        {
        }

    }
}
