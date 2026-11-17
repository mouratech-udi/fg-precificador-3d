using System.IO.Compression;
using System.Text.Json;

namespace FG.Precificador;

public static class SelfTests
{
    public static int Run(string output)
    {
        var checks=new List<string>();
        void Check(bool condition,string name){if(!condition)throw new Exception(name);checks.Add(name);}
        try
        {
            var q=new Quote {Plates=[new Plate {Grams=100,Seconds=3600}]};
            var r=Pricing.Calculate(q);
            Check(r.PrintCost==11.144m&&r.Minimum==42.29m&&r.Suggested==43,"Custo e arredondamento sem Shopee");
            q.Shopee=true;q.ShopeeFixed=5.5m;r=Pricing.Calculate(q);
            Check(r.Minimum==59.74m&&r.Minimum*.8m-5.5m>=42.288m,"Shopee preserva receita pretendida após taxas");
            Check(r.Profit>=r.PrintCost,"Lucro mínimo permanece 100% após mão de obra e taxas");
            q.Shopee=false;q.OrderQuantity=3;r=Pricing.Calculate(q);
            Check(r.PrintCost==11.144m&&r.OrderTotal==129,"Quantidade do pedido não divide o consumo");
            q.UnitsInFile=2;r=Pricing.Calculate(q);
            Check(r.PrintCost==5.572m,"Unidades realmente presentes no arquivo dividem o custo");
            q.UnitsInFile=0;bool rejected=false;try{Pricing.Calculate(q);}catch(InvalidDataException){rejected=true;}Check(rejected,"Unidade zero é rejeitada");
            q.UnitsInFile=1;q.Shopee=true;q.ShopeePercent=100;rejected=false;try{Pricing.Calculate(q);}catch(InvalidDataException){rejected=true;}Check(rejected,"Taxa de 100% é rejeitada");
            var oldSettings=new Dictionary<string,string>{{"version","\"02.07.01.57\""},{"layer_height","\"0.28\""},{"filament_prime_volume","[\"30\",\"30\"]"}};
            var newSettings=new Dictionary<string,string>(oldSettings){{"new_metadata","0"}};
            newSettings["version"]="\"02.08.02.60\"";
            Check(Slicer.ChangedPrintSettings(oldSettings,newSettings).Length==0,"Versão 2.7 para 2.8 não bloqueia impressão inalterada");
            newSettings["layer_height"]="\"0.20\"";
            Check(Slicer.ChangedPrintSettings(oldSettings,newSettings).SequenceEqual(new[]{"layer_height"}),"Mudança real de camada continua sendo detectada");
            newSettings["layer_height"]=oldSettings["layer_height"];newSettings["filament_prime_volume"]="[\"45\",\"45\"]";
            Check(Slicer.ChangedPrintSettings(oldSettings,newSettings).SequenceEqual(new[]{"filament_prime_volume"}),"Mudança real de purga continua sendo detectada");
            newSettings["filament_prime_volume"]=oldSettings["filament_prime_volume"];newSettings.Remove("version");
            Check(Slicer.ChangedPrintSettings(oldSettings,newSettings).SequenceEqual(new[]{"version"}),"Versão ausente não recebe exceção de compatibilidade");
            newSettings["version"]="\"not-a-version\"";
            Check(Slicer.ChangedPrintSettings(oldSettings,newSettings).SequenceEqual(new[]{"version"}),"Versão inválida continua sendo rejeitada");
            var g=Slicer.ParseGcode(new StringReader("; model printing time: 1h; total estimated time: 1h 2m 3s\n; total filament weight [g] : 10.50, 2.25\n"));
            Check(g.Grams==12.75m&&g.Seconds==3723,"G-code soma filamentos e usa tempo total");
            var xml="<config><plate><metadata key='index' value='1'/><metadata key='prediction' value='120'/><metadata key='weight' value='5'/></plate><plate><metadata key='index' value='2'/><metadata key='prediction' value='180'/><metadata key='weight' value='7'/></plate></config>";
            var plates=Slicer.ParseSliceInfo(xml);Check(plates.Sum(x=>x.Grams)==12&&plates.Sum(x=>x.Seconds)==300,"Metadados preservam todas as mesas");
            rejected=false;try{Slicer.ParseSliceInfo(xml.Replace("value='180'","value='0'"));}catch(InvalidDataException){rejected=true;}Check(rejected,"Mesa incompleta é rejeitada");
            var folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!,"teste-"+Guid.NewGuid().ToString("N"));
            var store=new Store(folder);var app=new AppData();app.Quotes.Add(new Quote {Plates=plates});store.Save(app);app.Defaults.FilamentPerKg=120;store.Save(app);var read=store.Load();
            Check(read.Defaults.FilamentPerKg==120&&read.Quotes.Count==1&&File.Exists(store.DataPath+".bak"),"Histórico e padrões salvos com cópia anterior");
            var archive=Path.Combine(folder,"duas-mesas.3mf");
            using(var z=ZipFile.Open(archive,ZipArchiveMode.Create)){using var w=new StreamWriter(z.CreateEntry("Metadata/slice_info.config").Open());w.Write(xml);}
            var imported=Slicer.ImportAsync(archive,"",store).GetAwaiter().GetResult();
            Check(imported.Plates.Count==2&&imported.Grams==12&&imported.Seconds==300,"3MF fatiado funciona sem o Bambu Studio");
            Check(File.Exists(archive),"Arquivo original preservado");
            var partial=Path.Combine(folder,"fatiamento-parcial.3mf");
            using(var z=ZipFile.Open(partial,ZipArchiveMode.Create))
            {
                using(var w=new StreamWriter(z.CreateEntry("Metadata/slice_info.config").Open()))w.Write("<config><plate><metadata key='index' value='1'/><metadata key='prediction' value='120'/><metadata key='weight' value='5'/></plate></config>");
                using(var w=new StreamWriter(z.CreateEntry("Metadata/model_settings.config").Open()))w.Write("<config><plate><metadata key='plater_id' value='1'/><model_instance/></plate><plate><metadata key='plater_id' value='2'/><model_instance/></plate></config>");
            }
            rejected=false;try{Slicer.ImportAsync(partial,"",store).GetAwaiter().GetResult();}catch(InvalidDataException){rejected=true;}Check(rejected,"Fatiamento parcial não omite o custo das outras mesas");
            Reports.Html(Path.Combine(folder,"orcamento.html"),imported);Reports.Csv(Path.Combine(folder,"orcamento.csv"),imported);
            Check(File.ReadAllText(Path.Combine(folder,"orcamento.html")).Contains("FG DESIGN PRINT"),"Relatório contém composição e preço");
            imported.Name="Borboleta lilás - edição especial";
            PdfReports.Save(Path.Combine(folder,"cliente.pdf"),imported,false,CompanyProfile.Fg());
            PdfReports.Save(Path.Combine(folder,"interno.pdf"),imported,true);
            Check(System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(folder,"cliente.pdf"))[..5])=="%PDF-","PDF para cliente gerado diretamente");
            Check(new FileInfo(Path.Combine(folder,"interno.pdf")).Length>10000,"PDF detalhado contém documento e marca incorporada");
            var company=new CompanyProfile{Name="Oficina Exemplo 3D",Instagram="@oficina.exemplo",Phone="(00) 00000-0000",Email="contato@example.com",Website="example.com",Document="Dado de teste",Address="Endereço de demonstração",City="Cidade de exemplo",ContactName="Contato de teste",PaymentTerms="Condições combinadas com o cliente",DeliveryTerms="Prazo a combinar",ValidityDays=7,Notes="Orçamento de demonstração."};
            using(var image=new Bitmap(80,80)){using(var drawing=Graphics.FromImage(image))drawing.Clear(Color.DarkBlue);using var bytes=new MemoryStream();image.Save(bytes,System.Drawing.Imaging.ImageFormat.Png);company.LogoPngBase64=Convert.ToBase64String(bytes.ToArray());}
            PdfReports.Save(Path.Combine(folder,"empresa-personalizada.pdf"),imported,false,company);
            PdfReports.Save(Path.Combine(folder,"interno-personalizado.pdf"),imported,true,company);
            Check(new FileInfo(Path.Combine(folder,"empresa-personalizada.pdf")).Length>5000,"PDF aceita logo e contatos de outra empresa");
            Check(CompanyProfile.NormalizeInstagram("https://www.instagram.com/oficina.exemplo/")=="@oficina.exemplo","Instagram aceita endereço completo ou nome de usuário");
            rejected=false;try{PdfReports.Save(Path.Combine(folder,"sem-empresa.pdf"),imported,false,new CompanyProfile());}catch(InvalidDataException){rejected=true;}Check(rejected,"PDF do cliente exige a identificação da própria empresa");
            var invalidLogo=company.Copy();invalidLogo.LogoPngBase64="not-an-image";rejected=false;try{invalidLogo.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Logo inválida é rejeitada antes da exportação");
            var empty=new Store(Path.Combine(folder,"instalacao-nova")).Load();Check(empty.Quotes.Count==0&&empty.Company.Name.Length==0,"Nova instalação inicia sem dados e produtos da FG");
            var history=new AppData{Company=company,Quotes=[new Quote{Name="Mesmo nome",Source=archive,Thumbnail="miniatura-a.png",Plates=plates},new Quote{Name="Mesmo nome",Source=archive,Thumbnail="miniatura-b.png",Plates=plates}]};
            var first=history.Quotes[0];var second=history.Quotes[1];store.Save(history);
            Check(store.DeleteQuote(history,first.Id)&&history.Quotes.Count==1&&history.Quotes[0].Id==second.Id,"Excluir usa identificação e preserva os outros orçamentos");
            Check(File.Exists(archive)&&history.DeletedQuotes[0].Quote.Thumbnail=="miniatura-a.png","Excluir orçamento preserva projeto e referência da miniatura");
            history=store.Load();var restored=store.RestoreLastDeleted(history);
            Check(restored?.Id==first.Id&&history.Quotes[0].Id==first.Id&&history.DeletedQuotes.Count==0,"Desfazer exclusão funciona após reabrir o histórico");
            Check(history.Company.Name==company.Name&&history.Company.LogoPngBase64==company.LogoPngBase64,"Dados e logo da empresa sobrevivem a backup, exclusão e recuperação");
            Check(!store.DeleteQuote(history,Guid.NewGuid())&&history.Quotes.Count==2,"Excluir identificação ausente não altera o histórico");
            var failingStore=new Store(Path.Combine(folder,"falha-gravacao"));Directory.CreateDirectory(failingStore.DataPath);var failed=false;
            try{failingStore.DeleteQuote(history,first.Id);}catch(IOException){failed=true;}
            Check(failed&&history.Quotes.Count==2&&history.DeletedQuotes.Count==0,"Falha de gravação não exclui o orçamento em memória");
            var unsafeArchive=Path.Combine(folder,"pos-processamento.3mf");
            using(var z=ZipFile.Open(unsafeArchive,ZipArchiveMode.Create)){using var w=new StreamWriter(z.CreateEntry("Metadata/project_settings.config").Open());w.Write("{\"post_process\":[\"some-external-script\"]}");}
            rejected=false;try{Slicer.ImportAsync(unsafeArchive,"",store).GetAwaiter().GetResult();}catch(InvalidDataException){rejected=true;}Check(rejected,"Projeto com programa externo não é executado");
            File.WriteAllText(output,JsonSerializer.Serialize(new{success=true,checks},Store.Json));return 0;
        }
        catch(Exception ex){File.WriteAllText(output,JsonSerializer.Serialize(new{success=false,checks,error=ex.ToString()},Store.Json));return 1;}
    }
}
