using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;

namespace FG.Precificador;

public sealed record MeshFace(Vector3 A,Vector3 B,Vector3 C,Color Color);
public sealed class ProjectMesh
{
    public List<MeshFace> Faces { get; }=[];
    public Vector3 Min { get; set; }=new(float.MaxValue);
    public Vector3 Max { get; set; }=new(float.MinValue);
    public bool Simplified { get; set; }
    public string Message { get; set; }="";
}
public static class Project3D
{
    public static async Task VerifySource(Quote q,CancellationToken cancel=default)
    {
        if(!File.Exists(q.Source))throw new FileNotFoundException("Arquivo original não encontrado. Use Abrir projeto para localizar novamente.");
        if(q.Sha256.Length==0)return;
        await using var stream=new FileStream(q.Source,FileMode.Open,FileAccess.Read,FileShare.Read);
        var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,cancel).ConfigureAwait(false));
        if(!hash.Equals(q.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Este arquivo foi alterado desde o orçamento. Importe-o novamente para conferir peso, tempo e visualização juntos. O orçamento anterior foi preservado.");
    }
    public static XDocument ReadXml(ZipArchiveEntry entry)
    {
        if(entry.Length>150_000_000)throw new InvalidDataException("Esta malha é grande demais para a visualização. Abra o arquivo no fatiador.");
        using var input=entry.Open();using var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=150_000_000});return XDocument.Load(reader);
    }
    static IEnumerable<XElement> Children(XElement? root,string name)=>root?.Elements().Where(e=>e.Name.LocalName==name)??[];
    static XElement? Child(XElement? root,string name)=>Children(root,name).FirstOrDefault();
    public static Matrix4x4 Transform(string? text)
    {
        if(string.IsNullOrWhiteSpace(text))return Matrix4x4.Identity;
        var v=text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(x=>float.Parse(x,CultureInfo.InvariantCulture)).ToArray();
        if(v.Length!=12||v.Any(x=>!float.IsFinite(x)))throw new InvalidDataException("Transformação 3D inválida.");
        return new(v[0],v[1],v[2],0,v[3],v[4],v[5],0,v[6],v[7],v[8],0,v[9],v[10],v[11],1);
    }
    public static ProjectMesh Load(string path,int? plate=null,CancellationToken cancel=default)
    {
        if(!File.Exists(path))throw new FileNotFoundException("Arquivo original não encontrado. Use Abrir projeto para localizar novamente.");
        if(!path.EndsWith(".3mf",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("A visualização 3D está disponível para projetos 3MF que contenham a malha.");
        using var zip=ZipFile.OpenRead(path);var result=new ProjectMesh();var docs=new Dictionary<string,XDocument>();
        string Normalize(string name)=>Uri.UnescapeDataString(name).Replace('\\','/').TrimStart('/');
        XDocument Doc(string name)
        {
            name=Normalize(name);if(docs.TryGetValue(name,out var d))return d;
            if(docs.Count>128)throw new InvalidDataException("Projeto com componentes demais para esta visualização.");
            var entry=zip.GetEntry(name)??throw new InvalidDataException("Componente 3D ausente: "+name);
            return docs[name]=ReadXml(entry);
        }
        var rootName="3D/3dmodel.model";
        if(zip.GetEntry("_rels/.rels") is{}rels)
        {
            var target=ReadXml(rels).Descendants().FirstOrDefault(e=>e.Name.LocalName=="Relationship"&&((string?)e.Attribute("Type"))?.EndsWith("/3dmodel")==true)?.Attribute("Target")?.Value;
            if(target!=null)rootName=Normalize(target);
        }
        var root=Doc(rootName);var allItems=Children(Child(root.Root,"build"),"item").ToArray();
        var instances=new HashSet<(int,int)>();
        if(plate.HasValue&&zip.GetEntry("Metadata/model_settings.config") is{}meta)
        {
            var p=ReadXml(meta).Descendants("plate").FirstOrDefault(e=>e.Elements("metadata").Any(m=>(string?)m.Attribute("key")=="plater_id"&&(string?)m.Attribute("value")==plate.Value.ToString(CultureInfo.InvariantCulture)));
            foreach(var i in p?.Elements("model_instance")??[])
            {
                string? Value(string k)=>i.Elements("metadata").FirstOrDefault(m=>(string?)m.Attribute("key")==k)?.Attribute("value")?.Value;
                if(int.TryParse(Value("object_id"),out var id)&&int.TryParse(Value("instance_id"),out var ix))instances.Add((id,ix));
            }
            if(instances.Count==0)throw new InvalidDataException("Não foi possível identificar os objetos desta placa. Escolha Todas as placas.");
        }
        var palette=new[]{Color.FromArgb(0,151,164),Color.FromArgb(234,112,48),Color.FromArgb(164,90,191),Color.FromArgb(220,72,113)};
        var stack=new HashSet<string>();
        void AddObject(string part,int id,Matrix4x4 matrix,int color,int depth)
        {
            cancel.ThrowIfCancellationRequested();if(depth>32)throw new InvalidDataException("Hierarquia 3D profunda demais.");
            part=Normalize(part);var key=part+":"+id;if(!stack.Add(key))throw new InvalidDataException("Referência circular no modelo 3D.");
            try
            {
                var doc=Doc(part);var obj=Children(Child(doc.Root,"resources"),"object").FirstOrDefault(e=>(int?)e.Attribute("id")==id)??throw new InvalidDataException("Objeto 3D ausente.");
                var mesh=Child(obj,"mesh");
                if(mesh!=null)
                {
                    var vertices=Children(Child(mesh,"vertices"),"vertex").Select(v=>Vector3.Transform(new Vector3(float.Parse(v.Attribute("x")!.Value,CultureInfo.InvariantCulture),float.Parse(v.Attribute("y")!.Value,CultureInfo.InvariantCulture),float.Parse(v.Attribute("z")!.Value,CultureInfo.InvariantCulture)),matrix)).ToArray();
                    foreach(var v in vertices){if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))throw new InvalidDataException("Coordenada 3D inválida.");result.Min=Vector3.Min(result.Min,v);result.Max=Vector3.Max(result.Max,v);}
                    var triangles=Children(Child(mesh,"triangles"),"triangle").ToArray();int stride=Math.Max(1,(int)Math.Ceiling(triangles.Length/35000d));result.Simplified|=stride>1;
                    for(int i=0;i<triangles.Length;i+=stride)
                    {
                        if(result.Faces.Count>=120000){result.Simplified=true;break;}
                        var t=triangles[i];var a=(int)t.Attribute("v1")!;var b=(int)t.Attribute("v2")!;var c=(int)t.Attribute("v3")!;
                        if(a<0||b<0||c<0||a>=vertices.Length||b>=vertices.Length||c>=vertices.Length)throw new InvalidDataException("Triângulo inválido na malha.");
                        result.Faces.Add(new(vertices[a],vertices[b],vertices[c],palette[color%palette.Length]));
                    }
                }
                foreach(var component in Children(Child(obj,"components"),"component"))
                {
                    var reference=component.Attributes().FirstOrDefault(a=>a.Name.LocalName=="path")?.Value;
                    var next=reference==null?part:reference.StartsWith('/')?Normalize(reference):Normalize(new Uri(new Uri("https://3mf.local/"+part),reference).AbsolutePath);
                    AddObject(next,(int)component.Attribute("objectid")!,Transform((string?)component.Attribute("transform"))*matrix,color,depth+1);
                }
            }finally{stack.Remove(key);}
        }
        var counts=new Dictionary<int,int>();int col=0;
        var unit=(string?)root.Root?.Attribute("unit");var factor=unit switch{"micron"=>.001f,"centimeter"=>10f,"meter"=>1000f,"inch"=>25.4f,"foot"=>304.8f,_=>1f};
        foreach(var item in allItems)
        {
            var id=(int)item.Attribute("objectid")!;var ix=counts.GetValueOrDefault(id);counts[id]=ix+1;
            if(instances.Count>0&&!instances.Contains((id,ix)))continue;
            var part=item.Attributes().FirstOrDefault(a=>a.Name.LocalName=="path")?.Value??rootName;
            AddObject(part,id,Transform((string?)item.Attribute("transform"))*Matrix4x4.CreateScale(factor),col++,0);
        }
        if(result.Faces.Count==0)throw new InvalidDataException("Este arquivo não contém uma malha 3D disponível para exibição.");
        result.Message=result.Simplified?"Visualização simplificada para manter a navegação leve.":"Cores ilustrativas por objeto; a pintura do fatiador é preservada no arquivo.";return result;
    }
}

