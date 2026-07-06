using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using PPlus;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class BatchFlver
{
    internal enum BatchFilesMode
    {
        [Display(Name = "Select file(s)")] SelectFiles,
        [Display(Name = "Select all files in folder (and subfolders)")] SelectFolderRecursive
    }

    internal enum BatchOperationMode
    {
        [Display(Name = "Convert FLVER to ER")] ConvertToER,
        [Display(Name = "Change materials by shader")] ChangeMaterialsByShader,
        [Display(Name = "Change materials by material name")] ChangeMaterialsByMTD,
        [Display(Name = "Auto-convert materials with known shaders")] BulkConvertExistingMaterials,
        [Display(Name = "List MATBINs used in the files")] ListUsedMatbins,
    }
    public static void Perform()
    {
        var fileModeSelect = PromptPlus.Select<BatchFilesMode>("Select mode of operation")
            .Run();
        if (fileModeSelect.IsAborted) return;
        List<string> flverPaths = new();
        switch (fileModeSelect.Value)
        {
            case BatchFilesMode.SelectFiles:
                PromptPlus.KeyPress("Next up, please select the files you wish to process.")
                    .Run();
                var filesPicker = NativeFileDialogSharp.Dialog.FileOpenMultiple("flver");
                if (filesPicker == null || !filesPicker.IsOk) return;
                flverPaths = filesPicker.Paths.ToList();
                break;
            case BatchFilesMode.SelectFolderRecursive:
                PromptPlus.KeyPress("Next up, please select the folder containing the files you wish to process.")
                    .Run();
                var folderPicker = NativeFileDialogSharp.Dialog.FolderPicker();
                if (folderPicker == null || !folderPicker.IsOk) return;
                flverPaths = Directory.GetFiles(folderPicker.Path, "*.flver", SearchOption.AllDirectories).ToList();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        bool load = true;
        ConcurrentDictionary<string, FLVER2> dict = new();
        while (true)
        {
            if (load)
            {
                dict = new();
                PromptPlus.WriteLine($"Loading {flverPaths.Count} FLVERs...");
                Parallel.ForEach(flverPaths, p =>
                {
                    dict.TryAdd(p, FLVER2.Read(p));
                });
            }
            load = false;

            var modeSelect = PromptPlus.Select<BatchOperationMode>("Select mode of operation").Run();
            if (modeSelect.IsAborted) return;

            switch (modeSelect.Value)
            {
                case BatchOperationMode.ConvertToER:
                    load = ConvertToER(dict);
                    break;
                case BatchOperationMode.ChangeMaterialsByShader:
                    load = ChangeMaterials(dict, EditFlver.GroupBy.Shader);
                    break;
                case BatchOperationMode.ChangeMaterialsByMTD:
                    load = ChangeMaterials(dict, EditFlver.GroupBy.MTD);
                    break;
                case BatchOperationMode.BulkConvertExistingMaterials:
                    load = BulkConvertMaterials(dict);
                    break;
                case BatchOperationMode.ListUsedMatbins:
                    var mtds = dict.SelectMany(kvp => kvp.Value.Materials.Select(m => Path.GetFileNameWithoutExtension(m.MTD)))
                        .DistinctBy(m => m.ToLower()).OrderBy(m => m).ToList();
                    PromptPlus.WriteLine($"\n{string.Join("\n", mtds)}\n");
                    PromptPlus.KeyPress("Press any key to continue...");
                    continue;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}