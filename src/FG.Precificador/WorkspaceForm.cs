using System.Diagnostics;
using System.Globalization;

namespace FG.Precificador;

public sealed partial class MainForm
{
    readonly TextBox searchBox=new(){Dock=DockStyle.Fill,PlaceholderText="Pesquisar modelo…",AccessibleName="Pesquisar modelo"};
    readonly ComboBox groupFilter=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,DropDownWidth=600,AccessibleName="Filtrar grupo"};
    readonly ComboBox laborBasis=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly Button openProjectButton=Button("Abrir projeto"),moveGroupButton=Button("Mover para grupo…"),resliceButton=Button("Ajustar e refatiar…",true);
    readonly CheckBox autoUpdate=new();
    readonly MeshViewer viewer=new(){Dock=DockStyle.Fill};
    readonly ComboBox platePicker=new(){Width=180,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Placa da visualização 3D"};
    readonly Label meshInfo=new(){Dock=DockStyle.Bottom,Height=48,Padding=new Padding(8),ForeColor=Muted};
    CancellationTokenSource? meshCancel;Guid? meshQuote;bool filterLoading,checkingUpdates;
    sealed record GroupChoice(Guid? Id,string Text,bool Ungrouped=false){public override string ToString()=>Text;}
    sealed record PlateChoice(int? Id,string Text){public override string ToString()=>Text;}
    void BuildCatalogTools(Panel parent)
    {
        var tools=new TableLayoutPanel{Dock=DockStyle.Top,Height=168,ColumnCount=1,RowCount=4,Padding=new Padding(0,4,0,4)};
        tools.RowStyles.Add(new RowStyle(SizeType.Absolute,35));tools.RowStyles.Add(new RowStyle(SizeType.Absolute,35));tools.RowStyles.Add(new RowStyle(SizeType.Absolute,51));tools.RowStyles.Add(new RowStyle(SizeType.Absolute,37));
        tools.Controls.Add(searchBox);tools.Controls.Add(groupFilter);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};var create=Button("Novo grupo");var child=Button("Subgrupo");buttons.Controls.AddRange([create,child]);tools.Controls.Add(buttons);
        tools.Controls.Add(new Label{Text="Ctrl / Shift: selecionar vários orçamentos",Dock=DockStyle.Fill,ForeColor=Muted,Font=new Font("Segoe UI",9)});
        parent.Controls.Add(tools);tools.BringToFront();
        searchBox.TextChanged+=(_,_)=>RefreshCatalog();groupFilter.SelectedIndexChanged+=(_,_)=>{if(!filterLoading)RefreshCatalog();};
        create.Click+=(_,_)=>NewGroup(false);child.Click+=(_,_)=>NewGroup(true);
        openProjectButton.Click+=(_,_)=>OpenProject();moveGroupButton.Click+=(_,_)=>MoveToGroup();
    }
    void RefreshGroups(Guid? select=null)
    {
        filterLoading=true;groupFilter.Items.Clear();groupFilter.Items.Add(new GroupChoice(null,"Todos os grupos"));groupFilter.Items.Add(new GroupChoice(null,"Sem grupo",true));
        foreach(var g in data.Groups.OrderBy(g=>CatalogGroups.PathName(data,g.Id)))groupFilter.Items.Add(new GroupChoice(g.Id,CatalogGroups.PathName(data,g.Id)));
        groupFilter.SelectedIndex=select.HasValue?Math.Max(0,groupFilter.Items.Cast<GroupChoice>().ToList().FindIndex(x=>x.Id==select)):0;filterLoading=false;
    }
    void RefreshCatalog(Guid? selected=null)
    {
        if(filterLoading)return;var previous=selected??Current?.Id;var group=groupFilter.SelectedItem as GroupChoice;
        catalog.BeginUpdate();try{catalog.Items.Clear();foreach(var q in CatalogGroups.Filter(data,searchBox.Text,group?.Id,group?.Ungrouped??false))catalog.Items.Add(q);
            var index=catalog.Items.Cast<Quote>().ToList().FindIndex(q=>q.Id==previous);if(catalog.Items.Count>0)catalog.SelectedIndex=index>=0?index:0;else LoadCurrent();}
        finally{catalog.EndUpdate();}UpdateActions();
    }
    static string? AskName(IWin32Window owner,string caption)
    {
        using var dialog=new Form{Text=caption,Size=new Size(440,180),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false};
        var input=new TextBox{Left=18,Top=24,Width=385,MaxLength=80,AccessibleName="Nome do grupo"};var ok=new Button{Text="Criar",Left=218,Top=75,Width=90,DialogResult=DialogResult.OK};var cancel=new Button{Text="Cancelar",Left=316,Top=75,Width=90,DialogResult=DialogResult.Cancel};
        dialog.Controls.AddRange([input,ok,cancel]);dialog.AcceptButton=ok;dialog.CancelButton=cancel;return dialog.ShowDialog(owner)==DialogResult.OK?input.Text:null;
    }
    void NewGroup(bool child)
    {
        var parent=child?(groupFilter.SelectedItem as GroupChoice)?.Id:null;
        if(child&&!parent.HasValue){MessageBox.Show(this,"Selecione primeiro o grupo principal no filtro.","Criar subgrupo");return;}
        var name=AskName(this,child?"Criar subgrupo":"Criar grupo");if(name==null)return;
        try{CatalogGroups.Create(data,name,parent);Save();RefreshGroups(parent);RefreshCatalog();status.Text="Grupo criado. Selecione orçamentos e use Mover para grupo.";}
        catch(Exception ex){MessageBox.Show(this,ex.Message,"Criar grupo");}
    }
    void MoveToGroup()
    {
        var ids=catalog.SelectedItems.Cast<Quote>().Select(q=>q.Id).ToArray();if(ids.Length==0)return;
        using var dialog=new Form{Text=$"Mover {ids.Length} orçamento(s)",Size=new Size(520,200),StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false};
        var list=new ComboBox{Left=18,Top=28,Width=465,DropDownStyle=ComboBoxStyle.DropDownList};list.Items.Add(new GroupChoice(null,"Sem grupo"));
        foreach(var g in data.Groups.OrderBy(g=>CatalogGroups.PathName(data,g.Id)))list.Items.Add(new GroupChoice(g.Id,CatalogGroups.PathName(data,g.Id)));list.SelectedIndex=0;
        var ok=new Button{Text="Mover",Left=280,Top=89,Width=95,DialogResult=DialogResult.OK};var cancel=new Button{Text="Cancelar",Left=388,Top=89,Width=95,DialogResult=DialogResult.Cancel};dialog.Controls.AddRange([list,ok,cancel]);dialog.AcceptButton=ok;dialog.CancelButton=cancel;
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{var count=CatalogGroups.Move(data,ids,((GroupChoice)list.SelectedItem!).Id);Save();RefreshCatalog();status.Text=$"{count} orçamento(s) movido(s).";}
        catch(Exception ex){MessageBox.Show(this,ex.Message,"Mover orçamentos");}
    }
    void OpenProject()
    {
        if(Current is not{}q)return;
        if(File.Exists(q.Source))
        {
            tabs.SelectedIndex=4;_ = LoadMesh();status.Text="Projeto aberto na visualização 3D. Use Abrir no aplicativo associado para editar externamente.";return;
        }
        using var d=new OpenFileDialog{Title="Localizar novamente o arquivo original",Filter="Projeto ou G-code|*.3mf;*.gcode"};
        if(d.ShowDialog(this)!=DialogResult.OK)return;
        // Rebinding never claims the new file matches the quote without re-analysis.
        _=Import([d.FileName]);status.Text="Arquivo localizado: será importado como novo orçamento para conferir os dados.";
    }
    void OpenExternal()
    {
        if(Current is not{}q)return;
        try{if(!File.Exists(q.Source))throw new FileNotFoundException("Arquivo não encontrado. Use Abrir projeto para localizá-lo.");Process.Start(new ProcessStartInfo(q.Source){UseShellExecute=true});}
        catch(Exception ex){MessageBox.Show(this,"Não foi possível abrir no aplicativo associado. Você pode usar a visualização 3D do precificador.\r\n\r\n"+ex.Message,"Abrir arquivo");}
    }
    TabPage Build3DTab()
    {
        var tab=new TabPage("Projeto 3D") {BackColor=Color.White,Padding=new Padding(12)};var tools=new FlowLayoutPanel{Dock=DockStyle.Top,Height=104,WrapContents=true};
        var reset=Button("Centralizar");var external=Button("Abrir no aplicativo associado");tools.Controls.AddRange([platePicker,reset,resliceButton,external]);
        reset.Click+=(_,_)=>viewer.ResetView();external.Click+=(_,_)=>OpenExternal();resliceButton.Click+=async(_,_)=>await AdjustAndSlice();platePicker.SelectedIndexChanged+=(_,_)=>{if(!loading&&tabs.SelectedTab==tab)_=LoadMesh();};
        tab.Controls.Add(viewer);tab.Controls.Add(meshInfo);tab.Controls.Add(tools);tabs.SelectedIndexChanged+=(_,_)=>{if(tabs.SelectedTab==tab)_=LoadMesh();};return tab;
    }
    void RefreshPlatePicker(Quote q)
    {
        if(meshQuote==q.Id)return;meshQuote=q.Id;platePicker.Items.Clear();platePicker.Items.Add(new PlateChoice(null,"Todas as placas"));foreach(var p in q.Plates)platePicker.Items.Add(new PlateChoice(p.Index,"Placa "+p.Index));platePicker.SelectedIndex=0;
        viewer.SetMesh(null);if(tabs.SelectedIndex==4)BeginInvoke(()=>_ = LoadMesh());
    }
    async Task LoadMesh()
    {
        meshCancel?.Cancel();var sourceCancel=new CancellationTokenSource();meshCancel=sourceCancel;var token=sourceCancel.Token;var q=Current;
        viewer.SetMesh(null,"Carregando a malha 3D…");meshInfo.Text="";if(q==null){viewer.SetMesh(null);return;}
        var plate=(platePicker.SelectedItem as PlateChoice)?.Id;
        try{await Project3D.VerifySource(q,token);var mesh=await Task.Run(()=>Project3D.Load(q.Source,plate,token),token);if(!token.IsCancellationRequested&&Current?.Id==q.Id){viewer.SetMesh(mesh);meshInfo.Text=mesh.Message;}}
        catch(OperationCanceledException){}
        catch(Exception ex){if(!token.IsCancellationRequested){viewer.SetMesh(null,ex.Message);meshInfo.Text="O orçamento continua disponível mesmo sem a visualização.";}}
        finally{if(meshCancel==sourceCancel)meshCancel=null;sourceCancel.Dispose();}
    }
    void RefreshPlatePrices(Quote q)
    {
        plates.Rows.Clear();foreach(var p in Pricing.ByPlate(q))plates.Rows.Add(PlateImage(p.Plate.Thumbnail)!,p.Plate.Index,p.Plate.Grams.ToString("N2"),Pricing.Duration(p.Plate.Seconds),p.Material.ToString("N2"),p.Energy.ToString("N2"),p.Machine.ToString("N2"),p.Time.ToString("N2"),p.Other.ToString("N2"),p.Cost.ToString("N2"),p.Labor.ToString("N2"),p.Minimum.ToString("N2"));
        var r=Pricing.Calculate(q);plates.Rows.Add(null!,"TOTAL",q.Grams.ToString("N2"),Pricing.Duration(q.Seconds),"","","","","",(r.PrintCost*q.UnitsInFile).ToString("N2"),(r.Labor*q.UnitsInFile).ToString("N2"),(r.Minimum*q.UnitsInFile).ToString("N2"));
        plates.Rows[^1].DefaultCellStyle.BackColor=Color.FromArgb(221,239,235);
        plates.AccessibleDescription="Custos do arquivo inteiro por placa. O mínimo rateado soma o mínimo por unidade multiplicado pelas unidades do arquivo; não é um preço independente de venda de cada placa. Mão de obra por projeto é rateada pelo tempo, extras e outros pelo material.";
    }
    Image? PlateImage(string path)=>ListThumbnail(new Quote{Thumbnail=path});
    async Task AdjustAndSlice()
    {
        if(Current is not{}q||importCancel!=null)return;
        try
        {
            await Project3D.VerifySource(q);var settings=PrintAdjustments.Read(q.Source);using var dialog=new Form{Text="Ajustar impressão e salvar uma cópia",Size=new Size(650,610),StartPosition=FormStartPosition.CenterParent,Font=Font,MinimizeBox=false,MaximizeBox=false};
            var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(20)};dialog.Controls.Add(panel);
            var note=TextLabel("Marque apenas os parâmetros que deseja alterar. Eles serão aplicados a todas as placas e às sobreposições dos objetos. A geometria, orientação, cores e os demais parâmetros serão mantidos. O original será preservado.");note.MaximumSize=new Size(575,0);panel.Controls.Add(note);
            var rows=new Dictionary<string,(CheckBox apply,Control value)>();
            void Add(string key,string label,Control control){var row=new FlowLayoutPanel{Width=570,Height=45};var check=new CheckBox{Text=label,Width=315,AutoSize=false,Height=32};control.Width=210;control.Enabled=false;check.CheckedChanged+=(_,_)=>control.Enabled=check.Checked;row.Controls.AddRange([check,control]);panel.Controls.Add(row);rows[key]=(check,control);}
            NumericUpDown Num(string key,decimal min,decimal max,int digits,decimal fallback){var n=Numeric(min,max,digits,fallback);n.Dock=DockStyle.None;Set(n,decimal.TryParse(settings.GetValueOrDefault(key)?.TrimEnd('%'),NumberStyles.Number,CultureInfo.InvariantCulture,out var v)?v:fallback);return n;}
            Add("layer_height","Altura de camada (mm)",Num("layer_height",.04m,.8m,2,.2m));Add("sparse_infill_density","Preenchimento (%)",Num("sparse_infill_density",0,100,0,15));Add("wall_loops","Número de paredes",Num("wall_loops",1,20,0,2));
            var support=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};support.Items.AddRange(["Desativado","Ativado"]);support.SelectedIndex=settings["enable_support"]=="1"?1:0;Add("enable_support","Suporte (tipo atual do projeto)",support);
            var brim=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};brim.Items.AddRange(["Sem brim","Externo","Automático"]);brim.SelectedIndex=Array.IndexOf(new[]{"no_brim","outer_only","auto_brim"},settings["brim_type"]);if(brim.SelectedIndex<0)brim.SelectedIndex=0;Add("brim_type","Adesão à mesa (brim)",brim);Add("brim_width","Largura do brim (mm)",Num("brim_width",0,30,1,5));
            var actions=new FlowLayoutPanel{Width=570,Height=60};var ok=Button("Salvar cópia e recalcular",true);ok.DialogResult=DialogResult.OK;var cancel=Button("Cancelar");cancel.DialogResult=DialogResult.Cancel;actions.Controls.AddRange([ok,cancel]);panel.Controls.Add(actions);dialog.AcceptButton=ok;dialog.CancelButton=cancel;
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            var changes=new Dictionary<string,string>();foreach(var (key,row) in rows.Where(x=>x.Value.apply.Checked))changes[key]=row.value switch{NumericUpDown n=>n.Value.ToString(CultureInfo.InvariantCulture)+(key=="sparse_infill_density"?"%":""),ComboBox c when key=="enable_support"=>c.SelectedIndex==1?"1":"0",ComboBox c=>new[]{"no_brim","outer_only","auto_brim"}[c.SelectedIndex],_=>""};
            if(changes.Count==0){MessageBox.Show(this,"Marque pelo menos um parâmetro para alterar.","Ajustar impressão");return;}
            using var file=new SaveFileDialog{Filter="Projeto 3MF|*.3mf",FileName=Path.GetFileNameWithoutExtension(q.Source)+" - Ajustado.3mf",OverwritePrompt=true};if(file.ShowDialog(this)!=DialogResult.OK)return;
            importCancel=new CancellationTokenSource();var token=importCancel.Token;UpdateActions();importButton.Enabled=manualButton.Enabled=false;cancelButton.Enabled=true;
            try
            {
                var progress=new Progress<string>(s=>status.Text=s);
                var imported=await Task.Run(async()=>{PrintAdjustments.CreateCopy(q.Source,file.FileName,changes);return await Slicer.ImportAsync(file.FileName,data.BambuPath,store,progress,token);},token);
                imported.Name=q.Name+" - Ajustado";imported.Settings=q.Settings.Copy();imported.GroupId=q.GroupId;imported.UnitsInFile=q.UnitsInFile;imported.OrderQuantity=q.OrderQuantity;imported.ExtraGrams=q.ExtraGrams;imported.OtherPerUnit=q.OtherPerUnit;imported.Shopee=q.Shopee;imported.ShopeePercent=q.ShopeePercent;imported.ShopeeFixed=q.ShopeeFixed;
                imported.Notes+="\r\nParâmetros alterados nesta cópia: "+string.Join("; ",changes.Select(x=>x.Key+"="+x.Value));data.Quotes.Insert(0,imported);Save();searchBox.Clear();groupFilter.SelectedIndex=0;RefreshCatalog(imported.Id);status.Text="Cópia refatiada e adicionada como novo orçamento. O original foi preservado.";
            }
            finally{importCancel.Dispose();importCancel=null;UpdateActions();importButton.Enabled=manualButton.Enabled=true;cancelButton.Enabled=false;}
        }
        catch(OperationCanceledException){status.Text="Análise cancelada. O original foi preservado; a cópia de configurações continua no local escolhido.";}
        catch(Exception ex){MessageBox.Show(this,ex.Message,"Ajustar impressão");}
    }
    async Task CheckForUpdates(bool explicitCheck)
    {
        if(checkingUpdates)return;checkingUpdates=true;
        try
        {
            var release=await UpdateChecker.Check();if(IsDisposed)return;
            if(release==null){if(explicitCheck)MessageBox.Show(this,"Você já está usando a versão mais recente disponível.","Atualizações");return;}
            if(MessageBox.Show(this,$"A versão {release.Version} está disponível. Você usa {UpdateChecker.Current.ToString(3)}.\r\n\r\nAbrir a página oficial de download?","Atualização disponível",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)Process.Start(new ProcessStartInfo(release.Url){UseShellExecute=true});
        }
        catch(Exception ex){if(explicitCheck&&!IsDisposed)MessageBox.Show(this,"Não foi possível consultar as atualizações agora.\r\n"+ex.Message,"Atualizações");}
        finally{checkingUpdates=false;}
    }
}
