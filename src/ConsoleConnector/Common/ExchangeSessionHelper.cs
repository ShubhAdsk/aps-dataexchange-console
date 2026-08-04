using System;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.DataExchange;
using Autodesk.DataExchange.Core.Enums;
using Autodesk.DataExchange.Core.Models;
using Autodesk.DataExchange.DataModels;
using Autodesk.DataExchange.Interface;
using Autodesk.DataExchange.Models;
using ConsoleConnector.Driver;
using ConsoleConnector.Samples;

namespace ConsoleConnector.Common
{
    /// <summary>
    /// Console plumbing for samples — session state for loaded/created exchanges.
    /// </summary>
    internal static class ExchangeSessionHelper
    {
        internal static DataExchangeIdentifier ToIdentifier(ExchangeDetails details, string? hubId) =>
            new()
            {
                ExchangeId = details.ExchangeID,
                CollectionId = details.CollectionID,
                HubId = details.HubId ?? hubId,
            };

        internal static async Task<bool> LoadFromDetailsAsync(
            SampleContext ctx,
            ExchangeDetails details,
            bool useEmptyModelForNewExchange = false)
        {
            if (string.IsNullOrWhiteSpace(details.FileUrn))
            {
                TerminalUi.Error("Created exchange has no file URN.");
                return false;
            }

            var displayName = details.DisplayName ?? details.FileUrn;
            ElementDataModel model;

            if (useEmptyModelForNewExchange)
            {
                TerminalUi.Info(
                    $"Preparing empty in-memory model for {displayName} " +
                    "(new exchanges have no Forma snapshot until after the first sync).");
                model = ElementDataModel.Create(ctx.Client);
            }
            else
            {
                var identifier = ToIdentifier(details, ctx.Folder?.HubId);

                try
                {
                    var loaded = await TryLoadModelWithStatusAsync(ctx, identifier, displayName);
                    if (loaded == null)
                    {
                        TerminalUi.Error("Could not load exchange into memory.");
                        return false;
                    }

                    model = loaded;
                }
                catch (Exception ex)
                {
                    TerminalUi.Error($"Failed to load exchange: {ex}");
                    return false;
                }
            }

            RegisterLoaded(ctx, details, model);
            return true;
        }

        internal static async Task<bool> CreateAndPrepareEmptyAsync(SampleContext ctx)
        {
            ctx.LastCreatedExchange = null;
            if (!await CreateNewExchangeAsync(ctx))
                return false;

            if (!await LoadFromDetailsAsync(ctx, ctx.LastCreatedExchange!, useEmptyModelForNewExchange: true))
                return false;

            ctx.ScenarioExchangeTitle = ctx.LastExchangeTitle;
            return true;
        }

        internal static void EndScenario(SampleContext ctx) => ctx.ScenarioExchangeTitle = null;

        /// <summary>Creates a new empty exchange in the session folder. Used by 2.1 Create Exchange and by scenarios that need a fresh exchange.</summary>
        internal static async Task<bool> CreateNewExchangeAsync(SampleContext ctx)
        {
            var folder = NavigationHelper.LoadFolderFromSession(ctx);
            if (folder == null)
                return false;

            if (ctx.Client is not Client client || client.SDKOptions?.HostingProvider == null)
                throw new InvalidOperationException("SDK client is not configured.");

            var fileName = Prompt.AskString("Exchange name", $"New Exchange_{DateTime.Now:yyyyMMdd_HHmmss}");
            if (string.IsNullOrWhiteSpace(fileName))
            {
                TerminalUi.Dim("Cancelled.");
                return false;
            }

            var request = new ExchangeCreateRequestACC
            {
                Host = client.SDKOptions.HostingProvider,
                Contract = client.SDKOptions.ContractProvider,
                FileName = fileName.Trim(),
                ACCFolderURN = folder.FolderUrn,
                ProjectId = folder.ProjectUrn,
                HubId = folder.HubId,
                Region = folder.Region,
                ProjectType = ProjectType.ACC,
                Description = string.Empty,
            };

            TerminalUi.Chat("Creating exchange...");
            var response = await ctx.Client.CreateExchangeAsync(request);
            if (response.IsFailed)
            {
                var message = response.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                TerminalUi.Error($"Failed: {message}");
                return false;
            }

            RememberCreated(ctx, response.Value, fileName);
            PrintCreated(response.Value);
            return true;
        }

