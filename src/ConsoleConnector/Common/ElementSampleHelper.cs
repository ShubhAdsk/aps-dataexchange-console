using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.DataExchange;
using Autodesk.DataExchange.Core.Models;
using Autodesk.DataExchange.DataModels;
using Autodesk.DataExchange.Interface;
using Autodesk.DataExchange.Models;
using ConsoleConnector.Driver;
using ConsoleConnector.Samples;

namespace ConsoleConnector.Common
{
    /// <summary>
    /// Console plumbing for samples — loaded-exchange session, sync wrapper, element display.
    /// </summary>
    internal sealed record ElementSampleSession(
        ActiveExchange Active,
        ExchangeDetails Details,
        DataExchangeIdentifier Identifier,
        ElementDataModel Model);

    internal static class ElementSampleHelper
    {
        internal static bool EnsureFolder(SampleContext ctx) => NavigationHelper.EnsureFullFolder(ctx);

        internal static async Task<ElementSampleSession?> BeginAsync(SampleContext ctx)
        {
            if (!EnsureFolder(ctx))
                return null;

            var active = LoadedExchangePicker.Pick(ctx);
            if (active == null)
            {
                if (ctx.Exchanges.Count == 0)
                    TerminalUi.Warning("No loaded exchange. Run 2.2 Load Exchange first.");
                else
                    TerminalUi.Dim("Cancelled.");
                return null;
            }

            ExchangeDetails details;
            try
            {
                details = default!;
                await TerminalUi.RunWithStatusAsync(
                    "Resolving exchange details…",
                    async () =>
                    {
                        var response = await ctx.Client
                            .GetExchangeDetailsAsync(active.CollectionId, active.ExchangeFileUrn)
                            .ConfigureAwait(false);
                        if (response.IsFailed)
                            throw new InvalidOperationException(
                                response.Errors.FirstOrDefault()?.Message ?? "Failed to resolve exchange details.");
                        details = response.Value;
                    });
            }
            catch (Exception ex)
            {
                TerminalUi.Error($"Failed to resolve exchange: {ex}");
                return null;
            }

            var identifier = ExchangeSessionHelper.ToIdentifier(details, ctx.Folder!.HubId);
            return new ElementSampleSession(active, details, identifier, active.DataModel);
        }

        internal static void ClassifyGeneric(ElementDataModel model, IElement element)
        {
            var category = model.Classify(element, ClassificationSystem.Category, "Generics");
            var family = model.Classify(element, ClassificationSystem.Family, "Generic", parent: category);
            var type = model.DefineType("Type", "ConsoleConnector sample", parent: family);
            model.SetType(element, type);
        }

        internal static async Task<bool> SyncAsync(SampleContext ctx, ElementSampleSession session)
        {
            var title = session.Details.DisplayName ?? session.Active.ExchangeFileUrn;
            var success = false;
            await TerminalUi.RunWithStatusAsync(
                $"Syncing {title}…",
                async () =>
                {
                    var response = await ctx.Client.SyncExchangeDataAsync(
                        session.Identifier,
                        session.Model,
                        CancellationToken.None);
                    if (response.IsFailed)
                    {
                        var message = response.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                        TerminalUi.Error($"Sync failed: {message}");
                        return;
                    }

                    TerminalUi.Success("Sync complete.");
                    success = true;
                });
            return success;
        }

        internal static void PrintElementSummary(IElement element, string prefix = "    ")
        {
            TerminalUi.Chat($"{prefix}{element.Name} ({element.SourceId})");
            TerminalUi.Chat($"{prefix}  Category: {element.Category ?? "(none)"}");
            TerminalUi.Chat($"{prefix}  Family:   {element.Family ?? "(none)"}");
            TerminalUi.Chat($"{prefix}  Type:     {element.Type?.Value ?? "(none)"}");
            TerminalUi.Chat($"{prefix}  HasGeometry: {element.HasGeometry}");
        }
    }
}
