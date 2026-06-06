using System.ComponentModel.DataAnnotations;
using PPlus;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class BatchFlver
{
    internal enum BatchFilesMode
    {
        [Display(Name = "Select files")] SelectFiles,
        [Display(Name = "Process files in folder")] SelectFolder,
        [Display(Name = "Process files in folder and subfolders")] SelectFolderRecursive
    }

    internal enum BatchOperationMode
    {
        [Display(Name = "Convert FLVER to ER")] ConvertToER,
        [Display(Name = "Change materials by shader")] ChangeMaterials,
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
            case BatchFilesMode.SelectFolder:
            case BatchFilesMode.SelectFolderRecursive:
                PromptPlus.KeyPress("Next up, please select the folder containing the files you wish to process.")
                    .Run();
                var folderPicker = NativeFileDialogSharp.Dialog.FolderPicker();
                if (folderPicker == null || !folderPicker.IsOk) return;
                flverPaths = Directory.GetFiles(folderPicker.Path, "*.flver", fileModeSelect.Value == BatchFilesMode.SelectFolderRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        while (true)
        {
            PromptPlus.WriteLine($"Loading {flverPaths.Count} FLVERs...");
            Dictionary<string, FLVER2> dict = flverPaths.ToDictionary(p => p, p => FLVER2.Read(p));

            var modeSelect = PromptPlus.Select<BatchOperationMode>("Select mode of operation").Run();
            if (modeSelect.IsAborted) return;

            switch (modeSelect.Value)
            {
                case BatchOperationMode.ConvertToER:
                    ConvertToER(dict);
                    break;
                case BatchOperationMode.ChangeMaterials:
                    ChangeMaterials(dict);
                    break;
                case BatchOperationMode.BulkConvertExistingMaterials:
                    BulkConvertMaterials(dict);
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