public sealed class MeshViewer : Control
{
    ProjectMesh? mesh;float yaw=-.6f,pitch=.75f,zoom=1;Point last;PointF pan;bool dragging;
    string EmptyText { get; set; }="Selecione um projeto para visualizar em 3D.";
    public MeshViewer(){DoubleBuffered=true;BackColor=Color.FromArgb(242,246,250);TabStop=true;AccessibleName="Visualização 3D: arraste para girar, roda do mouse para ampliar";}
    public void SetMesh(ProjectMesh? value,string? message=null){mesh=value;if(message!=null)EmptyText=message;ResetView();}
    public void ResetView(){yaw=-.6f;pitch=.75f;zoom=1;pan=default;Invalidate();}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();last=e.Location;dragging=true;Capture=true;}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;if(e.Button==MouseButtons.Right){pan.X+=e.X-last.X;pan.Y+=e.Y-last.Y;}else{yaw+=(e.X-last.X)*.012f;pitch=Math.Clamp(pitch+(e.Y-last.Y)*.012f,-1.55f,1.55f);}last=e.Location;Invalidate();}
    protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);zoom=Math.Clamp(zoom*(e.Delta>0?1.15f:.87f),.15f,8);Invalidate();}
    protected override void OnResize(EventArgs e){base.OnResize(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.Clear(BackColor);
        if(mesh==null){TextRenderer.DrawText(g,EmptyText,Font,ClientRectangle,Color.FromArgb(82,99,114),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);return;}
        var center=(mesh.Min+mesh.Max)*.5f;var span=mesh.Max-mesh.Min;float extent=span.Length();if(extent<=0)return;
        var matrix=Matrix4x4.CreateRotationZ(yaw)*Matrix4x4.CreateRotationX(pitch);float scale=Math.Max(1,Math.Min(Width-32,Height-88))*.84f/extent*zoom;
        Vector3 V(Vector3 v)=>Vector3.Transform(v-center,matrix);
        PointF P(Vector3 v)=>new(Width*.5f+pan.X+v.X*scale,Height*.51f+pan.Y-v.Z*scale);
        var faces=mesh.Faces.Select(f=>{var a=V(f.A);var b=V(f.B);var c=V(f.C);return(f,a,b,c,z:(a.Y+b.Y+c.Y)/3);}).OrderByDescending(f=>f.z);
        var brushes=new Dictionary<int,SolidBrush>();
        try
        {
            foreach(var (f,a,b,c,_) in faces)
            {
                var cross=Vector3.Cross(b-a,c-a);if(cross.LengthSquared()<1e-12f)continue;
                var n=Vector3.Normalize(cross);var shade=(int)Math.Clamp((.45f+.55f*Math.Abs(Vector3.Dot(n,Vector3.Normalize(new Vector3(-.3f,-.5f,1)))))*15,0,15);
                var key=f.Color.ToArgb()^shade;if(!brushes.TryGetValue(key,out var brush)){float brightness=.4f+shade/15f*.6f;brush=new SolidBrush(Color.FromArgb((int)(f.Color.R*brightness),(int)(f.Color.G*brightness),(int)(f.Color.B*brightness)));brushes[key]=brush;}
                g.FillPolygon(brush,[P(a),P(b),P(c)]);
            }
        }finally{foreach(var brush in brushes.Values)brush.Dispose();}
        TextRenderer.DrawText(g,$"{span.X:N1} × {span.Y:N1} × {span.Z:N1} mm",Font,new Point(12,12),Color.FromArgb(18,39,62));
        TextRenderer.DrawText(g,"Arraste: girar  •  Botão direito: mover  •  Roda: zoom",Font,new Rectangle(12,Height-33,Width-24,28),Color.FromArgb(82,99,114));
    }
}
