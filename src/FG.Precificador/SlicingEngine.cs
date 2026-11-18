using System.Diagnostics;
using System.Text.Json;

namespace FG.Precificador;

public static class SlicingEngine
{
    public const string Version="02.08.02.60";
    public static string BundledPath => Path.Combine(AppContext.BaseDirectory,"Engine","bambu-studio.exe");
    public static bool Available => File.Exists(BundledPath);
    public static bool IsBundled(string executable) => string.Equals(Path.GetFullPath(executable),Path.GetFullPath(BundledPath),StringComparison.OrdinalIgnoreCase);
    public static string Resolve(string external="") => Available?BundledPath:external;
    public static string Description(string external="") => Available?$"Motor integrado · BambuStudio {Version}":File.Exists(external)?"Motor externo: "+external:"Motor integrado não encontrado. Reinstale o pacote completo.";
    public static void Validate(string executable)
    {
        if(!File.Exists(executable))throw new InvalidDataException("O motor de fatiamento não foi encontrado. Reinstale o FG Precificador completo, incluindo a pasta Engine.");
        if(!IsBundled(executable))return;
        var folder=Path.GetDirectoryName(executable)!;
        foreach(var name in new[]{"engine-manifest.json","BambuStudio.dll","vcruntime140.dll","vcruntime140_1.dll","msvcp140.dll","msvcp140_codecvt_ids.dll","concrt140.dll","resources/profiles/BBL.json"})
            if(!File.Exists(Path.Combine(folder,name)))throw new InvalidDataException("A pasta do motor integrado está incompleta. Reinstale o FG Precificador ou extraia novamente o ZIP inteiro.");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"engine-manifest.json")));
        if(doc.RootElement.GetProperty("version").GetString()!=Version)throw new InvalidDataException("A versão do motor integrado não corresponde ao programa. Reinstale o pacote completo.");
    }
    public static void Configure(ProcessStartInfo info,string job)
    {
        // All slicer settings and temporary logs belong to this job; no Studio account,
        // installed profiles or PATH entries from another slicer are required.
        foreach(var name in new[]{"engine-data","engine-roaming","engine-local"})Directory.CreateDirectory(Path.Combine(job,name));
        info.ArgumentList.Add("--datadir");info.ArgumentList.Add(Path.Combine(job,"engine-data"));
        info.Environment["APPDATA"]=Path.Combine(job,"engine-roaming");
        info.Environment["LOCALAPPDATA"]=Path.Combine(job,"engine-local");
        if(IsBundled(info.FileName))info.Environment["PATH"]=Path.GetDirectoryName(info.FileName)+Path.PathSeparator+Environment.GetFolderPath(Environment.SpecialFolder.System)+Path.PathSeparator+Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    }
}
