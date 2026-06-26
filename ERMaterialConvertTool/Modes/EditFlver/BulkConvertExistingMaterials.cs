using PPlus;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class EditFlver
{
    public static bool BulkConvertExistingMaterials(ref FLVER2 flver, ref string filePath)
    {
        var f = flver;
        PromptPlus.WriteLine(
            "This tool will automatically convert every material which has a fitting ER shader, without touching the MATBIN name.");

        var allMatInfoBank = MatInfoBank.GetMergedMatInfoBank();
        var erMatInfoBank = MatInfoBank.GetERMatInfoBank();

        var materials = flver.Materials!;

        var matchingMaterials = materials.Where(material =>
        {
            var mtd = material.MTD;
            var originalMatDef =
                allMatInfoBank.MaterialDefs.Values.FirstOrDefault(d =>
                    d.MTD.Equals(mtd, StringComparison.CurrentCultureIgnoreCase));

            if (originalMatDef == null) return false;
            var matchDefs = erMatInfoBank.MaterialDefs.Values
                .Where(d => d.Shader != null &&
                            d.Shader.Equals(originalMatDef.Shader, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            return matchDefs.Any();
        }).ToList();

        var diff = materials.Count - matchingMaterials.Count;
        if (diff > 0)
        {
            PromptPlus.WriteLine($"{diff} materials do not have a matching ER shader.");
        }

        var multiSelect = PromptPlus.MultiSelect<FLVER2.Material>("Select materials to bulk convert")
            .AddItems(matchingMaterials)
            .AddDefault(matchingMaterials)
            .TextSelector(m => m.ToString(f, allMatInfoBank).PromptPlusEscape() )
            .Run();
        if (multiSelect.IsAborted) return false;

        matchingMaterials = multiSelect.Value.ToList();

        PromptPlus.WriteLine("Will bulk convert materials:");
        foreach (FLVER2.Material material in matchingMaterials)
        {
            PromptPlus.WriteLine($"- {material.ToString(f, allMatInfoBank).PromptPlusEscape()}");
        }
        var confirm = PromptPlus.Confirm("Proceed?")
            .Config(o => o.EnabledAbortKey(false))
            .Run();
        if (confirm.IsAborted || confirm.Value.IsNoResponseKey()) return false;

        MatNameDecision decision = MatNameDecision.KeepOldMTD;
        string? name = null;
        foreach (FLVER2.Material material in matchingMaterials)
        {
            ChangeMaterial(flver, filePath, material, allMatInfoBank, erMatInfoBank, true, ref decision, ref name, true);
        }
        Program.SaveFlver(ref flver, ref filePath, true);

        return true;
    }
}