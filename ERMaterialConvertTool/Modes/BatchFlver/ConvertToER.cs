using PPlus;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class BatchFlver
{
    public static void ConvertToER(Dictionary<string, FLVER2> dict)
    {
        foreach ((string filePath, FLVER2 flver) in dict)
        {
            var f = filePath;
            var fl = flver;
            ConvertFlver.Perform(ref fl, ref f, false);
        }

        PromptPlus.KeyPress("Press any key to continue...").Run();
    }
}