        internal static void RegisterLoaded(SampleContext ctx, ExchangeDetails details, ElementDataModel model)
        {
            var title = details.DisplayName ?? details.FileUrn;
            ctx.Exchanges[title] = new ActiveExchange(details.FileUrn, model);
            RememberLoaded(
                ctx,
                title,
                details.FileUrn,
                details.ExchangeID,
                details.CollectionID,
                details.HubId ?? ctx.Folder?.HubId);

            var elementCount = model.Elements.Count();
            TerminalUi.WriteResultPanel(
                "Loaded",
                ("Name", title),
                ("Exchange", details.ExchangeID),
                ("File URN", details.FileUrn),
                ("Elements", elementCount.ToString()));
        }

        internal static void RememberCreated(SampleContext ctx, ExchangeDetails details, string? fallbackName = null)
        {
            ctx.LastCreatedExchange = details;
            ctx.LastExchangeTitle = details.DisplayName ?? fallbackName;
        }

        internal static void PrintCreated(ExchangeDetails details)
        {
            TerminalUi.Chat("Created:");
            TerminalUi.Chat($"  Name:       {details.DisplayName}");
            TerminalUi.Chat($"  Exchange:   {details.ExchangeID}");
            TerminalUi.Chat($"  Collection: {details.CollectionID}");
            TerminalUi.Chat($"  File URN:   {details.FileUrn}");
            TerminalUi.Chat($"  Version:    {details.FileVersionUrn}");
        }

        internal static void RemoveLoadedByFileUrn(SampleContext ctx, string fileUrn)
        {
            var loaded = ctx.Exchanges
                .Where(pair => string.Equals(pair.Value.ExchangeFileUrn, fileUrn, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .ToList();
            foreach (var title in loaded)
                ctx.Exchanges.Remove(title);

            ClearLastIfMatches(ctx, fileUrn);
        }

        private static async Task<ElementDataModel?> TryLoadModelWithStatusAsync(
            SampleContext ctx,
            DataExchangeIdentifier identifier,
            string displayName)
        {
            IResponse<IElementDataModel> response = default!;
            await TerminalUi.RunWithStatusAsync(
                $"Loading {displayName}…",
                async () => response = await ctx.Client.GetElementDataModelAsync(identifier).ConfigureAwait(false));

            return ToElementDataModel(response);
        }

        private static ElementDataModel? ToElementDataModel(IResponse<IElementDataModel> response)
        {
            if (response.IsFailed)
                return null;

            return response.Value as ElementDataModel;
        }

        internal static void RememberLoaded(
            SampleContext ctx,
            string title,
            string fileUrn,
            string exchangeId,
            string collectionId,
            string? hubId)
        {
            ctx.LastExchange = new LoadedExchangeInfo(title, fileUrn, exchangeId, collectionId, hubId);
            ctx.LastExchangeTitle = title;
        }

        internal static void ClearLast(SampleContext ctx)
        {
            ctx.LastExchange = null;
            ctx.LastExchangeTitle = null;
        }

        internal static void ClearLastIfMatches(SampleContext ctx, string fileUrn)
        {
            if (ctx.LastExchange != null
                && string.Equals(ctx.LastExchange.FileUrn, fileUrn, StringComparison.OrdinalIgnoreCase))
            {
                ClearLast(ctx);
            }
        }

        internal static async Task<bool> TryRestoreAsync(SampleContext ctx)
        {
            var info = ctx.LastExchange;
            if (info == null || ctx.Exchanges.ContainsKey(info.Title))
                return false;

            var identifier = new DataExchangeIdentifier
            {
                ExchangeId = info.ExchangeId,
                CollectionId = info.CollectionId,
                HubId = info.HubId ?? ctx.Folder?.HubId,
            };

            IResponse<IElementDataModel> response;
            try
            {
                response = await ctx.Client.GetElementDataModelAsync(identifier).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                TerminalUi.Warning($"Could not restore '{info.Title}': {ex}");
                ClearLast(ctx);
                return false;
            }

            if (response.IsFailed)
            {
                var message = response.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                TerminalUi.Warning($"Could not restore '{info.Title}': {message}");
                ClearLast(ctx);
                return false;
            }

            if (response.Value is not ElementDataModel model)
            {
                TerminalUi.Warning($"Could not restore '{info.Title}': unexpected data model type.");
                ClearLast(ctx);
                return false;
            }

            ctx.Exchanges[info.Title] = new ActiveExchange(info.FileUrn, model);
            ctx.LastExchangeTitle = info.Title;
            return true;
        }
    }
}
