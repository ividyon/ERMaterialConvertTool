using System.ComponentModel.DataAnnotations;
using PromptPlusLibrary;
using SoulsAssetPipeline.FLVERImporting;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class EditFlver
{
    private enum FlverDecision
    {
        [Display(Name = "Change a material")] ChangeMaterials,
        [Display(Name = "Bulk convert materials")] BulkConvertMaterials,
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

                    var editChoice = PromptPlus.Controls.Select<FlverDecision>("Select option: ").Run();
                    if (editChoice.IsAborted)
                    {
                        flverDecisionLoop = false;
                        continue;
                    }

                    switch (editChoice.Content)
                    {
                        case FlverDecision.Exit:
                            flverDecisionLoop = false;
                            continue;
                        case FlverDecision.ChangeMaterials:
                            ChangeMaterials(ref flver, ref filePath);
                            break;
                        case FlverDecision.BulkConvertMaterials:
                            BulkConvertMaterials(ref flver, ref filePath);
                            break;
                        // case FlverDecision.Save:
                        //     Program.SaveFlver(ref flver, ref filePath);
                        //     break;
                        // case FlverDecision.Import:
                        //     PromptPlus.Controls.KeyPress("NYI").Run();
                        //     break;
                        // case FlverDecision.Export:
                        //     PromptPlus.Controls.KeyPress("NYI").Run();
                        //     break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
    }
}