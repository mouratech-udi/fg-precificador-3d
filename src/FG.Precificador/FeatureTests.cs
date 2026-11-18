using System.IO.Compression;
using System.Text.Json;
using System.Numerics;

namespace FG.Precificador;
public static class FeatureTests
{
    public static void Run(Action<bool,string> check,string folder)
    {
        var q=new Quote{UnitsInFile=3,Plates=[new(){Index=1,Grams=20,Seconds=120},new(){Index=2,Grams=35,Seconds=345},new(){Index=3,Grams=45,Seconds=891}],ExtraGrams=5,OtherPerUnit=2.75m};
        check(q.Settings.LaborBasis==LaborBasis.Project&&Pricing.Calculate(q).Labor*3==20,"Novo orçamento cobra mão de obra uma vez por projeto");
        foreach(var basis in Enum.GetValues<LaborBasis>())foreach(var shopee in new[]{false,true})
        {
            q.Settings.LaborBasis=basis;q.Shopee=shopee;q.ShopeeFixed=5.5m;
            var r=Pricing.Calculate(q);var plates=Pricing.ByPlate(q);
            check(Math.Abs(plates.Sum(x=>x.Cost)-r.PrintCost*q.UnitsInFile)<.0000001m&&Math.Abs(plates.Sum(x=>x.Labor)-r.Labor*q.UnitsInFile)<.0000001m&&plates.Sum(x=>x.Minimum)==r.Minimum*q.UnitsInFile,$"Rateio fecha custo, mão de obra e preço: {basis}, Shopee={shopee}");
        }
        q.Settings.LaborBasis=LaborBasis.Plate;check(Pricing.Calculate(q).Labor*q.UnitsInFile==60,"Três placas cobram três mãos de obra quando essa opção é escolhida");
        q.Settings=new PricingSettings{FilamentPerKg=0,EnergyPerKwh=0,MachinePerHour=0,Labor=0};q.ExtraGrams=0;q.OtherPerUnit=0;q.Shopee=false;check(Pricing.ByPlate(q).All(x=>x.Minimum==0),"Custos zerados não causam divisão por zero no rateio");
        var data=new AppData();var root=CatalogGroups.Create(data,"Quadros",null);var child=CatalogGroups.Create(data,"Animais",root.Id);var a=new Quote{Name="Joaninha vermelha"};var b=new Quote{Name="Borboleta lilás"};data.Quotes=[a,b];
        CatalogGroups.Move(data,[a.Id,b.Id],child.Id);check(CatalogGroups.Filter(data,"LILAS",root.Id).Single().Id==b.Id,"Pesquisa ignora acentos e encontra orçamento dentro de subgrupo");
        check(CatalogGroups.PathName(data,child.Id)=="Quadros / Animais"&&data.Quotes.All(x=>x.GroupId==child.Id),"Move vários orçamentos e conserva a hierarquia de grupos");
        var store=new Store(Path.Combine(folder,"grupos"));store.Save(data);store.DeleteQuote(data,a.Id);var restored=store.Load();store.RestoreLastDeleted(restored);check(restored.Groups.Count==2&&restored.Quotes.First(x=>x.Id==a.Id).GroupId==child.Id,"Excluir e recuperar preserva grupos e associação do orçamento");
        var old=new Store(Path.Combine(folder,"migracao"));var previous=new AppData{Version=1,Quotes=[new Quote{UnitsInFile=3,Settings=new PricingSettings{LaborBasis=LaborBasis.Unit},Plates=[new(){Grams=100,Seconds=3600}]}]};
        var expected=Pricing.Calculate(previous.Quotes[0]).Minimum;var node=JsonSerializer.SerializeToNode(previous,Store.Json)!;node["Quotes"]![0]!["Settings"]!.AsObject().Remove("LaborBasis");node["Defaults"]!.AsObject().Remove("LaborBasis");File.WriteAllText(old.DataPath,node.ToJsonString());
        var migrated=old.Load();old.Load();check(migrated.Quotes[0].Settings.LaborBasis==LaborBasis.Unit&&Pricing.Calculate(migrated.Quotes[0]).Minimum==expected&&migrated.Defaults.LaborBasis==LaborBasis.Project&&File.Exists(old.DataPath+".antes-1.4.0.bak"),"Migração preserva preço anterior, cria cópia e muda apenas o padrão dos novos");
        string Release(string tag,string url="https://github.com/mouratech-udi/fg-precificador-3d/releases/tag/v1.5.0")=>JsonSerializer.Serialize(new{tag_name=tag,html_url=url,draft=false,prerelease=false});
        check(UpdateChecker.Parse(Release("v1.5.0"),new Version(1,4,0,0))?.Version==new Version(1,5,0)&&UpdateChecker.Parse(Release("v1.4.0"),new Version(1,4,0,0))==null,"Aviso compara versões publicadas sem falso positivo pela revisão da DLL");
        bool blocked=false;try{UpdateChecker.Parse(Release("v1.5.0","https://example.com/download"),new Version(1,4,0));}catch(InvalidDataException){blocked=true;}check(blocked,"Atualizações aceitam apenas a página do repositório oficial");
        var cube=Path.Combine(folder,"cubo.3mf");
        using(var zip=ZipFile.Open(cube,ZipArchiveMode.Create))
        {
            using(var w=new StreamWriter(zip.CreateEntry("3D/3dmodel.model").Open()))w.Write("<model xmlns='http://schemas.microsoft.com/3dmanufacturing/core/2015/02' unit='millimeter'><resources><object id='1'><mesh><vertices><vertex x='0' y='0' z='0'/><vertex x='20' y='0' z='0'/><vertex x='0' y='20' z='20'/></vertices><triangles><triangle v1='0' v2='1' v3='2'/></triangles></mesh></object><object id='2'><components><component objectid='1' transform='1 0 0 0 1 0 0 0 1 5 0 0'/></components></object></resources><build><item objectid='2' transform='1 0 0 0 1 0 0 0 1 10 0 0'/></build></model>");
            using(var w=new StreamWriter(zip.CreateEntry("Metadata/project_settings.config").Open()))w.Write("{\"layer_height\":\"0.2\",\"nozzle_diameter\":[\"0.4\"],\"wall_loops\":\"2\"}");
            using(var w=new StreamWriter(zip.CreateEntry("Metadata/slice_info.config").Open()))w.Write("old");using(var w=new StreamWriter(zip.CreateEntry("Metadata/plate_1.gcode").Open()))w.Write("old");
        }
        var mesh=Project3D.Load(cube);check(mesh.Faces.Count==1&&mesh.Min==new Vector3(15,0,0)&&mesh.Max==new Vector3(35,20,20),"Visualização compõe transformações de componente e instância corretamente");
        var adjusted=Path.Combine(folder,"ajustado.3mf");PrintAdjustments.CreateCopy(cube,adjusted,new Dictionary<string,string>{{"layer_height","0.12"}});
        using(var zip=ZipFile.OpenRead(adjusted))check(PrintAdjustments.Read(adjusted)["layer_height"]=="0.12"&&zip.GetEntry("Metadata/slice_info.config")==null&&zip.GetEntry("Metadata/plate_1.gcode")==null&&PrintAdjustments.Read(cube)["layer_height"]=="0.2","Ajuste salva cópia, remove dados obsoletos e preserva o original");
        blocked=false;try{PrintAdjustments.CreateCopy(cube,cube,new Dictionary<string,string>{{"layer_height","0.12"}});}catch(InvalidDataException){blocked=true;}check(blocked,"Editor não sobrescreve o arquivo original");
        blocked=false;try{PrintAdjustments.CreateCopy(cube,adjusted,new Dictionary<string,string>{{"layer_height","0.6"}});}catch(InvalidDataException){blocked=true;}check(blocked,"Editor rejeita camada incompatível com o bico");
        a.Plates=[new(){Index=1,Grams=20,Seconds=600}];b.Plates=[new(){Index=1,Grams=30,Seconds=900}];using var form=new MainForm(store,data);check(form.Text.Contains("FG Precificador 3D"),"Interface inicia com histórico, grupos e orçamento calculável");
        using(var control=new MeshViewer{Size=new Size(900,600)})using(var image=new Bitmap(900,600)){control.SetMesh(mesh);control.DrawToBitmap(image,new Rectangle(Point.Empty,control.Size));image.Save(Path.Combine(folder,"malha.png"));}
        var changedQuote=new Quote{Source=cube,Sha256=new string('0',64)};blocked=false;try{Project3D.VerifySource(changedQuote).GetAwaiter().GetResult();}catch(InvalidDataException){blocked=true;}check(blocked,"Arquivo alterado não é apresentado como o modelo do orçamento anterior");
    }
}
