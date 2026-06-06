using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using ERMaterialConvertTool.Modes;
using NativeFileDialogSharp;
using PPlus;
using SoulsAssetPipeline.FLVERImporting;
using SoulsFormats;

namespace ERMaterialConvertTool;

class Program
{
    private enum Mode
    {
        [Display(Name = "Convert FLVER metadata between versions")]
        ConvertFlver,
        [Display(Name = "Edit single FLVER")] EditFlver,
        [Display(Name = "Batch process FLVERs")] BatchFlver,
        [Display(Name = "MATBIN utility actions")] MatbinUtility,
        [Display(Name = "Exit program")] Exit
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

    internal static FLVER2? LoadFlver(ref string? filePath)
    {
        if (filePath != null)
        {
            PromptPlus.WriteLine("Reloading FLVER...");
            return FLVER2.Read(filePath);
        }
        var flverPress = PromptPlus.KeyPress("In the next dialog, please select the FLVER file").Run();
        if (flverPress.IsAborted)
        {
            filePath = null;
            return null;
        }
        DialogResult? picker = null;
        while (picker == null || !picker.IsCancelled)
        {
            picker = Dialog.FileOpen();
            if (picker.IsCancelled) continue;
            if (picker.IsError)
            {
                var filePathPress = PromptPlus.KeyPress("Invalid file path! Try again...").Run();
                if (filePathPress.IsAborted)
                    break;
                continue;
            }

            filePath = picker.Path;
            break;
        }

        if (filePath == null)
        {
            return null;
        }

        var confirm = PromptPlus.Confirm("Would you like to choose a different path to save any output to?")
            .Run();
        if (!confirm.IsAborted && confirm.Value.IsYesResponseKey())
        {
            var tarPicker = Dialog.FileSave("flver", Path.GetDirectoryName(filePath)!);
            if (tarPicker.IsOk)
            {
                if (filePath != tarPicker.Path)
                {
                    PromptPlus.WriteLine($"Copying file to {tarPicker.Path.PromptPlusEscape()}...");
                    File.Copy(filePath, tarPicker.Path, true);
                }
                filePath = tarPicker.Path;
            }
        }

        PromptPlus.WriteLine("Loading FLVER...");
        return FLVER2.Read(filePath);
    }

    internal static void SaveFlvers(Dictionary<string, FLVER2> flvers, bool prompt)
    {
        if (prompt)
        {
            var confirm = PromptPlus.Confirm("Save results? Changes will be discarded otherwise.").Run();
            if (confirm.IsAborted || confirm.Value.IsNoResponseKey()) return;
        }

        foreach ((string filePath, FLVER2 flver) in flvers)
        {
            string p = filePath;
            var f = flver;
            SaveFlver(ref f, ref p);
        }
    }
    internal static void SaveFlver(ref FLVER2 flver, ref string filePath, bool prompt = false)
    {
        if (prompt)
        {
            var confirm = PromptPlus.Confirm("Save FLVER? Changes will be discarded otherwise.").Run();
            if (confirm.IsAborted || confirm.Value.IsNoResponseKey()) return;
        }

        if (File.Exists(filePath))
        {
            PromptPlus.WriteLine("Backing up FLVER...");
            File.Copy(filePath, ($"{filePath}.bak"), true);
        }

        CleanFlver(ref flver);

        PromptPlus.WriteLine("Saving FLVER...");
        var tmpPath = Path.GetTempFileName();
        try
        {
            flver.Write(tmpPath);
        }
        catch (Exception e) when (!IsDebug())
        {
            PromptPlus.WriteLine($"[RED]ERROR[/]: There was an error in saving the FLVER:\n{e}");
            PromptPlus.KeyPress("Press any key to continue...").Run();
            return;
        }
        PromptPlus.WriteLine("Copying saved FLVER...");
        File.Copy(tmpPath, filePath, true);
        PromptPlus.WriteLine("Removing temporary file...");
        File.Delete(tmpPath);

        string goodbye = "Successfully saved the FLVER! Enjoy.";
        if (prompt)
        {
            PromptPlus.KeyPress(goodbye).Run();
        }
        else
        {
            PromptPlus.WriteLine("Successfully saved the FLVER! Enjoy.");
        }
    }

    internal static void CleanFlver(ref FLVER2 flver)
    {
        var f = flver;
        var unusedLayouts = f.BufferLayouts.Where(l =>
        {
            var idx = f.BufferLayouts.IndexOf(l);
            return !f.Meshes.Any(m => m.VertexBuffers.Any(b => b.LayoutIndex == idx));
        }).ToList();

        unusedLayouts.Reverse();

        foreach (FLVER2.BufferLayout l in unusedLayouts)
        {
            var idx = f.BufferLayouts.IndexOf(l);
            foreach (FLVER2.VertexBuffer buffer in f.Meshes.SelectMany(m => m.VertexBuffers).Where(b => b.LayoutIndex > idx))
            {
                buffer.LayoutIndex -= 1;
            }
            PromptPlus.WriteLine($"Removing unused buffer layout #{idx} ({l.Count} members)");
            f.BufferLayouts.Remove(l);
        }
    }

    private static void RunProgram(string[] args)
    {
        while (true)
        {
            PromptPlus.Clear();
            PromptPlus.DoubleDash("ERMaterialConvertTool");
            PromptPlus.WriteLine(
                "Hi! This is a tool for changing materials in an ELDEN RING FLVER2 file,\nor converting FLVER2 files from other games to ELDEN RING.");
            PromptPlus.WriteLine("");

            // string brokenPath = "G:\\Creative\\GitHub\\err-dev\\ERR\\mod\\chr\\c7580-chrbnd-dcx\\c7580.flver";
            // string workingPath = "G:\\Creative\\GitHub\\err-dev\\ERR\\mod\\chr\\c7580-chrbnd-dcx\\c7580-kindaworks.flver";
            // string nrPath = "G:\\Creative\\GitHub\\err-dev\\ERR\\mod\\chr\\c7580-chrbnd-dcx\\c7580_nr.flver";
            // // var broken = LoadFlver(ref brokenPath)!;
            // // var working = LoadFlver(ref workingPath)!;
            // // var nr = LoadFlver(ref nrPath)!;

            var modeSelect = PromptPlus.Select<Mode>("Select mode of operation").Run();
            if (modeSelect.IsAborted) return;

            Mode mode = modeSelect.Value;

            if (mode == Mode.Exit)
            {
                return;
            }

            string? filePath = null;
            FLVER2? flver = null;

            switch (mode)
            {
                case Mode.ConvertFlver:
                    ConvertFlver.Perform(ref flver, ref filePath);
                    break;
                case Mode.EditFlver:
                    EditFlver.Perform(ref flver, ref filePath);
                    break;
                case Mode.BatchFlver:
                    BatchFlver.Perform();
                    break;
                case Mode.MatbinUtility:
                    MatbinUtility.Perform();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
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