using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Neon.Tailwind
{
    public partial class HeadlessDisclosurePanel : HtmlBase
    {
        [CascadingParameter] public HeadlessDisclosure CascadedDisclosure { get; set; } = default!;

        /// <summary>
        /// Whether the disclosure panel is enabled.
        /// </summary>
        [Parameter] public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Whether the disclosure panel is visible.
        /// </summary>
        [Parameter] public DisclosureState IsVisible { get; set; } = DisclosureState.Open;

        protected HeadlessDisclosure Disclosure { get; set; } = default!;

        private HtmlElement rootElement;

        protected async override Task OnInitializedAsync()
        {
            await Disclosure.RegisterPanel(this);

        }
        public async Task Open()
        {
            IsVisible = DisclosureState.Open;
        }
        public async Task Close()
        {
            IsVisible = DisclosureState.Closed;
        }
    }
}
