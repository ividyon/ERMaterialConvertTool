using System.ComponentModel.DataAnnotations;
using PPlus;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class MatbinUtility
{
    internal enum BatchFilesMode
    {
        [Display(Name = "Select single file")] SelectSingleFile,
        [Display(Name = "Select files")] SelectFiles,
        [Display(Name = "Process files in folder")] SelectFolder,
        [Display(Name = "Process files in folder and subfolders")] SelectFolderRecursive
    }

    internal enum MatbinAction
    {
        [Display(Name = "Print used texture names")] PrintSamplers,
        [Display(Name = "Print used shaders")] PrintShaders,
    }

    public static void Perform()
    {

        var fileModeSelect = PromptPlus.Select<BatchFilesMode>("Select mode of operation")
            .Run();
        if (fileModeSelect.IsAborted) return;
        List<string> filePaths = new();
        switch (fileModeSelect.Value)
        {
            case BatchFilesMode.SelectSingleFile:
                PromptPlus.KeyPress("Next up, please select the file you wish to process.")
                    .Run();
                var filePicker = NativeFileDialogSharp.Dialog.FileOpen("matbin");
                if (filePicker == null || !filePicker.IsOk) return;
                filePaths = filePicker.Paths.ToList();
                break;
            case BatchFilesMode.SelectFiles:
                PromptPlus.KeyPress("Next up, please select the files you wish to process.")
                    .Run();
                var filesPicker = NativeFileDialogSharp.Dialog.FileOpenMultiple("matbin");
                if (filesPicker == null || !filesPicker.IsOk) return;
                filePaths = filesPicker.Paths.ToList();
                break;
            case BatchFilesMode.SelectFolder:
            case BatchFilesMode.SelectFolderRecursive:
                PromptPlus.KeyPress("Next up, please select the folder containing the files you wish to process.")
                    .Run();
                var folderPicker = NativeFileDialogSharp.Dialog.FolderPicker();
                if (folderPicker == null || !folderPicker.IsOk) return;
                filePaths = Directory.GetFiles(folderPicker.Path, "*.matbin",
                    fileModeSelect.Value == BatchFilesMode.SelectFolderRecursive
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly).ToList();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }


        while (true)
        {
            PromptPlus.WriteLine($"Loading {filePaths.Count} MATBINs...");
            Dictionary<string, MATBIN> dict = filePaths.ToDictionary(p => p, p => MATBIN.Read(p));

            var modeSelect = PromptPlus.Select<MatbinAction>("Select mode of operation").Run();
            if (modeSelect.IsAborted) return;

            switch (modeSelect.Value)
            {
                case MatbinAction.PrintSamplers:
                    var samplers = dict.Values.SelectMany(m => m.Samplers).Select(s => s.Path).Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).Distinct().ToList();
                    PromptPlus.WriteLine($"\n{string.Join("\n", samplers)}\n");
                    PromptPlus.KeyPress("Press any key to continue...");
                    break;
                case MatbinAction.PrintShaders:
                    var shaders = dict.Values.Select(m => Path.GetFileNameWithoutExtension(m.ShaderPath)).Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).Distinct().ToList();
                    PromptPlus.WriteLine($"\n{string.Join("\n", shaders)}\n");
                    PromptPlus.KeyPress("Press any key to continue...");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}