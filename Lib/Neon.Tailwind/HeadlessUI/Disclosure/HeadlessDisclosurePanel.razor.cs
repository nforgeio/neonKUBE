using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Neon.Tailwind
{
    public partial class HeadlessDisclosurePanel : HtmlBase, IDisposable
    {
        [CascadingParameter] public HeadlessDisclosure CascadedDisclosure { get; set; } = default!;

        /// <summary>
        /// Whether the disclosure panel is enabled.
        /// </summary>
        [Parameter] public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Whether the disclosure panel is visible.
        /// </summary>
        [Parameter] public DisclosureState IsVisible { get; set; } = DisclosureState.Closed;

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
        /// <inheritdoc/>
        public void Dispose()
        {
            _ = Disclosure.UnregisterPanel(this);
        }

        /// <inheritdoc/>
        public override Task SetParametersAsync(ParameterView parameters)
        {
            //This is here to follow the pattern/example as implmented in Microsoft's InputBase component
            //https://github.com/dotnet/aspnetcore/blob/main/src/Components/Web/src/Forms/InputBase.cs

            parameters.SetParameterProperties(this);

            if (Disclosure == null)
            {
                if (CascadedDisclosure == null)
                    throw new InvalidOperationException($"You must use {nameof(HeadlessDisclosurePanel)} inside an {nameof(HeadlessDisclosure)}.");

                Disclosure = CascadedDisclosure;
            }
            else if (CascadedDisclosure != Disclosure)
            {
                throw new InvalidOperationException($"{nameof(HeadlessDisclosure)} does not support changing the {nameof(HeadlessDisclosurePanel)} dynamically.");
            }

            return base.SetParametersAsync(ParameterView.Empty);
        }
    }
}
