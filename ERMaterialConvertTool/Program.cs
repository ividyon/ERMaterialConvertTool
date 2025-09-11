using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using PPlus;
using SoulsAssetPipeline.FLVERImporting;
using SoulsFormats;

namespace ERMaterialSwapTool;

class Program
{
    private enum Mode
    {
        [Display(Name = "Convert FLVER from another game")]
        ConvertFlver,

        [Display(Name = "Swap a material for another")]
        MaterialSwap,
    }

    public static bool IsDebug()
    {
        return Debugger.IsAttached;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory)!, "crash.log"),
            exception?.InnerException?.ToString() ?? exception?.ToString());
        throw exception;
    }

    private static void RunProgram(string[] args)
    {
        PromptPlus.DoubleDash("ERMaterialConvertTool");
        PromptPlus.WriteLine(
            "Hi! This is a quick tool for changing materials in an ELDEN RING FLVER2 file,\nor converting FLVER2 files from other games to ELDEN RING.");
        PromptPlus.WriteLine();

        var modeSelect = PromptPlus.Select<Mode>("Select mode of operation").Run();
        if (modeSelect.IsAborted) return;

        var mode = modeSelect.Value;

        PromptPlus.KeyPress("In the next dialog, please select the FLVER file").Run();
        string filePath;
        while (true)
        {
            var picker = NativeFileDialogSharp.Dialog.FileOpen();
            if (!picker.IsOk)
            {
                PromptPlus.KeyPress("Invalid file path! Try again...").Run();
                continue;
            }

            filePath = picker.Path;
            break;
        }

        PromptPlus.WriteLine("Loading FLVER...");
        var flver = FLVER2.Read(filePath);

        PromptPlus.WriteLine("Loading material bank...");
        string matInfoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SapResources",
            "FLVER2MaterialInfoBank", "BankER.xml");
        var matInfoBank = FLVER2MaterialInfoBank.ReadFromXML(matInfoPath);

        bool changesMade = false;
        bool exitAndSave = false;
        if (mode == Mode.ConvertFlver)
        {
            PromptPlus.WriteLine("Preparing FLVER...");
            var flverVersion = flver.Header.Version;
            bool isPreEldenRing = flverVersion < 0x2001A;
            bool isNightreign = flverVersion == 0x20021;
            flver.Header.Version = 0x2001A;
            if (isPreEldenRing)
            {
                PromptPlus.WriteLine("FLVER is pre-ELDEN RING; adding a skeleton set.");
                flver.Skeletons = new FLVER2.SkeletonSet();
            }

            if (isNightreign)
            {
                PromptPlus.WriteLine("FLVER is from Nightreign; changing some header values.");
                flver.Header.Unk68 = 4;
            }

            flver.BufferLayouts = new();
            flver.GXLists = new();

            Dictionary<string, FLVER2MaterialInfoBank.MaterialDef> mapping = new();
            PromptPlus.WriteLine(
                $"Materials:\n{string.Join("\n", flver.Materials.Select(a => $"#{flver.Materials.IndexOf(a)}: {a.Name} ({a.MTD}) Index {a.Index}"))}\n");
            foreach (string mtd in flver.Materials.Select(a => Path.GetFileNameWithoutExtension(a.MTD).ToLower())
                         .Distinct())
            {
                var select = PromptPlus.Select<FLVER2MaterialInfoBank.MaterialDef>($"Select ER material to map to {mtd}")
                    .TextSelector(a => $"{a.MTD} ({a.Shader})")
                    .AddItems(matInfoBank.MaterialDefs.Values);

                var selectPrompt = select.Run();

                mapping[mtd] = selectPrompt.Value;
            }

            foreach (FLVER2.Material flverMaterial in flver.Materials)
            {
                var matIndex = flver.Materials.IndexOf(flverMaterial);
                PromptPlus.WriteLine(
                    $"Processing material #{matIndex}: {Path.GetFileNameWithoutExtension(flverMaterial.Name)}");
                var matName = Path.GetFileNameWithoutExtension(flverMaterial.MTD).ToLower();

                FLVER2MaterialInfoBank.MaterialDef matDef = mapping[matName];

                FLVER2.GXList gxList = new();
                gxList.AddRange(matInfoBank.GetDefaultGXItemsForMTD(matDef.MTD));
                flver.GXLists.Add(gxList);
                flverMaterial.MTD = matDef.MTD;
                if (isPreEldenRing)
                    flverMaterial.Index = matIndex;
                flverMaterial.GXIndex = flver.GXLists.IndexOf(gxList);

                flverMaterial.Textures =
                    matDef.TextureChannels.Values.Select(x => new FLVER2.Texture { ParamName = x }).ToList();

                var meshes = flver.Meshes.Where(a => a.MaterialIndex == matIndex).ToList();
                var firstMesh = meshes.First();
                var acceptableBufferDeclarations = matDef.AcceptableVertexBufferDeclarations;
                List<FLVER2.BufferLayout> bufferLayouts = acceptableBufferDeclarations[0].Buffers;
                if (acceptableBufferDeclarations.Count > 1)
                {
                    List<FLVER2.BufferLayout>? matchingLayouts = acceptableBufferDeclarations.FirstOrDefault(x =>
                        x.Buffers.SelectMany(y => y).Count(y => y.Semantic == FLVER.LayoutSemantic.Tangent) >=
                        firstMesh.Vertices.First().Tangents.Count)?.Buffers;

                    if (matchingLayouts != null)
                    {
                        // Log.Log("Replace with matching layouts");
                        bufferLayouts = matchingLayouts;
                    }
                }

                List<int> layoutIndices = FlverUtils.GetLayoutIndices(flver, bufferLayouts);
                foreach (FLVER2.Mesh mesh in meshes)
                {
                    mesh.VertexBuffers = layoutIndices.Select(x => new FLVER2.VertexBuffer(x)).ToList();
                    foreach (var v in mesh.Vertices)
                    {
                        FlverUtils.PadVertex(v, bufferLayouts);
                    }
                }

                FlverUtils.AdjustBoneIndexBufferSize(flver, bufferLayouts);
            }

            PromptPlus.WriteLine();
            PromptPlus.WriteLine($"FLVER conversion complete.");
            PromptPlus.WriteLine();
            exitAndSave = true;
        }
        else if (mode == Mode.MaterialSwap)
        {
            while (true)
            {
                if (changesMade)
                {
                    var confirm = PromptPlus.Confirm("Swap more materials? (Select \"No\" to save your changes)").Run();
                    if (confirm.Value.IsNoResponseKey())
                    {
                        exitAndSave = true;
                        break;
                    }
                }

                var matSelect = PromptPlus
                    .Select<FLVER2.Material>("Select a material to swap")
                    .AddItems(flver.Materials)
                    .TextSelector(m => $"#{flver.Materials.IndexOf(m)}: {m.Name} || {m.MTD}")
                    .Run();
                if (matSelect.IsAborted) break;
                var material = matSelect.Value;
                int matIdx = flver.Materials.IndexOf(material);
                PromptPlus.WriteLine(
                    $"Selected #{flver.Materials.IndexOf(material)}: {material.Name} || {material.MTD}");
                var select = PromptPlus
                    .Select<FLVER2MaterialInfoBank.MaterialDef>($"Select new material to replace {material.Name} with")
                    .TextSelector(a => $"{a.MTD} ({a.Shader})")
                    .AddItems(matInfoBank.MaterialDefs.Values)
                    .Run();
                if (select.IsAborted) break;
                var replaceMatDef = select.Value;

                FLVER2.GXList gxList = new();
                gxList.AddRange(matInfoBank.GetDefaultGXItemsForMTD(replaceMatDef.MTD));
                flver.GXLists.Add(gxList);
                material.MTD = replaceMatDef.MTD;
                material.GXIndex = flver.GXLists.IndexOf(gxList);

                material.Textures =
                    replaceMatDef.TextureChannels.Values.Select(x => new FLVER2.Texture { ParamName = x }).ToList();

                var meshes = flver.Meshes.Where(a => a.MaterialIndex == matIdx).ToList();
                var firstMesh = meshes.First();
                var acceptableBufferDeclarations = replaceMatDef.AcceptableVertexBufferDeclarations;
                List<FLVER2.BufferLayout> bufferLayouts = acceptableBufferDeclarations[0].Buffers;
                if (acceptableBufferDeclarations.Count > 1)
                {
                    List<FLVER2.BufferLayout>? matchingLayouts = acceptableBufferDeclarations.FirstOrDefault(x =>
                        x.Buffers.SelectMany(y => y).Count(y => y.Semantic == FLVER.LayoutSemantic.Tangent) >=
                        firstMesh.Vertices.First().Tangents.Count)?.Buffers;

                    if (matchingLayouts != null)
                    {
                        // Log.Log("Replace with matching layouts");
                        bufferLayouts = matchingLayouts;
                    }
                }

                List<int> layoutIndices = FlverUtils.GetLayoutIndices(flver, bufferLayouts);
                foreach (FLVER2.Mesh mesh in meshes)
                {
                    mesh.VertexBuffers = layoutIndices.Select(x => new FLVER2.VertexBuffer(x)).ToList();
                    foreach (var v in mesh.Vertices)
                    {
                        FlverUtils.PadVertex(v, bufferLayouts);
                    }
                }

                FlverUtils.AdjustBoneIndexBufferSize(flver, bufferLayouts);
                changesMade = true;
                PromptPlus.WriteLine();
                PromptPlus.WriteLine($"Material conversion complete.");
                PromptPlus.WriteLine();
            }
        }

        if (exitAndSave)
        {
            PromptPlus.WriteLine("Backing up FLVER...");
            if (File.Exists(filePath))
            {
                File.Copy(filePath, ($"{filePath}.bak"), true);
            }

            PromptPlus.WriteLine("Saving FLVER...");
            flver.Write(filePath);
            PromptPlus.KeyPress("Successfully saved the FLVER! Enjoy.").Run();
        }
    }

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        PromptPlus.Config.DefaultCulture = new CultureInfo("en-us");
        PromptPlus.IgnoreColorTokens = true;

        if (!IsDebug())
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            RunProgram(args);
        }
        catch (Exception e) when (!IsDebug())
        {
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "crash.log"),
                e?.InnerException?.ToString() ?? e?.ToString());
            PromptPlus.Error.WriteLine(@$"
There was an exception:

{e?.InnerException?.ToString() ?? e?.ToString()}

This error message has also been saved to crash.log in the program directory.

Press any key to exit...");
            PromptPlus.ReadKey();
        }
    }
}