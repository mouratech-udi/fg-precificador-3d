using System.Net.Http;
using System.Text.Json;

namespace FG.Precificador;
public sealed record ReleaseInfo(Version Version,string Url);
public static class UpdateChecker
{
    public static Version Current=>typeof(UpdateChecker).Assembly.GetName().Version??new Version(1,0,0);
    public static ReleaseInfo? Parse(string json,Version current)
    {
        using var doc=JsonDocument.Parse(json);var r=doc.RootElement;
        if(r.GetProperty("draft").GetBoolean()||r.GetProperty("prerelease").GetBoolean())return null;
        if(!Version.TryParse(r.GetProperty("tag_name").GetString()?.TrimStart('v','V'),out var version))return null;
        // GitHub tags have three parts while assembly versions have four.
        var normalized=new Version(current.Major,current.Minor,Math.Max(current.Build,0));if(version<=normalized)return null;
        var url=r.GetProperty("html_url").GetString()??"";
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/mouratech-udi/fg-precificador-3d/releases/",StringComparison.Ordinal))throw new InvalidDataException("Endereço de atualização inválido.");
        return new(version,url);
    }
    public static async Task<ReleaseInfo?> Check(CancellationToken cancel=default)
    {
        using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(15)};client.DefaultRequestHeaders.UserAgent.ParseAdd("FG-Precificador-3D/"+Current.ToString(3));
        return Parse(await client.GetStringAsync("https://api.github.com/repos/mouratech-udi/fg-precificador-3d/releases/latest",cancel),Current);
    }
}
