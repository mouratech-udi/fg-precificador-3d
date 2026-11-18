namespace FG.Precificador;

public sealed partial class MainForm
{
    internal static int TestSyntheticImport(string output)
    {
        var root=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!,"catalogo-nativo-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var source=Path.Combine(root,"Novo projeto.3mf");
        using(var zip=System.IO.Compression.ZipFile.Open(source,System.IO.Compression.ZipArchiveMode.Create))
        {using var writer=new StreamWriter(zip.CreateEntry("Metadata/slice_info.config").Open());writer.Write("<config><plate><metadata key='index' value='1'/><metadata key='weight' value='12'/><metadata key='prediction' value='300'/></plate></config>");}
        var store=new Store(Path.Combine(root,"historico"));store.Save(new AppData{Quotes=[new Quote{Name="Satoro Gojo",Plates=[new Plate{Index=1,Grams=45,Seconds=3600}]}]});
        return TestLiveImport(source,store.DataPath,output);
    }
    internal static int TestLiveImport(string source,string history,string output)
    {
        ApplicationConfiguration.Initialize();var store=new Store(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!,"importacao-"+Guid.NewGuid().ToString("N")));
        var state=System.Text.Json.JsonSerializer.Deserialize<AppData>(File.ReadAllText(history),Store.Json)!;state.CheckUpdatesOnStartup=false;
        var deletedBefore=state.DeletedQuotes.Count;var countBefore=state.Quotes.Count;var before=state.Quotes.Select(q=>q.Id).ToHashSet();
        using var form=new MainForm(store,state){ShowInTaskbar=false,Opacity=0};int exitCode=1;
        form.Shown+=async(_,_)=>
        {
            try
            {
                form.searchBox.Text="Satoro";
                await form.Import([source]);
                await Task.Delay(750);
                var added=state.Quotes.Where(q=>!before.Contains(q.Id)).ToArray();
                var listRect=form.catalog.RectangleToScreen(form.catalog.ClientRectangle);var filterRect=form.searchBox.Parent!.RectangleToScreen(form.searchBox.Parent.ClientRectangle);
                bool noOverlap=!listRect.IntersectsWith(filterRect);
                var sizes=new List<object>();
                foreach(var size in new[]{new Size(1200,800),new Size(1380,920),new Size(1600,1000)})
                {
                    form.Size=size;form.PerformLayout();await Task.Yield();
                    var list=form.catalog.RectangleToScreen(form.catalog.ClientRectangle);var filters=form.searchBox.Parent.RectangleToScreen(form.searchBox.Parent.ClientRectangle);var actions=form.openProjectButton.Parent!.RectangleToScreen(form.openProjectButton.Parent.ClientRectangle);
                    var visible=list.Top>=filters.Bottom&&list.Bottom<=actions.Top&&form.catalog.GetItemRectangle(0).Top>=0;
                    noOverlap&=visible;sizes.Add(new{width=size.Width,height=size.Height,noOverlap=visible,listHeight=list.Height});
                }
                bool success=added.Length==1&&form.Current?.Id==added[0].Id&&form.title.Text==added[0].Name&&form.name.Text==added[0].Name&&state.DeletedQuotes.Count==deletedBefore&&noOverlap;
                File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{success,noOverlap,sizes,listTop=listRect.Top,filtersBottom=filterRect.Bottom,countBefore,countAfter=state.Quotes.Count,added=added.Select(q=>new{q.Id,q.Name,q.Source,q.Grams,q.Seconds}),selected=form.Current?.Name,selectedId=form.Current?.Id,panel=form.title.Text,selectionCount=form.catalog.SelectedItems.Count,first=state.Quotes[0].Name,deleted=state.DeletedQuotes.Count},Store.Json));exitCode=success?0:1;
            }
            catch(Exception ex){File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{success=false,error=ex.ToString()},Store.Json));}
            finally{form.Close();}
        };
        Application.Run(form);return exitCode;
    }
    internal static void TestCatalogRefresh(Action<bool,string> check,string folder)
    {
        Quote New(string name,decimal grams)=>new(){Name=name,Plates=[new(){Index=1,Grams=grams,Seconds=3600}]};
        var old=New("Satoro Gojo",45);var other=New("Abelhas",114);var state=new AppData{Quotes=[old,other]};
        using var form=new MainForm(new Store(Path.Combine(folder,"catalogo")),state);
        _=form.catalog.Handle;
        var added=New("Luffy",61);state.Quotes.Insert(0,added);form.RefreshCatalog(added.Id);
        check(form.Current?.Id==added.Id&&form.title.Text=="Luffy"&&form.name.Text=="Luffy",$"Importação seleciona e exibe o projeto novo: selecionado={form.Current?.Name}, painel={form.title.Text}");
        form.catalog.SelectedIndex=2;form.searchBox.Text="Satoro";
        var next=New("Luffy - segundo",62);state.Quotes.Insert(0,next);form.searchBox.Clear();form.groupFilter.SelectedIndex=0;form.RefreshCatalog(next.Id);
        check(form.Current?.Id==next.Id&&form.title.Text==next.Name,"Importação após filtro abandona o orçamento anterior");
        form.catalog.SetSelected(1,true);form.RefreshCatalog(added.Id);
        check(form.catalog.SelectedItems.Count==1&&form.Current?.Id==added.Id&&form.title.Text==added.Name,"Atualizar seleção múltipla mostra somente o projeto solicitado");
    }
}
