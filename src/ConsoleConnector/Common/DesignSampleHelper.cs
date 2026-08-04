using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.DataExchange.DataModels;
using Autodesk.DataExchange.Interface;
using ConsoleConnector.Driver;
using ConsoleConnector.Samples;

namespace ConsoleConnector.Common
{
    internal static class DesignSampleHelper
    {
        internal static async Task<ElementSampleSession?> BeginAsync(SampleContext ctx) =>
            await ElementSampleHelper.BeginAsync(ctx);
            internal static IElement CreateDefinitionWithMesh(ElementDataModel model, string elementId, string name)
        {
            var def = model.AddElement(elementId, name);
            ElementSampleHelper.ClassifyGeneric(model, def);
            var mesh = GeometrySampleHelper.CreateSampleMesh();
            var geometry = ElementDataModel.CreateMeshGeometry(mesh, $"{name}_Mesh");
            model.SetElementGeometry(def, new List<IElementGeometry> { geometry });
            return def;
        }

        internal static IElement CreateInstance(ElementDataModel model, string elementId, string name)
        {
            var instance = model.AddElement(elementId, name);
            ElementSampleHelper.ClassifyGeneric(model, instance);
            return instance;
        }

        internal static IDesign CreateDesignRef(ElementDataModel model, IElement def, string designName, string designId) =>
            model.CreateDesignRef(def, designName, designId);
            internal static void PrintDesignSummary(ElementDataModel model, IDesign design)
        {
            var instances = model.GetDesignInstances(design).ToList();
            TerminalUi.Chat($"  Design: {design.Name} (id: {design.ID})");
            TerminalUi.Chat($"  Instances: {instances.Count}");
            foreach (var instance in instances)
                TerminalUi.Chat($"    {instance.Name} ({instance.Id})");
        }

        internal static (string DesignName, string DesignId) PromptDesignIdentity(string? defaultName = null, string? defaultId = null)
        {
            var name = Prompt.AskString("Design name", defaultName ?? "Chair");
            var id = Prompt.AskString("Design id", defaultId ?? "chair-id");
            return (name, id);
        }
    }
}
