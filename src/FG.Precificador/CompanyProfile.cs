using System.Drawing.Imaging;

namespace FG.Precificador;

public sealed class CompanyProfile
{
    public string Name { get; set; } = "";
    public string Document { get; set; } = "";
    public string Instagram { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Website { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string PaymentTerms { get; set; } = "";
    public string DeliveryTerms { get; set; } = "";
    public string Notes { get; set; } = "";
    public int ValidityDays { get; set; }
    // An embedded PNG keeps the logo with local backups and works after the source image is moved.
    public string LogoPngBase64 { get; set; } = "";
    public CompanyProfile Copy() => (CompanyProfile)MemberwiseClone();
    public static CompanyProfile Fg()
    {
        var profile=new CompanyProfile{Name="FG Design Print",Instagram="@fgdesigneprint"};
        using var source=typeof(CompanyProfile).Assembly.GetManifestResourceStream("FG.Marca");
        if(source!=null){using var bytes=new MemoryStream();source.CopyTo(bytes);profile.LogoPngBase64=Convert.ToBase64String(bytes.ToArray());}
        return profile;
    }
    public static string NormalizeInstagram(string value)
    {
        value=value.Trim();
        if(Uri.TryCreate(value,UriKind.Absolute,out var uri)&&(uri.Host.Equals("instagram.com",StringComparison.OrdinalIgnoreCase)||uri.Host.Equals("www.instagram.com",StringComparison.OrdinalIgnoreCase)))
            value=uri.AbsolutePath.Trim('/').Split('/')[0];
        return value.Length==0?"":"@"+value.TrimStart('@');
    }
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Name))throw new InvalidDataException("Informe o nome da empresa na aba Empresa / PDF do cliente.");
        if(Name.Length>120||Document.Length>40||Instagram.Length>120||Phone.Length>80||Email.Length>160||Website.Length>200||Address.Length>240||City.Length>120||ContactName.Length>120||PaymentTerms.Length>500||DeliveryTerms.Length>500||Notes.Length>1600||ValidityDays<0||ValidityDays>3650)
            throw new InvalidDataException("Confira o tamanho dos dados da empresa e a validade do orçamento.");
        if(LogoPngBase64.Length>0)
        {
            if(LogoPngBase64.Length>10_000_000)throw new InvalidDataException("A logo armazenada é grande demais. Escolha outra imagem.");
            try
            {
                using var stream=new MemoryStream(Convert.FromBase64String(LogoPngBase64));using var image=Image.FromStream(stream);
                if((long)image.Width*image.Height>20_000_000)throw new InvalidDataException("A logo tem resolução grande demais.");
            }
            catch(Exception ex) when(ex is ArgumentException or FormatException or OutOfMemoryException){throw new InvalidDataException("Não foi possível ler a logo da empresa. Escolha outra imagem.",ex);}
        }
    }
    public static string ReadLogo(string path)
    {
        if(new FileInfo(path).Length>10_000_000)throw new InvalidDataException("Escolha uma imagem PNG ou JPG de até 10 MB.");
        using var file=File.OpenRead(path);using var image=Image.FromStream(file);
        if((long)image.Width*image.Height>20_000_000)throw new InvalidDataException("Escolha uma logo com resolução menor (até 20 megapixels).");
        var scale=Math.Min(1,Math.Min(1800d/image.Width,1800d/image.Height));
        using var bitmap=new Bitmap(Math.Max(1,(int)(image.Width*scale)),Math.Max(1,(int)(image.Height*scale)),PixelFormat.Format32bppArgb);
        using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.Transparent);graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;graphics.DrawImage(image,0,0,bitmap.Width,bitmap.Height);}
        using var output=new MemoryStream();bitmap.Save(output,ImageFormat.Png);return Convert.ToBase64String(output.ToArray());
    }
    public IEnumerable<string> ContactLines()
    {
        yield return Name;
        if(Document.Length>0)yield return "CPF/CNPJ: "+Document;
        if(ContactName.Length>0)yield return "Responsável: "+ContactName;
        if(Phone.Length>0)yield return "Telefone / WhatsApp: "+Phone;
        if(Email.Length>0)yield return "E-mail: "+Email;
        if(Instagram.Length>0)yield return "Instagram: "+NormalizeInstagram(Instagram);
        if(Website.Length>0)yield return "Site: "+Website;
        if(Address.Length>0)yield return Address;
        if(City.Length>0)yield return City;
    }
}
