using System.Globalization;
using System.Text.Json;
using System.IO.Compression;

namespace FG.Precificador;

public sealed class PricingSettings
{
    public decimal FilamentPerKg { get; set; } = 100;
    public decimal EnergyPerKwh { get; set; } = 1.20m;
    public decimal Watts { get; set; } = 120;
    public decimal MachinePerHour { get; set; } = 1;
    public decimal TimePerHour { get; set; }
    public decimal Labor { get; set; } = 20;
    public decimal MarkupPercent { get; set; } = 100;
    public PricingSettings Copy() => (PricingSettings)MemberwiseClone();
}

public sealed class Plate
{
    public int Index { get; set; }
    public decimal Grams { get; set; }
    public decimal Seconds { get; set; }
    public int Objects { get; set; }
    public string Warning { get; set; } = "";
}

public sealed class Quote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    public string Name { get; set; } = "Nova peça";
    public string Source { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Method { get; set; } = "Entrada manual";
    public string Profile { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Thumbnail { get; set; } = "";
    public bool Manual { get; set; }
    public List<Plate> Plates { get; set; } = [];
    public PricingSettings Settings { get; set; } = new();
    public int UnitsInFile { get; set; } = 1;
    public int OrderQuantity { get; set; } = 1;
    public decimal ExtraGrams { get; set; }
    public decimal OtherPerUnit { get; set; }
    public bool Shopee { get; set; }
    public decimal ShopeePercent { get; set; } = 20;
    public decimal ShopeeFixed { get; set; } = 2;
    public decimal Grams => Plates.Sum(x => x.Grams);
    public decimal Seconds => Plates.Sum(x => x.Seconds);
    public override string ToString() => Name;
}

public sealed record Price(decimal Material, decimal Energy, decimal Machine, decimal Time, decimal Other,
    decimal PrintCost, decimal DirectTarget, decimal Minimum, decimal Suggested, decimal Fee, decimal Profit, decimal OrderTotal);

public static class Pricing
{
    public static decimal Up(decimal value) => decimal.Ceiling(value * 100) / 100;
    public static Price Calculate(Quote q)
    {
        var s = q.Settings;
        if (q.UnitsInFile < 1 || q.OrderQuantity < 1 || q.Grams <= 0 || q.Seconds <= 0)
            throw new InvalidDataException("Informe material e tempo maiores que zero e pelo menos uma unidade.");
        if (new[] { s.FilamentPerKg, s.EnergyPerKwh, s.Watts, s.MachinePerHour, s.TimePerHour, s.Labor, s.MarkupPercent, q.ExtraGrams, q.OtherPerUnit }.Any(x => x < 0))
            throw new InvalidDataException("Os custos não podem ser negativos.");
        if (q.Shopee && (q.ShopeePercent < 0 || q.ShopeePercent >= 100 || q.ShopeeFixed < 2 || q.ShopeeFixed > 5.5m))
            throw new InvalidDataException("Confira as taxas: percentual de 0 a 99,99% e fixa de R$ 2,00 a R$ 5,50.");
        decimal hours = q.Seconds / 3600, n = q.UnitsInFile;
        var material = (q.Grams + q.ExtraGrams) / 1000 * s.FilamentPerKg / n;
        var energy = hours * s.Watts / 1000 * s.EnergyPerKwh / n;
        var machine = hours * s.MachinePerHour / n;
        var time = hours * s.TimePerHour / n;
        var cost = material + energy + machine + time + q.OtherPerUnit;
        var direct = cost * (1 + s.MarkupPercent / 100) + s.Labor;
        var rate = q.Shopee ? q.ShopeePercent / 100 : 0;
        var fixedFee = q.Shopee ? q.ShopeeFixed : 0;
        var minimum = Up((direct + fixedFee) / (1 - rate));
        var suggested = decimal.Ceiling(minimum);
        var fee = minimum * rate + fixedFee;
        return new(material, energy, machine, time, q.OtherPerUnit, cost, direct, minimum, suggested, fee,
            minimum - fee - cost - s.Labor, suggested * q.OrderQuantity);
    }
    public static string Money(decimal value) => value.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));
    public static string Duration(decimal seconds)
    {
        var t = TimeSpan.FromSeconds((double)seconds);
        return $"{(int)t.TotalHours}h {t.Minutes:00}min {t.Seconds:00}s";
    }
}

