using System.ComponentModel.DataAnnotations;
using PPlus;
using SoulsAssetPipeline.FLVERImporting;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class EditFlver
{
    private enum FlverDecision
    {
        [Display(Name = "Convert materials to shader")] ChangeMaterials,
        [Display(Name = "Auto-convert materials with known shaders")] BulkConvertExistingMaterials,
        [Display(Name = "Rearrange UVs")] RearrangeUVs,
        [Display(Name = "List used MATBINs")] ListUsedMatbins,
        // [Display(Name = "Remove motion blur")] RemoveMotionBlur,
        // [Display(Name = "Save FLVER")] Save,
        // [Display(Name = "Import and apply JSON")] Import,
        // [Display(Name = "Export JSON")] Export,
        [Display(Name = "Exit program")] Exit
    }

    public static void PrepFlver(ref FLVER2 flver)
    {

    }
    public static void Perform(ref FLVER2 flver, ref string filePath)
    {

                bool flverDecisionLoop = true;
                while (flverDecisionLoop)
                {
                    flver = Program.LoadFlver(ref filePath);
                    if (flver == null || filePath == null) return;

                    var editChoice = PromptPlus.Select<FlverDecision>("Select option").Run();
                    if (editChoice.IsAborted)
                    {
                        flverDecisionLoop = false;
                        continue;
                    }

                    switch (editChoice.Value)
                    {
                        case FlverDecision.Exit:
                            flverDecisionLoop = false;
                            continue;
                        case FlverDecision.ChangeMaterials:
                            ChangeMaterials(ref flver, ref filePath);
                            break;
                        case FlverDecision.BulkConvertExistingMaterials:
                            BulkConvertExistingMaterials(ref flver, ref filePath);
                            break;
                        case FlverDecision.RearrangeUVs:
                            RearrangeUVs(ref flver, ref filePath);
                            break;
                        case FlverDecision.ListUsedMatbins:
                            var mtds = flver.Materials.Select(m => Path.GetFileNameWithoutExtension(m.MTD))
                                .DistinctBy(m => m.ToLower()).OrderBy(m => m).ToList();
                            PromptPlus.WriteLine($"\n{string.Join("\n", mtds)}\n");
                            PromptPlus.KeyPress("Press any key to continue...");
                            continue;
                        // case FlverDecision.RemoveMotionBlur:
                        //     RemoveMotionBlur(ref flver, ref filePath);
                        //     break;
                        // case FlverDecision.Save:
                        //     Program.SaveFlver(ref flver, ref filePath);
                        //     break;
                        // case FlverDecision.Import:
                        //     PromptPlus.KeyPress("NYI").Run();
                        //     break;
                        // case FlverDecision.Export:
                        //     PromptPlus.KeyPress("NYI").Run();
                        //     break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
    }
}