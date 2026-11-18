using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace FG.Precificador;

public static partial class Slicer
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static decimal Number(string? s) => decimal.TryParse(s, NumberStyles.Float, Inv, out var n) ? n : throw new InvalidDataException("Número ausente ou inválido no fatiamento.");
    public static string FindBambu()
    {
        if(SlicingEngine.Available)return SlicingEngine.BundledPath;
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs") })
        {
            var p = Path.Combine(root, "Bambu Studio", "bambu-studio.exe");
            if (File.Exists(p)) return p;
        }
        return "";
    }
    static string ReadEntry(ZipArchiveEntry entry, long limit = 20_000_000)
    {
        if (entry.Length > limit) throw new InvalidDataException("O projeto contém metadados grandes demais para leitura.");
        using var reader = new StreamReader(entry.Open()); return reader.ReadToEnd();
    }
    static XDocument Xml(string s)
    {
        using var reader = XmlReader.Create(new StringReader(s), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 20_000_000 });
        return XDocument.Load(reader);
    }
    public static List<Plate> ParseSliceInfo(string xml)
    {
        var list = new List<Plate>();
        foreach (var plate in Xml(xml).Descendants("plate"))
        {
            var meta = plate.Elements("metadata").Where(e => e.Attribute("key") != null).ToDictionary(e => (string)e.Attribute("key")!, e => (string?)e.Attribute("value") ?? "");
            // Incomplete metadata does not qualify as a priced plate.
            if (!meta.ContainsKey("prediction") || !meta.ContainsKey("weight")) throw new InvalidDataException("Uma das mesas não contém peso e tempo completos. Refaça o fatiamento no Bambu Studio.");
            var grams = Number(meta["weight"]); var seconds = Number(meta["prediction"]);
            if (grams <= 0 || seconds <= 0) throw new InvalidDataException("Uma das mesas tem consumo ou tempo zerado. Confira o arquivo no Bambu Studio.");
            if (meta.GetValueOrDefault("outside") == "true") throw new InvalidDataException("O arquivo informa objetos fora da área de impressão. Confira no Bambu Studio.");
            list.Add(new Plate { Index = int.TryParse(meta.GetValueOrDefault("index"), out var idx) ? idx : list.Count + 1,
                Grams = grams, Seconds = seconds, Objects = plate.Elements("object").Count(x => (string?)x.Attribute("skipped") != "true"),
                Warning = string.Join("; ", plate.Elements("warning").Select(x => (string?)x.Attribute("msg")).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()) });
        }
        return list;
    }
    public static Plate ParseGcode(TextReader reader, int index = 1)
    {
        decimal? grams = null, seconds = null;
        // Bambu headers are at the beginning; do not infer weight from model volume.
        for (int i = 0; i < 4000 && reader.ReadLine() is { } line; i++)
        {
            var w = WeightRegex().Match(line);
            if (w.Success) grams = w.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries).Sum(x => Number(x.Trim()));
            var t = TimeRegex().Match(line);
            if (t.Success) seconds = ParseTime(t.Groups[1].Value);
            if (grams.HasValue && seconds.HasValue) break;
        }
        if (grams is not > 0 || seconds is not > 0) throw new InvalidDataException("O G-code não contém peso e tempo total reconhecíveis. Exporte um .3mf fatiado no Bambu Studio ou use a entrada manual.");
        return new Plate { Index = index, Grams = grams.Value, Seconds = seconds.Value };
    }
    public static decimal ParseTime(string value)
    {
        decimal total = 0;
        foreach (Match m in DurationRegex().Matches(value)) total += Number(m.Groups[1].Value) * (m.Groups[2].Value switch { "d" => 86400, "h" => 3600, "m" => 60, _ => 1 });
        return total;
    }
    static Dictionary<string, string> Settings(ZipArchive zip)
    {
        var entry = zip.GetEntry("Metadata/project_settings.config");
        if (entry == null) return [];
        using var doc = JsonDocument.Parse(ReadEntry(entry));
        return doc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetRawText());
    }
    public static string[] ChangedPrintSettings(IReadOnlyDictionary<string,string> before,IReadOnlyDictionary<string,string> after)
    {
        // "version" identifies the Studio release that wrote these metadata; it is
        // not a printing parameter. Only this exact, well-formed field is exempt.
        // Missing fields and changes to real print parameters still fail closed.
        return before.Where(pair=>
        {
            if(!after.TryGetValue(pair.Key,out var value))return true;
            using var a=JsonDocument.Parse(pair.Value);
            using var b=JsonDocument.Parse(value);
            if(JsonElement.DeepEquals(a.RootElement,b.RootElement))return false;
            if(pair.Key=="version"&&a.RootElement.ValueKind==JsonValueKind.String&&b.RootElement.ValueKind==JsonValueKind.String
                &&Version.TryParse(a.RootElement.GetString(),out _)&&Version.TryParse(b.RootElement.GetString(),out _))return false;
            return true;
        }).Select(pair=>pair.Key).ToArray();
    }
    static void CheckPostProcess(Dictionary<string, string> settings)
    {
        if (settings.TryGetValue("post_process", out var raw))
        {
            using var doc = JsonDocument.Parse(raw);
            bool nonempty = doc.RootElement.ValueKind == JsonValueKind.String ? !string.IsNullOrWhiteSpace(doc.RootElement.GetString()) :
                doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.EnumerateArray().Any(x => !string.IsNullOrWhiteSpace(x.ToString()));
            if (nonempty) throw new InvalidDataException("Este projeto usa pós-processamento externo. Abra e exporte o .3mf já fatiado no Bambu Studio para importá-lo.");
        }
    }
    static string Profile(Dictionary<string, string> settings)
    {
        string Read(string k)
        {
            if(!settings.TryGetValue(k,out var v))return "";
            using var doc=JsonDocument.Parse(v);
            return doc.RootElement.ValueKind==JsonValueKind.String?doc.RootElement.GetString()??"":doc.RootElement.ToString();
        }
        return string.Join(" · ", new[] { Read("printer_model"), Read("print_settings_id"), Read("filament_type").Replace("[", "").Replace("]", "").Replace("\"", "") }.Where(s => s.Length > 0));
    }
    static HashSet<int> ConfiguredPlates(ZipArchive zip)
    {
        var entry=zip.GetEntry("Metadata/model_settings.config");if(entry==null)return [];
        var result=new HashSet<int>();
        foreach(var p in Xml(ReadEntry(entry)).Descendants("plate").Where(x=>x.Elements("model_instance").Any()))
        {
            var value=p.Elements("metadata").FirstOrDefault(x=>(string?)x.Attribute("key")=="plater_id")?.Attribute("value")?.Value;
            if(int.TryParse(value,out var index))result.Add(index);
        }
        return result;
    }
    static void Thumbnail(ZipArchive zip, Quote q, Store store)
    {
        foreach(var p in q.Plates)
        {
            var pe=zip.GetEntry($"Metadata/plate_{p.Index}.png");if(pe==null||pe.Length>5_000_000)continue;
            var dir=Path.Combine(store.Root,"Imagens");Directory.CreateDirectory(dir);p.Thumbnail=Path.Combine(dir,q.Id.ToString("N")+"-placa-"+p.Index+".png");
            using var a=pe.Open();using var b=File.Create(p.Thumbnail);a.CopyTo(b);
        }
        var entry = zip.GetEntry("Metadata/plate_1.png") ?? zip.GetEntry("Metadata/thumbnail.png");
        if (entry == null || entry.Length > 5_000_000) return;
        var folder = Path.Combine(store.Root, "Imagens");Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, q.Id.ToString("N") + ".png");
        using var source = entry.Open();using var target = File.Create(path);source.CopyTo(target);q.Thumbnail = path;
    }
    public static async Task<Quote> ImportAsync(string source, string bambu, Store store, IProgress<string>? progress = null, CancellationToken cancel = default)
    {
        var ext = Path.GetExtension(source).ToLowerInvariant();
        if (ext is not ".3mf" and not ".gcode") throw new InvalidDataException("Selecione um .3mf do Bambu Studio ou um .gcode com consumo e tempo salvos.");
        if (!File.Exists(source)) throw new FileNotFoundException("O arquivo não foi encontrado.", source);
        var job = Path.Combine(store.Root, "Trabalhos", Guid.NewGuid().ToString("N"));Directory.CreateDirectory(job);
        var input = Path.Combine(job, "input" + ext);
        try
        {
            progress?.Report("Copiando o arquivo para análise…");
            // Every later read uses this immutable local snapshot, never the original path.
            await using (var a = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var b = File.Create(input)) await a.CopyToAsync(b, cancel);
            var q = new Quote { Name = Path.GetFileNameWithoutExtension(source).Replace(".gcode", "", StringComparison.OrdinalIgnoreCase), Source = Path.GetFullPath(source) };
            await using (var f = File.OpenRead(input)) q.Sha256 = Convert.ToHexString(await SHA256.HashDataAsync(f, cancel)).ToLowerInvariant();
            if (ext == ".gcode")
            {
                using var reader = File.OpenText(input);q.Plates = [ParseGcode(reader)];q.Method = "Dados salvos no G-code";return q;
            }
            Dictionary<string, string> before;
            using (var zip = ZipFile.OpenRead(input))
            {
                if (zip.Entries.Count > 10000 || zip.Entries.Sum(x => x.Length) > 4_000_000_000L) throw new InvalidDataException("Projeto grande demais para esta versão. Separe-o em arquivos menores.");
                before = Settings(zip);q.Profile = Profile(before);
                var info = zip.GetEntry("Metadata/slice_info.config");
                if (info != null) q.Plates = ParseSliceInfo(ReadEntry(info));
                var gcodes = zip.Entries.Where(x => Regex.IsMatch(x.FullName, @"(?:^|/)plate_\d+\.gcode$", RegexOptions.IgnoreCase)).OrderBy(x => x.FullName).ToArray();
                if (q.Plates.Count > 0)
                {
                    var configured=ConfiguredPlates(zip);
                    if(configured.Count>0&&!configured.SetEquals(q.Plates.Select(x=>x.Index)))throw new InvalidDataException("Há mesas configuradas sem dados de fatiamento completos. Fatie todas as mesas no Bambu Studio e salve o projeto antes de importar.");
                    var platesWithCode = gcodes.Select(x => int.Parse(Regex.Match(x.Name, @"\d+").Value)).ToHashSet();
                    if (platesWithCode.Count > 0 && !platesWithCode.SetEquals(q.Plates.Select(x => x.Index))) throw new InvalidDataException("Os dados das mesas estão incompletos. Exporte novamente o projeto fatiado.");
                    q.Method = "Dados de fatiamento salvos no .3mf";
                    Thumbnail(zip, q, store);return q;
                }
                if (gcodes.Length > 0)
                {
                    foreach (var entry in gcodes) { using var codeReader = new StreamReader(entry.Open());q.Plates.Add(ParseGcode(codeReader, int.Parse(Regex.Match(entry.Name, @"\d+").Value))); }
                    q.Method = "G-code incorporado no .3mf";Thumbnail(zip,q,store);return q;
                }
                if (before.Count == 0) throw new InvalidDataException("Este .3mf não tem as configurações do Bambu Studio. Configure-o no fatiador e salve o projeto antes de importar.");
                CheckPostProcess(before);
                // The stored model settings determine the configured printable plate set.
                q.Notes = "Arquivo inteiro tratado como uma unidade de venda. Confira as mesas e ajuste as unidades apenas se este arquivo já contiver várias unidades iguais.";
                Thumbnail(zip,q,store);
            }
            bambu=SlicingEngine.Resolve(bambu);SlicingEngine.Validate(bambu);
            progress?.Report(SlicingEngine.IsBundled(bambu)?"Fatiando com o motor integrado…":"Fatiando com as configurações do arquivo…");
            var infoProcess = new ProcessStartInfo(bambu) { WorkingDirectory = job, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WindowStyle = ProcessWindowStyle.Hidden };
            SlicingEngine.Configure(infoProcess,job);
            // no-check avoids the CLI's cloud-upload preset rewriting (notably prime volume).
            // The original project and all saved settings are independently checked below.
            foreach (var arg in new[] { "--debug", "2", "--no-check", "--slice", "0", "--outputdir", job, "--export-3mf", "analisado.3mf", input }) infoProcess.ArgumentList.Add(arg);
            using var p = Process.Start(infoProcess) ?? throw new InvalidOperationException("Não foi possível iniciar o Bambu Studio.");
            var stdout = p.StandardOutput.ReadToEndAsync(cancel);var stderr = p.StandardError.ReadToEndAsync(cancel);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);timeout.CancelAfter(TimeSpan.FromMinutes(10));
            try { await p.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                if (cancel.IsCancellationRequested) throw;
                throw new TimeoutException("O fatiamento ultrapassou dez minutos. Fatie o projeto no Bambu Studio e importe o arquivo salvo.");
            }
            await Task.WhenAll(stdout,stderr);
            var resultFile = Path.Combine(job, "result.json");
            if (!File.Exists(resultFile)) throw new InvalidDataException("O Bambu Studio não devolveu o resultado. Confira o arquivo e o caminho do programa selecionado.");
            using var result = JsonDocument.Parse(await File.ReadAllTextAsync(resultFile,cancel));var r = result.RootElement;
            if (p.ExitCode != 0 || r.GetProperty("return_code").GetInt32() != 0) throw new InvalidDataException("O Bambu Studio não concluiu: " + r.GetProperty("error_string").GetString());
            foreach (var plate in r.GetProperty("sliced_plates").EnumerateArray())
            {
                q.Plates.Add(new Plate { Index = plate.GetProperty("id").GetInt32(), Grams = plate.GetProperty("filaments").EnumerateArray().Sum(x => x.GetProperty("total_used_g").GetDecimal()),
                    Seconds = plate.GetProperty("total_predication").GetDecimal(), Objects = plate.TryGetProperty("objects",out var objs) ? objs.GetArrayLength() : 0,
                    Warning = plate.TryGetProperty("warning_message",out var w) ? w.ToString() : "" });
            }
            if (q.Plates.Count == 0 || q.Plates.Any(x => x.Grams <= 0 || x.Seconds <= 0)) throw new InvalidDataException("O fatiador não retornou consumo completo para todas as mesas.");
            var exported = Path.Combine(job,"analisado.3mf");
            if (!File.Exists(exported)) throw new InvalidDataException("Não foi possível conferir as configurações usadas no fatiamento.");
            using (var zip = ZipFile.OpenRead(exported))
            {
                var after = Settings(zip);
                var changed = ChangedPrintSettings(before,after);
                if (changed.Length > 0) throw new InvalidDataException("O Bambu Studio alterou configurações durante a análise ("+string.Join(", ",changed.Take(5))+"). Fatie na interface do Bambu Studio e importe o .3mf já fatiado.");
                if(before.TryGetValue("version",out var oldVersion)&&after.TryGetValue("version",out var newVersion)
                    &&!JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(oldVersion),JsonSerializer.Deserialize<JsonElement>(newVersion)))
                    q.Notes+=$"\r\nCompatibilidade: projeto salvo no Bambu Studio {JsonSerializer.Deserialize<string>(oldVersion)} e analisado no {JsonSerializer.Deserialize<string>(newVersion)}. A identificação da versão foi atualizada; os parâmetros de impressão originais foram conferidos.";
            }
            using(var zip=ZipFile.OpenRead(input))
            {
                Thumbnail(zip,q,store);
                var expected=ConfiguredPlates(zip);
                if(expected.Count>0&&!expected.SetEquals(q.Plates.Select(x=>x.Index)))throw new InvalidDataException("Nem todas as mesas configuradas foram analisadas. Fatie todas as mesas e importe o projeto salvo.");
            }
            q.Method = SlicingEngine.IsBundled(bambu)?$"Motor integrado BambuStudio {SlicingEngine.Version} · configurações conferidas":"Fatiamento automático local · configurações conferidas";
            return q;
        }
        finally
        {
            // Only the task-created GUID directory under Trabalhos is eligible for cleanup.
            try { Directory.Delete(job, recursive:true); } catch { }
        }
    }
    [GeneratedRegex(@"^;\s*total filament weight \[g\]\s*:\s*([\d.,\s]+)",RegexOptions.IgnoreCase)] private static partial Regex WeightRegex();
    [GeneratedRegex(@"total estimated time\s*:\s*(.+)$",RegexOptions.IgnoreCase)] private static partial Regex TimeRegex();
    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*([dhms])",RegexOptions.IgnoreCase)] private static partial Regex DurationRegex();
}
