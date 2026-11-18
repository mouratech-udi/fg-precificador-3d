using System.Security.Cryptography;
using System.Text.Json;

namespace FG.Precificador;

public static class EngineTests
{
    public static int Run(string fixture,string output)
    {
        var checks=new List<string>();
        void Check(bool condition,string text){if(!condition)throw new InvalidDataException(text);checks.Add(text);}
        try
        {
            Check(SlicingEngine.Available,"Motor incluído na distribuição");
            SlicingEngine.Validate(SlicingEngine.BundledPath);checks.Add("Manifesto, perfis e bibliotecas locais presentes");
            var before=SHA256.HashData(File.ReadAllBytes(fixture));
            var store=new Store(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!,"engine-test-"+Guid.NewGuid().ToString("N")));
            var q=Slicer.ImportAsync(fixture,@"Z:\programa-externo-inexistente.exe",store).GetAwaiter().GetResult();
            Check(q.Method.StartsWith("Motor integrado"),"Usa o motor incluído mesmo com caminho externo inválido");
            Check(q.Plates.Count==1,"Todas as mesas do cubo foram analisadas");
            Check(Math.Abs(q.Grams-3.9644222259521484m)<.001m,"Consumo de referência do cubo preservado");
            Check(Math.Abs(q.Seconds-977.7164306640625m)<.1m,"Tempo de referência do cubo preservado");
            Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(fixture))),"Projeto de origem intacto");
            Check(!Directory.EnumerateDirectories(Path.Combine(store.Root,"Trabalhos")).Any(),"Arquivos temporários do motor removidos após a análise");
            var externalInstalled=File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Bambu Studio","bambu-studio.exe"));
            if(Environment.GetEnvironmentVariable("FG_REQUIRE_NO_BAMBU")=="1")Check(!externalInstalled,"Teste executado sem Bambu Studio instalado");
            File.WriteAllText(output,JsonSerializer.Serialize(new{success=true,checks,engineVersion=SlicingEngine.Version,externalStudioInstalled=externalInstalled,grams=q.Grams,seconds=q.Seconds},Store.Json));return 0;
        }
        catch(Exception ex){File.WriteAllText(output,JsonSerializer.Serialize(new{success=false,checks,error=ex.ToString()},Store.Json));return 1;}
    }
}
