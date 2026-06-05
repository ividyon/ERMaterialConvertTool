using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using PromptPlusLibrary;
using Spectre.Console;

namespace ERMaterialConvertTool;

public class ProgramSpectre
{
    private enum Mode
    {
        [Display(Name = "Convert FLVER from another game")]
        ConvertFlver,

        [Display(Name = "Swap a material for another")]
        MaterialSwap,
    }

    private static void RunProgram(string[] args)
    {
        var panel = new Panel("Test").AsciiBorder().Header("Header").HeaderAlignment(Justify.Center);
        AnsiConsole.Live(panel)
            .Start(ctx =>
            {
                Thread.Sleep(1000);
            });
        // var mode = AnsiConsole.Prompt(
        //     new SelectionPrompt<Mode>()
        //         .Title("Select mode")
        //         .AddChoices(Mode.GetValuesAsUnderlyingType<Mode>().OfType<Mode>())
        //         .UseConverter(m =>
        //         {
        //             var display = m.GetType()
        //                 .GetMember(m.ToString())
        //                 .First()
        //                 .GetCustomAttribute(typeof(DisplayAttribute));
        //             if (display != null)
        //             {
        //                 return ((DisplayAttribute)display).Name!;
        //             }
        //
        //             return m.ToString();
        //         }));
        //
        // AnsiConsole.MarkupLine($"You selected: [yellow]{mode}[/]");
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

    // static void Main(string[] args)
    // {
    //     Console.OutputEncoding = System.Text.Encoding.UTF8;
    //     Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    //     // PromptPlus.Config.DefaultCulture = new CultureInfo("en-us");
    //     // PromptPlus.IgnoreColorTokens = true;
    //
    //     if (!IsDebug())
    //         AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    //
    //     try
    //     {
    //         RunProgram(args);
    //     }
    //     catch (Exception e) when (!IsDebug())
    //     {
    //         File.WriteAllText(Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "crash.log"),
    //             e?.InnerException?.ToString() ?? e?.ToString());
    //         PromptPlus.ReadKey();
    //     }
    // }
}