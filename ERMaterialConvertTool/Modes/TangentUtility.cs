using System.ComponentModel.DataAnnotations;
using System.Numerics;
using PPlus;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static class TangentUtility
{
    internal enum BatchFilesMode
    {
        [Display(Name = "Select file(s)")] SelectFiles,
        [Display(Name = "Select all files in folder (and subfolders)")] SelectFolderRecursive
    }

    public static void Perform()
    {
        var confirm = PromptPlus.Confirm(
            @"This is a utility tool for fixing wrong tangents in meshes, which lead to lighting differences from vanilla.
These wrong tangents occur when importing custom or edited meshes using FLVER Editor or older versions of FBX Importer.

This tool does NOT make backups of your files! User beware!

The tool allows selecting FLVERs as well as BND archives.

Would you like to continue?").Run();
        if (confirm.IsAborted || confirm.Value.IsNoResponseKey()) return;

        var fileModeSelect = PromptPlus.Select<BatchFilesMode>("Select mode of operation")
            .Run();
        if (fileModeSelect.IsAborted) return;
        List<string> filePaths = new();
        switch (fileModeSelect.Value)
        {
            case BatchFilesMode.SelectFiles:
                PromptPlus.KeyPress("Next up, please select the FLVERs or BNDs you wish to process.")
                    .Run();
                var filesPicker = NativeFileDialogSharp.Dialog.FileOpenMultiple();
                if (filesPicker == null || !filesPicker.IsOk) return;
                filePaths = filesPicker.Paths.ToList();
                break;
            case BatchFilesMode.SelectFolderRecursive:
                PromptPlus.KeyPress(
                        "Next up, please select the folder containing the FLVERs or BNDs you wish to process.")
                    .Run();
                var folderPicker = NativeFileDialogSharp.Dialog.FolderPicker();
                if (folderPicker == null || !folderPicker.IsOk) return;
                filePaths = Directory.GetFiles(folderPicker.Path, "*",
                    SearchOption.AllDirectories).ToList();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        PromptPlus.WriteLine($"Loading {filePaths.Count} files...");

        var flverPaths = filePaths.Where(p => Path.GetExtension(p).ToLower() == ".flver").ToList();
        var bndPaths = filePaths.Where(p => Path.GetFileName(p).ToLower().Contains("bnd")).ToList();

        bool FlverCallback(FLVER2 flver)
        {
            var sel = PromptPlus.MultiSelect<FLVER2.Mesh>("Select meshes to process")
                .AddItems(flver.Meshes)
                .TextSelector(m => $"#{flver.Meshes.IndexOf(m)} {flver.Materials[m.MaterialIndex].Name} | {Path.GetFileName(flver.Materials[m.MaterialIndex].MTD)}")
                .Run();
            foreach (FLVER2.Mesh mesh in sel.Value)
            {
                var mat = flver.Materials[mesh.MaterialIndex];
                foreach (FLVER.Vertex vertex in mesh.Vertices)
                {
                    for (int i = 0; i < vertex.Tangents.Count; i++)
                    {
                        Vector4 tangent = vertex.Tangents[i];
                        Vector3 normal = vertex.Normal;
                        Vector3 bitangent = tangent.W * Vector3.Cross(tangent.AsVector3(), normal);
                        vertex.Tangents[i] = -(bitangent.AsVector4() with { W = tangent.W });
                        if (mat.Name.ToLower().Contains("cloth") || mat.MTD.ToLower().Contains("cloth"))
                        {
                            vertex.Bitangent = new(tangent.W, -1, -1, -1);
                        }
                    }
                }
            }

            return true;
        }

        foreach (string flverPath in flverPaths)
        {
            PromptPlus.WriteLine($"Reading {flverPath}...");
            var flver = FLVER2.Read(flverPath);
            if (!FlverCallback(flver))
            {
                continue;
            }
            PromptPlus.WriteLine($"Writing {flverPath}...");
            flver.Write(flverPath);
        }

        foreach (string bndPath in bndPaths)
        {
            PromptPlus.WriteLine($"Reading {bndPath}...");
            var bnd = BND4.Read(bndPath);
            bool changes = false;
            foreach (BinderFile file in bnd.Files.Where(f => f.Name.ToLower().EndsWith(".flver")))
            {
                var flver = FLVER2.Read(file.Bytes);
                if (!FlverCallback(flver))
                {
                    continue;
                }
                file.Bytes = flver.Write();
                changes = true;
            }
            if (changes)
            {
                PromptPlus.WriteLine($"Writing {bndPath}...");
                bnd.Write(bndPath);
            }
        }
    }
}