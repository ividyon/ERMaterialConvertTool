using PPlus;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class EditFlver
{
    public static bool BulkConvertMaterials(ref FLVER2 flver, ref string filePath)
    {
        PromptPlus.WriteLine(
            "This tool will automatically convert every material which has a fitting ER shader, without touching the MATBIN name.");

        var allMatInfoBank = MatInfoBank.GetMergedMatInfoBank();
        var erMatInfoBank = MatInfoBank.GetERMatInfoBank();

        var materials = flver.Materials!;

        var matchingMaterials = materials.Where(material =>
        {
            var mtd = Path.GetFileNameWithoutExtension(material.MTD).ToLower();
            var originalMatDef =
                allMatInfoBank.MaterialDefs.Values.FirstOrDefault(d =>
                    Path.GetFileNameWithoutExtension(d.MTD).Equals(mtd, StringComparison.CurrentCultureIgnoreCase));

            if (originalMatDef == null) return false;
            var matchDefs = erMatInfoBank.MaterialDefs.Values
                .Where(d => d.Shader != null &&
                            d.Shader.Equals(originalMatDef.Shader, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            return matchDefs.Any();
        }).ToList();

        var multiSelect = PromptPlus.MultiSelect<FLVER2.Material>("Select materials to bulk convert")
            .AddItems(matchingMaterials)
            .AddDefault(matchingMaterials)
            .TextSelector(m => m.ToString(materials.IndexOf(m), allMatInfoBank).PromptPlusEscape() )
            .Run();
        if (multiSelect.IsAborted) return false;

        matchingMaterials = multiSelect.Value.ToList();

        PromptPlus.WriteLine("Will bulk convert materials:");
        foreach (FLVER2.Material material in matchingMaterials)
        {
            PromptPlus.WriteLine($"- {material.ToString(materials.IndexOf(material), allMatInfoBank).PromptPlusEscape()}");
        }
        var confirm = PromptPlus.Confirm("Proceed?")
            .Config(o => o.EnabledAbortKey(false))
            .Run();
        if (confirm.IsAborted || confirm.Value.IsNoResponseKey()) return false;

        MatNameDecision decision = MatNameDecision.KeepOldMTD;
        string? name = null;
        foreach (FLVER2.Material material in matchingMaterials)
        {
            ChangeMaterial(flver, filePath, material, allMatInfoBank, erMatInfoBank, ref decision, ref name, true);
        }
        Program.SaveFlver(ref flver, ref filePath, true);

        return true;
    }
}