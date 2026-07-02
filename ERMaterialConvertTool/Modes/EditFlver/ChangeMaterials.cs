using System.ComponentModel.DataAnnotations;
using PPlus;
using SoulsAssetPipeline.FLVERImporting;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class EditFlver
{
    private static void ChangeMaterials(ref FLVER2 flver, ref string filePath)
    {
        bool materialLoop = true;
        MatNameDecision matNameDecisionKeep = 0;
        string? name = null;
        bool firstTime = true;
        while (materialLoop)
        {
            if (!firstTime)
            {
                flver = Program.LoadFlver(ref filePath!)!;
            }

            var f = flver;
            firstTime = false;
            var materials = flver.Materials!;
            var mergedBank = MatInfoBank.GetMergedMatInfoBank();

            var matSelect = PromptPlus
                .Select<FLVER2.Material>("Select a material (press Esc to go back)")
                .AddItems(flver.Materials)
                .TextSelector(m => m.ToString(f, mergedBank).PromptPlusEscape())
                .Run();
            if (matSelect.IsAborted)
            {
                materialLoop = false;
                continue;
            }

            FLVER2.Material material = matSelect.Value;

            var materialDecision =
                PromptPlus.Select<GroupBy>("Choose action to perform (press Esc to go back)").Run();
            if (materialDecision.IsAborted)
                continue;

            FLVER2MaterialInfoBank matInfoBank;
            switch (materialDecision.Value)
            {
                case GroupBy.Single:
                    matInfoBank = MatInfoBank.GetERMatInfoBank();
                    if (ChangeMaterial(flver, filePath, material, mergedBank, matInfoBank, GroupBy.Single, false, ref matNameDecisionKeep, ref name))
                    {
                        PromptPlus.WriteLine("");
                        PromptPlus.WriteLine("Material conversion complete.");
                        PromptPlus.WriteLine("");
                    }
                    break;
                case GroupBy.Shader:
                    matInfoBank = MatInfoBank.GetERMatInfoBank();
                    if (ChangeMaterial(flver, filePath, material, mergedBank, matInfoBank, GroupBy.Shader, false, ref matNameDecisionKeep, ref name))
                    {
                        PromptPlus.WriteLine("");
                        PromptPlus.WriteLine("Materials conversion complete.");
                        PromptPlus.WriteLine("");
                    }
                    break;
                case GroupBy.MTD:
                    matInfoBank = MatInfoBank.GetERMatInfoBank();
                    if (ChangeMaterial(flver, filePath, material, mergedBank, matInfoBank, GroupBy.MTD, false, ref matNameDecisionKeep, ref name))
                    {
                        PromptPlus.WriteLine("");
                        PromptPlus.WriteLine("Materials conversion complete.");
                        PromptPlus.WriteLine("");
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}