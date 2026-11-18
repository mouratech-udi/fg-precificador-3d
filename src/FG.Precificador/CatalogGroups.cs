using System.Globalization;
using System.Text;

namespace FG.Precificador;
public static class CatalogGroups
{
    public static string PathName(AppData data,Guid? id)
    {
        var names=new List<string>();var seen=new HashSet<Guid>();
        while(id.HasValue&&seen.Add(id.Value))
        {var g=data.Groups.FirstOrDefault(x=>x.Id==id);if(g==null)break;names.Insert(0,g.Name);id=g.ParentId;}
        return string.Join(" / ",names);
    }
    public static bool InGroup(AppData data,Guid? quoteGroup,Guid filter)
    {
        var seen=new HashSet<Guid>();
        while(quoteGroup.HasValue&&seen.Add(quoteGroup.Value)){if(quoteGroup==filter)return true;quoteGroup=data.Groups.FirstOrDefault(x=>x.Id==quoteGroup)?.ParentId;}
        return false;
    }
    static string Key(string value)=>new string(value.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant();
    public static IEnumerable<Quote> Filter(AppData data,string search,Guid? group,bool ungrouped=false)
    {
        var key=Key(search.Trim());return data.Quotes.Where(q=>(key.Length==0||Key(q.Name).Contains(key))&&(!ungrouped||q.GroupId==null)&&(!group.HasValue||InGroup(data,q.GroupId,group.Value)));
    }
    public static QuoteGroup Create(AppData data,string name,Guid? parent)
    {
        name=name.Trim();if(name.Length is 0 or >80)throw new InvalidDataException("Informe um nome de grupo com até 80 caracteres.");
        if(parent.HasValue&&!data.Groups.Any(x=>x.Id==parent))throw new InvalidDataException("Grupo principal não encontrado.");
        if(data.Groups.Any(x=>x.ParentId==parent&&string.Equals(x.Name,name,StringComparison.CurrentCultureIgnoreCase)))throw new InvalidDataException("Já existe um grupo com esse nome neste nível.");
        var g=new QuoteGroup{Name=name,ParentId=parent};data.Groups.Add(g);return g;
    }
    public static int Move(AppData data,IEnumerable<Guid> ids,Guid? group)
    {
        if(group.HasValue&&!data.Groups.Any(x=>x.Id==group))throw new InvalidDataException("Grupo não encontrado.");
        var chosen=ids.ToHashSet();int count=0;foreach(var q in data.Quotes.Where(q=>chosen.Contains(q.Id))){q.GroupId=group;count++;}return count;
    }
}
