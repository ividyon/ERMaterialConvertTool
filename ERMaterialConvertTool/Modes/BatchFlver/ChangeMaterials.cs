using PPlus;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class BatchFlver
{
    public static bool ChangeMaterials(IDictionary<string, FLVER2> dict)
    {
        EditFlver.MatNameDecision matDecisionKeep = EditFlver.MatNameDecision.KeepOldMTD;
        string matNameKeep = "";
        while (true)
        {
            var mergedBank = MatInfoBank.GetMergedMatInfoBank();
            var erBank = MatInfoBank.GetERMatInfoBank();

            // var materialsByMtd = dict.Values.SelectMany(f => f.Materials)
            //     .GroupBy(m => m.MTD.ToLower()).ToDictionary(g => g.Key, g => g.ToList());

            var materialsByShader = dict.Values.SelectMany(f => f.Materials)
                .GroupBy(m =>
                    mergedBank.MaterialDefs.Values.FirstOrDefault(md =>
                        md.MTD.Equals(m.MTD, StringComparison.InvariantCultureIgnoreCase))?.Shader.ToLower())
                .Where(g => g.Key != null).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                .ToDictionary(g => g.Key!, g => g.ToList());

            var matSelect = PromptPlus.Select<string>("Select materials to process by shader")
                .AddItems(materialsByShader.Keys)
                .TextSelector(shader =>
                {
                    var mats = materialsByShader[shader];
                    var first = mats.First();
                    return $"{shader} ({mats.Count}x) | {first.Name} | {first.MTD}";
                })
                .Run();
            if (matSelect.IsAborted) return false;

            var mats = materialsByShader[matSelect.Value];

            var filteredDict = dict.Where(kvp => mats.Any(m => kvp.Value.Materials.Contains(m))).ToDictionary();
            PromptPlus.WriteLine($"Processing {filteredDict.Count} FLVERs containing materials with that shader...");
            EditFlver.ChangeMaterial(filteredDict, mats.First(), mergedBank, erBank, false, ref matDecisionKeep,
                ref matNameKeep, true);
        }
        return true;
    }
}