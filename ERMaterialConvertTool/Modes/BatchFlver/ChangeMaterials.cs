using PPlus;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class BatchFlver
{
    public static bool ChangeMaterials(IDictionary<string, FLVER2> dict, EditFlver.GroupBy groupBy)
    {
        EditFlver.MatNameDecision matDecisionKeep = EditFlver.MatNameDecision.KeepOldMTD;
        string matNameKeep = "";
        while (true)
        {
            var mergedBank = MatInfoBank.GetMergedMatInfoBank();
            var erBank = MatInfoBank.GetERMatInfoBank();

            List<FLVER2.Material> mats;
            switch (groupBy)
            {
                case EditFlver.GroupBy.Shader:
                {
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
                            var matches = materialsByShader[shader];
                            var first = matches.First();
                            return $"{shader} ({matches.Count}x) | {first.Name} | {first.MTD}";
                        })
                        .Run();
                    if (matSelect.IsAborted) return false;
                    mats = materialsByShader[matSelect.Value];
                }
                    break;
                case EditFlver.GroupBy.MTD:
                {
                    var materialsByMtd = dict.Values.SelectMany(f => f.Materials)
                        .GroupBy(m => m.MTD.ToLower()).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    var matMtdSelect = PromptPlus.Select<string>("Select materials to process by material name")
                        .AddItems(materialsByMtd.Keys)
                        .TextSelector(mtd =>
                        {
                            var matches = materialsByMtd[mtd];
                            var first = matches.First();
                            var matchDef = mergedBank.MaterialDefs.Values.FirstOrDefault(d =>
                                Path.GetFileNameWithoutExtension(first.MTD).Equals(Path.GetFileNameWithoutExtension(mtd),
                                    StringComparison.InvariantCultureIgnoreCase));
                            var shader = matchDef?.Shader ?? "";
                            string[] components = [$"{matches.Count}x", first.Name, shader];
                            return $"{mtd} ({string.Join(" | ", components.OfType<string>())})";
                        })
                        .Run();
                    if (matMtdSelect.IsAborted) return false;
                    mats = materialsByMtd[matMtdSelect.Value];
                }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, null);
            }

            Dictionary<string, FLVER2> filteredDict =
                dict.Where(kvp => mats.Any(m => kvp.Value.Materials.Contains(m))).ToDictionary();
            PromptPlus.WriteLine($"Processing {filteredDict.Count} FLVERs...");
            EditFlver.ChangeMaterial(filteredDict, mats.First(), mergedBank, erBank, groupBy, true, ref matDecisionKeep,
                ref matNameKeep);
        }

        return true;
    }
}