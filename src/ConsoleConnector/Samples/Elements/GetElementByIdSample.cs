using System;
using System.Threading.Tasks;
using ConsoleConnector.Driver;
using ConsoleConnector.Common;

namespace ConsoleConnector.Samples
{
    /// <summary>
    /// What you learn: How to look up one element by its source id.
    /// SDK: ElementDataModel.GetElementById.
    /// Console plumbing: ElementSampleHelper.
    /// Prerequisites: 2.2 Load Exchange.
    /// </summary>
    [SampleAddress(3, 4)]
    public sealed class GetElementByIdSample : ISample
    {
        public string Name => "Get Element By Id";
        public string Description => "Look up one element by its source id";

        public async Task RunAsync(SampleContext ctx)
        {
            var session = await ElementSampleHelper.BeginAsync(ctx);
            if (session == null)
                return;
            var model = session.Model;
            var elementId = Prompt.AskString("Element id");
            if (string.IsNullOrWhiteSpace(elementId))
            {
                TerminalUi.Chat("Element id is required.");
                return;
            }

            var element = model.GetElementById(elementId);
            if (element == null)
            {
                TerminalUi.Warning($"No element found with id: {elementId}");
                return;
            }

            TerminalUi.Chat("Found:");
            ElementSampleHelper.PrintElementSummary(element);
        }
    }
}
