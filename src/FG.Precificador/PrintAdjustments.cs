using System.Globalization;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace FG.Precificador;

public static class PrintAdjustments
{
    public static readonly string[] Allowed=["layer_height","sparse_infill_density","wall_loops","enable_support","brim_type","brim_width"];
    public static Dictionary<string,string> Read(string path)
    {
        using var zip=ZipFile.OpenRead(path);var entry=zip.GetEntry("Metadata/project_settings.config")??throw new InvalidDataException("O projeto não contém parâmetros de impressão.");
        if(entry.Length>20_000_000)throw new InvalidDataException("Configurações grandes demais.");using var reader=new StreamReader(entry.Open());
        var obj=JsonNode.Parse(reader.ReadToEnd())!.AsObject();return Allowed.ToDictionary(k=>k,k=>obj[k]?.ToString()??"");
    }
    public static void CreateCopy(string source,string target,IReadOnlyDictionary<string,string> changes)
    {
        if(string.Equals(Path.GetFullPath(source),Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Salve uma cópia com outro nome. O original deve ser preservado.");
        if(changes.Count==0||changes.Keys.Except(Allowed).Any())throw new InvalidDataException("Selecione os parâmetros que deseja alterar.");
        foreach(var (key,value) in changes)
        {
            bool Numeric(decimal lo,decimal hi,string v)=>decimal.TryParse(v,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)&&n>=lo&&n<=hi;
            bool valid=key switch{"layer_height"=>Numeric(.04m,.8m,value),"sparse_infill_density"=>value.EndsWith('%')&&Numeric(0,100,value[..^1]),"wall_loops"=>int.TryParse(value,out var n)&&n>=1&&n<=20,"enable_support"=>value is "0" or "1","brim_type"=>value is "no_brim" or "outer_only" or "auto_brim","brim_width"=>Numeric(0,30,value),_=>false};
            if(!valid)throw new InvalidDataException("Valor de impressão inválido: "+key);
        }
        var temp=Path.GetFullPath(target)+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var input=ZipFile.OpenRead(source))
            using(var output=ZipFile.Open(temp,ZipArchiveMode.Create))
            {
                var settings=input.GetEntry("Metadata/project_settings.config")??throw new InvalidDataException("Configurações ausentes.");
                if(settings.Length>20_000_000||input.Entries.Count>10000||input.Entries.Sum(x=>x.Length)>4_000_000_000L)throw new InvalidDataException("Projeto grande demais.");
                JsonObject obj;using(var reader=new StreamReader(settings.Open()))obj=JsonNode.Parse(reader.ReadToEnd())!.AsObject();
                if(changes.TryGetValue("layer_height",out var layer)&&obj["nozzle_diameter"] is JsonArray nozzles)
                {
                    var limit=nozzles.Select(x=>decimal.Parse(x!.ToString(),CultureInfo.InvariantCulture)).Min()*.8m;
                    if(decimal.Parse(layer,CultureInfo.InvariantCulture)>limit)throw new InvalidDataException($"A camada excede 80% do menor bico deste projeto ({limit:N2} mm).");
                }
                foreach(var (key,value) in changes)obj[key]=value;
                if(changes.ContainsKey("layer_height")&&input.Entries.Any(e=>e.FullName.Contains("layer_heights_profile",StringComparison.OrdinalIgnoreCase)&&e.Length>0))throw new InvalidDataException("Este projeto tem camadas adaptativas. Remova o perfil adaptativo em um fatiador compatível antes de alterar a camada aqui.");
                foreach(var entry in input.Entries)
                {
                    var name=entry.FullName;
                    // Remove derived toolpaths and previews; they no longer describe these settings.
                    if(name.EndsWith(".gcode",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".gcode.md5",StringComparison.OrdinalIgnoreCase)||name=="Metadata/slice_info.config"||name.StartsWith("Metadata/plate_")&&(name.EndsWith(".png")||name.EndsWith(".json")))continue;
                    var next=output.CreateEntry(name,CompressionLevel.Fastest);using var dest=next.Open();
                    if(name=="Metadata/project_settings.config") {using var w=new StreamWriter(dest,leaveOpen:true);w.Write(obj.ToJsonString(Store.Json));}
                    else if(name=="Metadata/model_settings.config")
                    {
                        var model=Project3D.ReadXml(entry);
                        // Selected values apply project-wide, including any explicit object/part overrides.
                        foreach(var m in model.Descendants("metadata").Where(x=>changes.ContainsKey((string?)x.Attribute("key")??"")))m.SetAttributeValue("value",changes[(string)m.Attribute("key")!]);
                        model.Save(dest);
                    }
                    else{using var src=entry.Open();src.CopyTo(dest);}
                }
            }
            File.Move(temp,target,true);
        }finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
