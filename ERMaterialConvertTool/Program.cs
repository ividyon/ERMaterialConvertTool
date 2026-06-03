using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using PPlus;
using SoulsAssetPipeline.FLVERImporting;
using SoulsFormats;

namespace ERMaterialConvertTool;

class Program
{
    private enum Mode
    {
        [Display(Name = "Convert FLVER from another game")]
        ConvertFlver,

        [Display(Name = "Swap a material for another")]
        MaterialSwap,
    }

    private enum MTDDecision
    {
        UseNewMTD,
        KeepOldMTD,
        CustomName,
        KeepOriginal
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
        matInfoBank.MaterialDefs = matInfoBank.MaterialDefs.OrderBy(a => a.Value.MTD).ToDictionary();

        bool changesMade = false;
        bool exitAndSave = false;
        var mtdDecisionKeep = (MTDDecision)0;
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
                flver.Header.Unk74 = 0;
            }

            flver.BufferLayouts = new();
            flver.GXLists = new();

            Dictionary<string, FLVER2MaterialInfoBank.MaterialDef> mapping = new();
            Dictionary<string, MTDDecision> mtdDecisionMapping = new();
            PromptPlus.WriteLine(
                $"Materials:\n{string.Join("\n", flver.Materials.Select(a => $"#{flver.Materials.IndexOf(a)}: {a.Name} ({a.MTD}) Index {a.Index}"))}\n");
            foreach (string mtd in flver.Materials.Select(a => Path.GetFileNameWithoutExtension(a.MTD).ToLower())
                         .Distinct())
            {
                var select = PromptPlus.Select<FLVER2MaterialInfoBank.MaterialDef>($"Select ER material to map to {mtd}")
                    .TextSelector(a => $"{a.MTD} ({a.Shader})")
                    .AddItems(matInfoBank.MaterialDefs.Values.OrderBy(v => v.MTD.ToLower() == mtd));

                var selectPrompt = select.Run();

                mapping[mtd] = selectPrompt.Value;

                var mtdDecisionPrompt = PromptPlus.Select<MTDDecision>("What to do about the material name in the mesh?")
                    .Default(mtdDecisionKeep)
                    .Run();
                if (mtdDecisionPrompt.IsAborted) break;
                mtdDecisionMapping[mtd] = mtdDecisionPrompt.Value;
                mtdDecisionKeep = mtdDecisionPrompt.Value;
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
                switch (mtdDecisionMapping[matName])
                {
                    case MTDDecision.UseNewMTD:
                        flverMaterial.MTD = matDef.MTD;
                        break;
                    case MTDDecision.KeepOldMTD:
                        break;
                    case MTDDecision.CustomName:
                        var mtdInput = PromptPlus
                            .Input("Input new material name for the mesh (.matxml will be appended)").Run();
                        if (mtdInput.IsAborted)
                        {
                            flverMaterial.MTD = matDef.MTD;
                            break;
                        }

                        flverMaterial.MTD = mtdInput.Value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
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
                var mtd = Path.GetFileNameWithoutExtension(material.MTD).ToLower();
                int matIdx = flver.Materials.IndexOf(material);
                PromptPlus.WriteLine(
                    $"Selected #{flver.Materials.IndexOf(material)}: {material.Name} || {material.MTD}");
                var select = PromptPlus
                    .Select<FLVER2MaterialInfoBank.MaterialDef>($"Select new material to replace {material.Name} || {material.MTD} with")
                    .TextSelector(a => $"{a.MTD} ({a.Shader}) ({a.GXItems.Count} GXI)")
                    .AddItems(matInfoBank.MaterialDefs.Values.OrderBy(v => v.MTD == mtd))
                    .Run();
                if (select.IsAborted) break;
                var replaceMatDef = select.Value;

                FLVER2.GXList gxList = new();
                gxList.AddRange(matInfoBank.GetDefaultGXItemsForMTD(replaceMatDef.MTD));

                flver.GXLists.Add(gxList);
                var mtdDecisionPrompt = PromptPlus.Select<MTDDecision>("What to do about the material name in the mesh?")
                    .Default(mtdDecisionKeep)
                    .Run();
                if (mtdDecisionPrompt.IsAborted) break;
                switch (mtdDecisionPrompt.Value)
                {
                    case MTDDecision.UseNewMTD:
                        material.MTD = replaceMatDef.MTD;
                        break;
                    case MTDDecision.KeepOldMTD:
                        break;
                    case MTDDecision.CustomName:
                        var mtdInput = PromptPlus
                            .Input("Input new material name for the mesh (.matxml will be appended)").Run();
                        if (mtdInput.IsAborted)
                        {
                            material.MTD = replaceMatDef.MTD;
                            break;
                        }

                        material.MTD = mtdInput.Value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                mtdDecisionKeep = mtdDecisionPrompt.Value;
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