public sealed class AppData
{
    public int Version { get; set; } = 1;
    public PricingSettings Defaults { get; set; } = new();
    public string BambuPath { get; set; } = "";
    public List<Quote> Quotes { get; set; } = [];
    public List<DeletedQuote> DeletedQuotes { get; set; } = [];
    public CompanyProfile Company { get; set; } = new();
}

public sealed class DeletedQuote
{
    public Quote Quote { get; set; } = new();
    public int OriginalIndex { get; set; }
    public DateTimeOffset DeletedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class Store
{
    public string Root { get; }
    public string DataPath => Path.Combine(Root, "orcamentos.json");
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public Store(string? root = null)
    {
        Root = Path.GetFullPath(root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FG Design Print", "Precificador"));
        Directory.CreateDirectory(Root);
    }
    public AppData Load()
    {
        if (File.Exists(DataPath))
        {
            // A damaged history must not be silently replaced by an empty file.
            try { return Previews(JsonSerializer.Deserialize<AppData>(File.ReadAllText(DataPath), Json) ?? throw new InvalidDataException("Histórico vazio.")); }
            catch (Exception ex) { throw new InvalidDataException($"Não foi possível ler o histórico. O arquivo foi preservado em {DataPath}. {ex.Message}", ex); }
        }
        // Every new installation starts without the publisher's products or personal paths.
        return new AppData { BambuPath = Slicer.FindBambu() };
    }
    AppData Previews(AppData data)
    {
        var images=Path.Combine(Root,"Imagens");Directory.CreateDirectory(images);
        foreach(var q in data.Quotes)
        {
            try
            {
                if(File.Exists(q.Thumbnail))continue;
                if(!File.Exists(q.Source))continue;
                using var zip=ZipFile.OpenRead(q.Source);var entry=zip.GetEntry("Metadata/plate_1.png");
                if(entry==null||entry.Length>5_000_000)continue;
                q.Thumbnail=Path.Combine(images,q.Id.ToString("N")+".png");using var input=entry.Open();using var output=File.Create(q.Thumbnail);input.CopyTo(output);
            }catch{ /* Initial previews are optional; pricing data remain available. */ }
        }
        return data;
    }
    public void Save(AppData data)
    {
        var temp = DataPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(data, Json));
        if (File.Exists(DataPath)) File.Replace(temp, DataPath, DataPath + ".bak");
        else File.Move(temp, DataPath);
    }
    public bool DeleteQuote(AppData data,Guid id)
    {
        var index=data.Quotes.FindIndex(q=>q.Id==id);if(index<0)return false;
        var candidate=new AppData {Version=data.Version,Defaults=data.Defaults,BambuPath=data.BambuPath,Company=data.Company,
            Quotes=[..data.Quotes],DeletedQuotes=[..data.DeletedQuotes]};
        candidate.DeletedQuotes.Add(new DeletedQuote{Quote=candidate.Quotes[index],OriginalIndex=index});
        candidate.Quotes.RemoveAt(index);
        Save(candidate); // Publish the in-memory change only after a successful disk write.
        data.Quotes=candidate.Quotes;data.DeletedQuotes=candidate.DeletedQuotes;return true;
    }
    public Quote? RestoreLastDeleted(AppData data)
    {
        if(data.DeletedQuotes.Count==0)return null;
        var entry=data.DeletedQuotes[^1];
        if(data.Quotes.Any(q=>q.Id==entry.Quote.Id))throw new InvalidDataException("Este orçamento já está na lista.");
        var candidate=new AppData {Version=data.Version,Defaults=data.Defaults,BambuPath=data.BambuPath,Company=data.Company,
            Quotes=[..data.Quotes],DeletedQuotes=[..data.DeletedQuotes]};
        candidate.Quotes.Insert(Math.Clamp(entry.OriginalIndex,0,candidate.Quotes.Count),entry.Quote);
        candidate.DeletedQuotes.RemoveAt(candidate.DeletedQuotes.Count-1);
        Save(candidate);data.Quotes=candidate.Quotes;data.DeletedQuotes=candidate.DeletedQuotes;return entry.Quote;
    }
}
