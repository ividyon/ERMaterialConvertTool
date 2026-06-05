using System.ComponentModel.DataAnnotations;
using PPlus;
using SoulsAssetPipeline.FLVERImporting;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class EditFlver
{
    private enum MaterialDecision
    {
        [Display(Name = "Change material")]
        ChangeMaterial,

        // [Display(Name = "Change to NR material")] ChangeToNRMaterial,

        // [Display(Name = "Edit facesets")] EditFacesets,
    }

    private static void ChangeMaterials(ref FLVER2 flver, ref string filePath)
    {
        bool materialLoop = true;
        MatNameDecision matNameDecisionKeep = 0;
        string? name = null;
        while (materialLoop)
        {
            flver = Program.LoadFlver(ref filePath!)!;
            var materials = flver.Materials!;
            var mergedBank = MatInfoBank.GetMergedMatInfoBank();

            var matSelect = PromptPlus
                .Select<FLVER2.Material>("Select a material (press Esc to go back)")
                .AddItems(flver.Materials)
                .TextSelector(m => m.ToString(materials.IndexOf(m), mergedBank).PromptPlusEscape())
                .Run();
            if (matSelect.IsAborted)
            {
                materialLoop = false;
                continue;
            }

            FLVER2.Material material = matSelect.Value;

            var materialDecision =
                PromptPlus.Select<MaterialDecision>("Choose action to perform (press Esc to go back)").Run();
            if (materialDecision.IsAborted)
                continue;

            FLVER2MaterialInfoBank matInfoBank;
            switch (materialDecision.Value)
            {
                case MaterialDecision.ChangeMaterial:
                    matInfoBank = MatInfoBank.GetERMatInfoBank();
                    if (ChangeMaterial(flver, filePath, material, mergedBank, matInfoBank, ref matNameDecisionKeep, ref name))
                    {
                        PromptPlus.WriteLine("");
                        PromptPlus.WriteLine("Material conversion complete.");
                        PromptPlus.WriteLine("");
                    }
                    break;
                // case MaterialDecision.ChangeToNRMaterial:
                //     matInfoBank = MatInfoBank.GetNRMatInfoBank();
                //     if (ChangeMaterial(flver, filePath, material, mergedBank, matInfoBank, ref matNameDecisionKeep))
                //     {
                //         PromptPlus.WriteLine();
                //         PromptPlus.WriteLine("Material conversion complete.");
                //         PromptPlus.WriteLine();
                //     }
                //     break;
                // case MaterialDecision.EditFacesets:
                //     PromptPlus.KeyPress("NYI").Run();
                //     continue;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}