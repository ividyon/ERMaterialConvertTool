using PPlus;
using SoulsAssetPipeline.FLVERImporting;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class EditFlver
{
    public static bool RearrangeUVs(ref FLVER2 flver, ref string filePath)
    {
        var matInfoBank = MatInfoBank.GetMergedMatInfoBank();

        var f = flver;
        var viableMaterials = flver.Materials.Where(m =>
        {
            var idx = f.Materials.IndexOf(m);
            return f.Meshes.Any(mesh => mesh.MaterialIndex == idx && mesh.Vertices.First().UVs.Count > 1);
        }).ToList();

        bool rearrangeLoop = true;
        while (rearrangeLoop)
        {
            var select = PromptPlus.Select<FLVER2.Material>("Select material using multiple UVs")
                .AddItems(viableMaterials)
                .TextSelector(m => m.ToString(f, matInfoBank))
                .Run();
            if (select.IsAborted)
            {
                return false;
            }

            var matIdx = flver.Materials.IndexOf(select.Value);
            var meshes = flver.Meshes.Where(m => m.MaterialIndex == matIdx).ToList();
            var uvCount = meshes.First().Vertices.First().UVs.Count;
            var uvChoices = Enumerable.Range(0, uvCount).ToList();

            var targets = new List<int>();
            for (int i = 0; i < uvCount; i++)
            {
                var currentSelect = PromptPlus
                    .Select<int>(
                        $"Select UV to place at ID #{i} ({i + 1}/{uvCount})")
                    .AddItems(uvChoices)
                    .Default(i)
                    .TextSelector(c => $"#{c}")
                    .Config(o => o.EnabledAbortKey(false));

                if (uvChoices.Contains(i))
                {
                    currentSelect.Default(i);
                }

                var selectRun = currentSelect.Run();
                targets.Add(selectRun.Value);
            }

            PromptPlus.WriteLine("Applying new UV setup to all affected mesh vertices...");
            foreach (FLVER2.Mesh mesh in meshes)
            {
                foreach (FLVER.Vertex vertex in mesh.Vertices)
                {
                    vertex.UVs = targets.Select(u => vertex.UVs[u]).ToList();
                }
            }

            Program.SaveFlver(ref flver, ref filePath, true);
        }

        return true;
    }
}