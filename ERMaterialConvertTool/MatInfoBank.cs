using PPlus;
using SoulsAssetPipeline.FLVERImporting;
using SoulsAssetPipeline.XmlStructs;

namespace ERMaterialConvertTool;

public static class MatInfoBank
{
    private static FLVER2MaterialInfoBank? ERMatInfoBank { get; set; }
    private static FLVER2MaterialInfoBank? NRMatInfoBank { get; set; }
    private static FLVER2MaterialInfoBank? MergedMatInfoBank { get; set; }

    public static FLVER2MaterialInfoBank GetERMatInfoBank()
    {
        if (ERMatInfoBank != null)
            return ERMatInfoBank;

        PromptPlus.WriteLine("Loading ER material bank...");
        string matInfoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SapResources",
            "FLVER2MaterialInfoBank", "BankER.xml");
        FLVER2MaterialInfoBank matInfoBank = FLVER2MaterialInfoBank.ReadFromXML(matInfoPath);
        matInfoBank.MaterialDefs = matInfoBank.MaterialDefs.OrderBy(a => a.Value.MTD).ToDictionary();

        ERMatInfoBank = matInfoBank;
        return ERMatInfoBank;
    }
    public static FLVER2MaterialInfoBank GetNRMatInfoBank()
    {
        if (NRMatInfoBank != null)
            return NRMatInfoBank;

        PromptPlus.WriteLine("Loading NR material bank...");
        string matInfoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SapResources",
            "FLVER2MaterialInfoBank", "BankNR.xml");
        FLVER2MaterialInfoBank matInfoBank = FLVER2MaterialInfoBank.ReadFromXML(matInfoPath);
        matInfoBank.MaterialDefs = matInfoBank.MaterialDefs.OrderBy(a => a.Value.MTD).ToDictionary();

        NRMatInfoBank = matInfoBank;
        return NRMatInfoBank;
    }

    public static FLVER2MaterialInfoBank GetMergedMatInfoBank()
    {
        if (MergedMatInfoBank != null)
            return MergedMatInfoBank;

        var erBank = GetERMatInfoBank();
        var nrBank = GetNRMatInfoBank();
        PromptPlus.WriteLine("Creating merged material bank...");

        var mergedBank = new FLVER2MaterialInfoBank()
        {
            DefaultFallbackMTDName = erBank.DefaultFallbackMTDName,

        };
        mergedBank.DefaultGXItemDataExamples = new(erBank.DefaultGXItemDataExamples);
        foreach (KeyValuePair<string, List<byte[]>> keyValuePair in nrBank.DefaultGXItemDataExamples)
        {
            mergedBank.DefaultGXItemDataExamples.TryAdd(keyValuePair.Key, keyValuePair.Value);
        }
        mergedBank.GXItemStructs = new(erBank.GXItemStructs);
        foreach (KeyValuePair<string, XmlStructDef> keyValuePair in nrBank.GXItemStructs)
        {
            mergedBank.GXItemStructs.TryAdd(keyValuePair.Key, keyValuePair.Value);
        }
        mergedBank.MaterialDefs = new(erBank.MaterialDefs);
        foreach (KeyValuePair<string, FLVER2MaterialInfoBank.MaterialDef> keyValuePair in nrBank.MaterialDefs)
        {
            mergedBank.MaterialDefs.TryAdd(keyValuePair.Key, keyValuePair.Value);
        }

        MergedMatInfoBank = mergedBank;
        return MergedMatInfoBank;
    }
}