using System.Globalization;
using System.Text.Json;

namespace FG.Precificador;

public static class Program
{
    public static string[] StartFiles=[];
    [STAThread]
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pt-BR");CultureInfo.CurrentUICulture=CultureInfo.CurrentCulture;
        if(args.Length>0&&args[0]=="--self-test")return SelfTests.Run(args.Length>1?args[1]:"autoteste.json");
        if(args.Length>=3&&args[0]=="--self-test-engine")return EngineTests.Run(args[1],args[2]);
        if(args.Length>=3&&args[0]=="--verificar-3d")
        {
            try{var mesh=Project3D.Load(args[1],args.Length>3?int.Parse(args[3]):null);var size=mesh.Max-mesh.Min;File.WriteAllText(args[2],JsonSerializer.Serialize(new{success=true,faces=mesh.Faces.Count,width=size.X,depth=size.Y,height=size.Z,simplified=mesh.Simplified},Store.Json));return 0;}
            catch(Exception ex){File.WriteAllText(args[2],JsonSerializer.Serialize(new{success=false,error=ex.ToString()},Store.Json));return 1;}
        }
        if(args.Length>=3&&args[0]=="--exportar-pdf")
        {
            try
            {
                using var json=JsonDocument.Parse(File.ReadAllText(args[1]));
                var element=json.RootElement.TryGetProperty("quote",out var nested)?nested:json.RootElement;
                var q=element.Deserialize<Quote>(Store.Json)??throw new InvalidDataException("Orçamento inválido.");
                var company=json.RootElement.TryGetProperty("company",out var identity)?identity.Deserialize<CompanyProfile>(Store.Json):null;
                PdfReports.Save(args[2],q,args.Contains("--interno"),company);return 0;
            }catch(Exception ex){File.WriteAllText(args[2]+".error.txt",ex.ToString());return 1;}
        }
        if(args.Length>=3&&args[0]=="--analisar")
        {
            try
            {
                var root=args.Length>=4?args[3]:Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[2]))!,"dados-teste");
                var store=new Store(root);var q=Slicer.ImportAsync(args[1],Slicer.FindBambu(),store).GetAwaiter().GetResult();
                File.WriteAllText(args[2],JsonSerializer.Serialize(new{quote=q,price=Pricing.Calculate(q)},Store.Json));return 0;
            }
            catch(Exception ex){File.WriteAllText(args[2],JsonSerializer.Serialize(new{error=ex.ToString()},Store.Json));return 1;}
        }
        ApplicationConfiguration.Initialize();
        try
        {
            var rootIndex=Array.IndexOf(args,"--dados");var store=new Store(rootIndex>=0&&args.Length>rootIndex+1?args[rootIndex+1]:null);
            var mutexKey=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(store.Root.ToUpperInvariant())));
            using var instance=new Mutex(true,"Local\\FG.Precificador."+mutexKey,out var firstInstance);
            if(!firstInstance){MessageBox.Show("O FG Precificador 3D já está aberto neste computador.","FG Precificador 3D",MessageBoxButtons.OK,MessageBoxIcon.Information);return 0;}
            StartFiles=args.Where(x=>File.Exists(x)&&(x.EndsWith(".3mf",StringComparison.OrdinalIgnoreCase)||x.EndsWith(".gcode",StringComparison.OrdinalIgnoreCase))).ToArray();
            Application.Run(new MainForm(store,store.Load()));return 0;
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"FG Precificador 3D",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    }
}
