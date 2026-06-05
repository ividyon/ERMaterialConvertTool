using PromptPlusLibrary;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static class ConvertFlver
{
    public static void Perform(ref FLVER2 flver, ref string filePath)
    {
        flver = Program.LoadFlver(ref filePath);
        if (flver == null || filePath == null) return;

        PromptPlus.Console.WriteLine("Preparing FLVER...");
        var flverVersion = flver.Header.Version;
        bool isPreEldenRing = flverVersion < 0x2001A;
        bool isNightreign = flverVersion == 0x20021;
        flver.Header.Version = 0x2001A;
        if (isPreEldenRing)
        {
            PromptPlus.Console.WriteLine("FLVER is pre-ELDEN RING; adding a skeleton set and assigning material indices.");
            flver.Skeletons = new FLVER2.SkeletonSet();
            foreach (FLVER2.Material material in flver.Materials)
            {
                material.Index = flver.Materials.IndexOf(material);
            }
            Program.SaveFlver(ref flver, ref filePath);
        }
        else if (isNightreign)
        {
            PromptPlus.Console.WriteLine("FLVER is from Nightreign; changing some header values.");
            flver.Header.Unk68 = 4;
            flver.Header.Unk74 = 0;
            Program.SaveFlver(ref flver, ref filePath);
        }
        else
        {
            PromptPlus.Console.WriteLine("FLVER is already for ELDEN RING.");
        }
    }
}