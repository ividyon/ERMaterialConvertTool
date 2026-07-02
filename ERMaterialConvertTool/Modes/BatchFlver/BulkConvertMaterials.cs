using PPlus;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class BatchFlver
{
    public static bool BulkConvertMaterials(IDictionary<string, FLVER2> dict)
    {
        PromptPlus.WriteLine(
            "This tool will automatically convert every material which has a fitting ER shader, without touching the MATBIN name.");

        var allMatInfoBank = MatInfoBank.GetMergedMatInfoBank();
        var erMatInfoBank = MatInfoBank.GetERMatInfoBank();
        var erShaders = erMatInfoBank.MaterialDefs.Values.Where(d => d.Shader != null).Select(d => d.Shader.ToLower()).Distinct().ToList();

        var allMaterialsByShader = dict.Values.SelectMany(f => f.Materials)
            .GroupBy(m =>
                allMatInfoBank.MaterialDefs.Values.FirstOrDefault(md =>
                {
                    return md.MTD.Equals(m.MTD, StringComparison.InvariantCultureIgnoreCase);
                })?.Shader.ToLower()).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .ToDictionary(g => g.Key!, g => g.ToList());

        var materialsByShader = allMaterialsByShader
            .Where(g => erShaders.Contains(g.Key)).ToDictionary();

        var diff = allMaterialsByShader.Count - materialsByShader.Count;
        if (diff > 0)
        {
            var diffMaterials = allMaterialsByShader.Except(materialsByShader).SelectMany(d => d.Value).ToList();
            var diffFlvers = dict.Where(kvp => kvp.Value.Materials.Any(m => diffMaterials.Contains(m))).Select(a => a.Key).ToList();
            PromptPlus.WriteLine($"{diff} shaders do not exist in ER.");
            PromptPlus.WriteLine($"They are located in FLVERs:\n\n{string.Join("\n", diffFlvers.Select(a => $"- {Path.GetFileNameWithoutExtension(a)}"))}\n");
        }

        PromptPlus.WriteLine($"Breakdown of shaders:");
        foreach ((string shaderName, List<FLVER2.Material> materials) in materialsByShader)
        {
            PromptPlus.WriteLine($"- {shaderName} ({string.Join($", ", materials.DistinctBy(m => m.Name).Select(a => a.Name))})");
        }
        var multiSelect = PromptPlus.MultiSelect<string>("Select shaders to bulk convert")
            .AddItems(materialsByShader.Keys)
            .AddDefault(materialsByShader.Keys)
            .TextSelector(shader =>
            {
                var mats = materialsByShader[shader];
                var first = mats.First();
                return $"{shader} ({mats.Count}x) | {first.Name} | {first.MTD}";
            } )
            .Run();
        if (multiSelect.IsAborted) return false;
        var selectedShaders = multiSelect.Value.ToList();
        var matchingShaders = materialsByShader.Where(kvp => selectedShaders.Contains(kvp.Key)).ToDictionary();

        PromptPlus.WriteLine("Will bulk convert shaders:");
        foreach ((string shader, List<FLVER2.Material> _) in matchingShaders)
        {
            var mats = materialsByShader[shader];
            var first = mats.First();
            PromptPlus.WriteLine($"- {shader} ({mats.Count}x) | {first.Name} | {first.MTD}");
        }
        var confirm = PromptPlus.Confirm("Proceed?")
            .Config(o => o.EnabledAbortKey(false))
            .Run();
        if (confirm.IsAborted || confirm.Value.IsNoResponseKey()) return false;

        EditFlver.MatNameDecision decision = EditFlver.MatNameDecision.KeepOldMTD;
        string? name = null;
        Dictionary<string, FLVER2> saveDict = new();
        foreach ((string shader, List<FLVER2.Material> materials) in matchingShaders)
        {
            var filteredDict = dict.Where(kvp => materials.Any(m => kvp.Value.Materials.Contains(m))).ToDictionary();
            foreach (KeyValuePair<string, FLVER2> pair in filteredDict)
            {
                saveDict.TryAdd(pair.Key, pair.Value);
            }

            EditFlver.ChangeMaterial(filteredDict, materials.First(), allMatInfoBank, erMatInfoBank, EditFlver.GroupBy.Shader, true, ref decision, ref name);
        }
        Program.SaveFlvers(saveDict, true);

        return true;
    }
}