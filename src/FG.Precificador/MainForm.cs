using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FG.Precificador;

public sealed partial class MainForm : Form
{
    readonly Store store;
    readonly AppData data;
    readonly ListBox catalog = new() { Dock=DockStyle.Fill, BorderStyle=BorderStyle.None, IntegralHeight=false, DrawMode=DrawMode.OwnerDrawFixed, ItemHeight=88,SelectionMode=SelectionMode.MultiExtended,AccessibleName="Seus orçamentos com miniaturas" };
    readonly Dictionary<string,Image?> thumbnailCache=[];
    readonly Button editButton=Button("Editar"), deleteButton=Button("Excluir"), undoButton=Button("Desfazer exclusão");
    readonly TabControl tabs=new() {Dock=DockStyle.Fill,Padding=new Point(18,8)};
    readonly Dictionary<string,TextBox> companyFields=[];
    readonly PictureBox companyPicture=new(){SizeMode=PictureBoxSizeMode.Zoom,Width=260,Height=210,BackColor=Color.FromArgb(245,247,250)};
    readonly NumericUpDown validity=Numeric(0,3650,0);
    string companyLogo="";
    readonly Label status = new() { Dock=DockStyle.Bottom, Height=34, Padding=new Padding(18,7,8,0), Text="Pronto para importar. Seus arquivos são analisados neste computador." };
    readonly Label title = new() { AutoSize=true, Font=new Font("Segoe UI Semibold",18), ForeColor=Navy };
    readonly Label stats = new() { AutoSize=true, MaximumSize=new Size(780,0), ForeColor=Muted };
    readonly Label method = new() { AutoSize=true, MaximumSize=new Size(780,0), ForeColor=Muted };
    readonly PictureBox picture = new() { SizeMode=PictureBoxSizeMode.Zoom, Dock=DockStyle.Fill, BackColor=Color.FromArgb(246,248,251) };
    readonly Label costValue=ValueLabel(), priceValue=ValueLabel(), orderValue=ValueLabel();
    readonly Label minimum = new() { AutoSize=true, ForeColor=Muted };
    readonly DataGridView breakdown = Grid();
    readonly DataGridView plates = Grid();
    readonly TextBox name = new() { Dock=DockStyle.Fill };
    readonly NumericUpDown units=Numeric(1,100000,0,1), quantity=Numeric(1,100000,0,1), extra=Numeric(), other=Numeric();
    readonly NumericUpDown percent=Numeric(0,99.99m,2,20), fixedFee=Numeric(2,5.5m,2,2);
    readonly CheckBox shopee=new() { Text="Incluir taxa Shopee neste orçamento", AutoSize=true };
    readonly NumericUpDown grams=Numeric(), minutes=Numeric();
    readonly Dictionary<string,NumericUpDown> costs=[];
    readonly TextBox bambuPath=new() { Dock=DockStyle.Fill, ReadOnly=true };
    readonly TextBox details=new() { Dock=DockStyle.Fill, Multiline=true, ScrollBars=ScrollBars.Vertical, ReadOnly=true, BackColor=Color.White, BorderStyle=BorderStyle.None };
    readonly Button importButton=Button("Importar arquivo",true), manualButton=Button("Entrada manual"), cancelButton=Button("Cancelar análise");
    readonly System.Windows.Forms.Timer saveTimer=new() { Interval=650 };
    CancellationTokenSource? importCancel;
    bool loading;
    Quote? Current => catalog.SelectedItem as Quote;
    static readonly Color Navy=Color.FromArgb(18,39,62), Teal=Color.FromArgb(0,111,104), Muted=Color.FromArgb(82,99,114), Canvas=Color.FromArgb(242,245,248);

    public MainForm(Store store,AppData data)
    {
        this.store=store;this.data=data;
        Text="FG Precificador 3D · "+typeof(MainForm).Assembly.GetName().Version?.ToString(3);Font=new Font("Segoe UI",10);BackColor=Canvas;ForeColor=Navy;
        Size=new Size(1380,920);MinimumSize=new Size(1200,800);StartPosition=FormStartPosition.CenterScreen;
        Icon=Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        AutoScaleMode=AutoScaleMode.Dpi;AllowDrop=true;
        Build();Wire();
        RefreshGroups();RefreshCatalog();
        if(catalog.Items.Count>0)catalog.SelectedIndex=0;else LoadCurrent();
        UpdateActions();
        cancelButton.Enabled=false;
        Shown+=async(_,_)=>{ if(Program.StartFiles.Length>0)await Import(Program.StartFiles);if(data.CheckUpdatesOnStartup)await CheckForUpdates(false); };
        FormClosing+=(_,e)=>{if(importCancel!=null){importCancel.Cancel();e.Cancel=true;status.Text="Cancelando a análise. Feche novamente após a conclusão.";}else {saveTimer.Stop();Save();}};
        FormClosed+=(_,_)=>{meshCancel?.Cancel();foreach(var image in thumbnailCache.Values)image?.Dispose();picture.Image?.Dispose();companyPicture.Image?.Dispose();};
    }
    static Label ValueLabel()=>new(){AutoSize=true,Font=new Font("Segoe UI Semibold",23),ForeColor=Navy,Margin=new Padding(0,8,0,0)};
    static NumericUpDown Numeric(decimal min=0,decimal max=10000000,int decimals=2,decimal value=0)=>new(){Minimum=min,Maximum=max,DecimalPlaces=decimals,Value=value,ThousandsSeparator=true,Dock=DockStyle.Fill,Increment=decimals==0?1:.1m};
    static Button Button(string text,bool primary=false)
    {
        var b=new Button { Text=text,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(12,6,12,6),FlatStyle=FlatStyle.Flat,BackColor=primary?Teal:Color.White,ForeColor=primary?Color.White:Navy,Margin=new Padding(0,0,8,0) };
        b.FlatAppearance.BorderColor=primary?Teal:Color.FromArgb(203,214,223);return b;
    }
    static DataGridView Grid()=>new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal,GridColor=Color.FromArgb(229,235,240),ColumnHeadersHeight=34,RowTemplate={Height=31},EnableHeadersVisualStyles=false,ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle {BackColor=Canvas,ForeColor=Navy,Font=new Font("Segoe UI Semibold",10)},DefaultCellStyle=new DataGridViewCellStyle{SelectionBackColor=Color.FromArgb(223,240,237),SelectionForeColor=Navy,Padding=new Padding(7,0,7,0)}};
    static Label TextLabel(string text,bool heading=false)=>new(){Text=text,AutoSize=true,MaximumSize=new Size(700,0),Font=new Font(heading?"Segoe UI Semibold":"Segoe UI",heading?12:10),Margin=new Padding(0,0,0,10)};
    static TableLayoutPanel FormTable()=>new(){Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(0,4,0,0),ColumnStyles={new ColumnStyle(SizeType.Percent,64),new ColumnStyle(SizeType.Percent,36)}};
    static void Field(TableLayoutPanel t,string text,Control c)
    {
        int row=t.RowCount++;t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(new Label {Text=text,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(0,8,10,8)},0,row);
        c.Margin=new Padding(0,5,0,5);c.AccessibleName=text;t.Controls.Add(c,1,row);
    }
    static Panel Card(string text,Label value)
    {
        var panel=new Panel {Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(15),Margin=new Padding(0,0,12,0)};
        var stack=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        stack.Controls.Add(new Label{Text=text,AutoSize=true,ForeColor=Muted});stack.Controls.Add(value);panel.Controls.Add(stack);return panel;
    }
    void Build()
    {
        var top=new Panel {Dock=DockStyle.Top,Height=132,BackColor=Color.White,Padding=new Padding(22,8,22,8)};
        var logo=new PictureBox {Location=new Point(22,4),Size=new Size(225,124),SizeMode=PictureBoxSizeMode.Zoom,AccessibleName="FG Design Print · @fgdesigneprint"};
        using(var stream=typeof(MainForm).Assembly.GetManifestResourceStream("FG.Marca"))
        {if(stream!=null){using var image=Image.FromStream(stream);logo.Image=new Bitmap(image);}}
        top.Controls.Add(logo);
        var brand=new Label {Text="Precificador 3D",Font=new Font("Segoe UI Semibold",22),ForeColor=Navy,AutoSize=true,Location=new Point(275,24)};
        top.Controls.Add(brand);top.Controls.Add(new Label {Text="Do arquivo configurado ao preço de venda",ForeColor=Muted,AutoSize=true,Location=new Point(278,68)});
        var instagram=new LinkLabel {Text="Instagram: @fgdesigneprint",LinkColor=Teal,ActiveLinkColor=Navy,AutoSize=true,Location=new Point(278,93)};
        instagram.LinkClicked+=(_,_)=>Process.Start(new ProcessStartInfo("https://www.instagram.com/fgdesigneprint/"){UseShellExecute=true});top.Controls.Add(instagram);
        var toolbar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=62,Padding=new Padding(18,10,12,6),WrapContents=false};
        toolbar.Controls.AddRange([importButton,manualButton,cancelButton]);
        var export=Button("Exportar orçamento");var backup=Button("Backup dos dados");toolbar.Controls.Add(export);toolbar.Controls.Add(backup);
        var shortcut=Button("Atalho na área de trabalho");toolbar.Controls.Add(shortcut);
        shortcut.Click+=(_,_)=>{try{var path=DesktopShortcut.Create();status.Text="Atalho criado: "+path;}catch(Exception ex){MessageBox.Show(this,ex.Message,"Criar atalho");}};
        export.Click+=(_,_)=>Export();backup.Click+=(_,_)=>Backup();
        var split=new SplitContainer {Size=new Size(1320,650),Dock=DockStyle.Fill,FixedPanel=FixedPanel.Panel1,SplitterWidth=10,BackColor=Canvas,SplitterDistance=340,Panel1MinSize=310,Panel2MinSize=780};
        split.Panel1.BackColor=Color.White;split.Panel1.Padding=new Padding(12);
        split.Panel1.Controls.Add(catalog);split.Panel1.Controls.Add(new Label {Text="SEUS ORÇAMENTOS",Dock=DockStyle.Top,Height=35,Font=new Font("Segoe UI Semibold",10),Padding=new Padding(4,6,0,0)});
        BuildCatalogTools(split.Panel1);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=142,Padding=new Padding(0,8,0,0),WrapContents=true};
        actions.Controls.AddRange([openProjectButton,moveGroupButton,editButton,deleteButton,undoButton]);split.Panel1.Controls.Add(actions);
        var right=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Padding=new Padding(6,0,18,10),RowStyles={new RowStyle(SizeType.Absolute,120),new RowStyle(SizeType.Percent,100)}};
        var header=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,ColumnStyles={new ColumnStyle(SizeType.Absolute,128),new ColumnStyle(SizeType.Percent,100)},Margin=new Padding(0,0,0,12)};
        header.Controls.Add(picture,0,0);
        var labels=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(18,6,0,0)};
        labels.Controls.AddRange([title,stats,method]);header.Controls.Add(labels,1,0);right.Controls.Add(header,0,0);
        tabs.TabPages.Add(QuoteTab());tabs.TabPages.Add(CostsTab());tabs.TabPages.Add(CompanyTab());tabs.TabPages.Add(SourceTab());tabs.TabPages.Add(Build3DTab());right.Controls.Add(tabs,0,1);split.Panel2.Controls.Add(right);
        Controls.Add(split);Controls.Add(toolbar);Controls.Add(top);Controls.Add(status);
    }
    TabPage QuoteTab()
    {
        var tab=new TabPage("Orçamento") {BackColor=Canvas,Padding=new Padding(14)};
        var body=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,RowStyles={new RowStyle(SizeType.Absolute,108),new RowStyle(SizeType.Absolute,32),new RowStyle(SizeType.Percent,100)}};
        var cards=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,ColumnStyles={new ColumnStyle(SizeType.Percent,33.33f),new ColumnStyle(SizeType.Percent,33.33f),new ColumnStyle(SizeType.Percent,33.34f)}};
        cards.Controls.Add(Card("Custo por unidade",costValue),0,0);cards.Controls.Add(Card("Sugestão por unidade",priceValue),1,0);cards.Controls.Add(Card("Total do pedido",orderValue),2,0);body.Controls.Add(cards,0,0);
        body.Controls.Add(minimum,0,1);
        var lower=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,ColumnStyles={new ColumnStyle(SizeType.Percent,47),new ColumnStyle(SizeType.Percent,53)}};
        var sale=new Panel {Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(0,0,18,0)};
        var table=FormTable();Field(table,"Nome do produto",name);Field(table,"Unidades já contidas no arquivo",units);Field(table,"Quantidade do pedido",quantity);
        Field(table,"Filamento extra do arquivo (g)",extra);Field(table,"Outros custos / unidade (R$)",other);
        Field(table,"Material total do arquivo (g)",grams);Field(table,"Tempo total do arquivo (min)",minutes);
        var flags=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0,10,0,0)};
        flags.Controls.Add(shopee);
        var fees=FormTable();fees.Width=340;Field(fees,"Shopee: percentual (%)",percent);Field(fees,"Shopee: taxa fixa / item (R$)",fixedFee);flags.Controls.Add(fees);
        var help=TextLabel("O consumo é o total do arquivo. Só divida pelas unidades que já estão nele. A quantidade do pedido multiplica o preço de venda; não simula um novo lote no fatiador.");help.MaximumSize=new Size(370,0);help.ForeColor=Muted;flags.Controls.Add(help);
        var stack=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=1};stack.Controls.Add(table);stack.Controls.Add(flags);sale.Controls.Add(stack);
        breakdown.Columns.Add("part","Composição por unidade");breakdown.Columns.Add("value","Valor");breakdown.Columns[1].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;breakdown.Columns[0].FillWeight=73;breakdown.Columns[1].FillWeight=27;
        lower.Controls.Add(sale,0,0);lower.Controls.Add(breakdown,1,0);body.Controls.Add(lower,0,2);tab.Controls.Add(body);return tab;
    }
    TabPage CostsTab()
    {
        var tab=new TabPage("Custos e padrões") {BackColor=Color.White,Padding=new Padding(18),AutoScroll=true};
        var layout=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,ColumnStyles={new ColumnStyle(SizeType.Percent,55),new ColumnStyle(SizeType.Percent,45)}};
        var fields=FormTable();
        foreach(var x in new[]{("kg","Filamento (R$/kg)"),("energy","Energia (R$/kWh)"),("watts","Potência média estimada (W)"),("machine","Máquina: reserva por hora (R$)"),("time","Tempo: cobrança extra por hora (R$)"),("labor","Mão de obra (R$)"),("markup","Lucro sobre custo de impressão (%)")})
        {var n=Numeric();costs[x.Item1]=n;Field(fields,x.Item2,n);}
        laborBasis.Items.AddRange(Enum.GetValues<LaborBasis>().Select(x=>(object)Pricing.LaborLabel(x)).ToArray());Field(fields,"Cobrar mão de obra",laborBasis);laborBasis.SelectedIndexChanged+=(_,_)=>Edited();
        var left=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Padding=new Padding(0,0,22,0)};left.Controls.Add(fields);
        var defaults=Button("Usar estes custos nos próximos arquivos",true);defaults.Margin=new Padding(0,15,0,15);defaults.Click+=(_,_)=>{if(Current==null)return;data.Defaults=Current.Settings.Copy();Save();status.Text="Padrões salvos. Os próximos arquivos usarão estes custos; os orçamentos anteriores mantêm seus valores.";};left.Controls.Add(defaults);
        left.Controls.Add(TextLabel("Motor de fatiamento",true));left.Controls.Add(bambuPath);bambuPath.Text=SlicingEngine.Description(data.BambuPath);
        var browse=Button(SlicingEngine.Available?"Motor, código e licenças":"Localizar motor externo…");browse.Margin=new Padding(0,10,0,10);
        browse.Click+=(_,_)=>{if(SlicingEngine.Available)ShowEngineInfo();else BrowseBambu();};left.Controls.Add(browse);
        var updates=Button("Verificar atualizações");updates.Margin=new Padding(0,8,0,8);updates.Click+=async(_,_)=>await CheckForUpdates(true);left.Controls.Add(updates);
        autoUpdate.Text="Avisar sobre atualizações ao abrir";autoUpdate.AutoSize=true;autoUpdate.Checked=data.CheckUpdatesOnStartup;autoUpdate.CheckedChanged+=(_,_)=>{data.CheckUpdatesOnStartup=autoUpdate.Checked;Save();};left.Controls.Add(autoUpdate);
        layout.Controls.Add(left,0,0);
        var notes=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(8)};
        foreach(var text in new[]{"Preço direto = custo × (1 + lucro %) + mão de obra.","O padrão é 100% de lucro sobre os custos de impressão. Os R$ 20 de mão de obra entram depois.","R$ 1,20/kWh e 120 W são estimativas iniciais para Uberlândia e A1 Combo. Ajuste com a conta de luz e uma medição na tomada.","R$ 1/h é uma reserva inicial de máquina para depreciação e manutenção. O tempo total do fatiador determina essa cobrança.","A cobrança extra por tempo começa em zero. Use-a se quiser cobrar também pela ocupação da máquina.","A taxa Shopee começa desligada em todo novo orçamento. Quando ligada: (preço direto + taxa fixa) ÷ (1 − percentual).","Cada orçamento conserva os próprios custos. O botão ao lado define o padrão dos próximos arquivos."})
        {var l=TextLabel(text);l.MaximumSize=new Size(350,0);notes.Controls.Add(l);}
        layout.Controls.Add(notes,1,0);tab.Controls.Add(layout);return tab;
    }
    void ShowEngineInfo()
    {
        try
        {
            using var dialog=new Form{Text="Motor integrado, código e licenças",Size=new Size(820,620),StartPosition=FormStartPosition.CenterParent,Font=Font,MinimizeBox=false,MaximizeBox=false};
            dialog.Controls.Add(new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.White,Text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Engine","MOTOR-E-FONTES.md")).Replace("\r\n","\n").Replace("\n",Environment.NewLine)});
            dialog.ShowDialog(this);
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message,"Informações do motor");}
    }
    TabPage SourceTab()
    {
        var tab=new TabPage("Placas e custos") {BackColor=Color.White,Padding=new Padding(16)};
        var body=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,RowStyles={new RowStyle(SizeType.Percent,45),new RowStyle(SizeType.Percent,55)}};
        plates.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.AllCells;plates.RowTemplate.Height=72;plates.ColumnHeadersHeight=48;
        plates.Columns.Add(new DataGridViewImageColumn{Name="preview",HeaderText="Placa",ImageLayout=DataGridViewImageCellLayout.Zoom,Width=82,AutoSizeMode=DataGridViewAutoSizeColumnMode.None,DefaultCellStyle=new DataGridViewCellStyle{NullValue=null}});
        foreach(var c in new[]{("index","Nº"),("grams","Filamento (g)"),("time","Tempo"),("material","Material R$"),("energy","Energia R$"),("machine","Máquina R$"),("extraTime","Tempo extra R$"),("other","Outros R$"),("cost","Custo R$"),("labor","Mão de obra R$"),("minimum","Mínimo rateado R$")})plates.Columns.Add(c.Item1,c.Item2);
        body.RowStyles[0].Height=60;body.RowStyles[1].Height=40;body.Controls.Add(plates,0,0);body.Controls.Add(details,0,1);tab.Controls.Add(body);return tab;
    }
    void Wire()
    {
        catalog.DrawItem+=(_,e)=>
        {
            if(e.Index<0)return;var q=(Quote)catalog.Items[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;
            using var bg=new SolidBrush(selected?Color.FromArgb(221,239,235):Color.White);e.Graphics.FillRectangle(bg,e.Bounds);
            var thumb=new Rectangle(e.Bounds.X+8,e.Bounds.Y+8,70,70);
            e.Graphics.FillRectangle(Brushes.White,thumb);
            var image=ListThumbnail(q);
            if(image!=null)e.Graphics.DrawImage(image,thumb);
            else TextRenderer.DrawText(e.Graphics,"3D",Font,thumb,Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            using var itemFont=new Font("Segoe UI Semibold",10);
            TextRenderer.DrawText(e.Graphics,q.Name,itemFont,new Rectangle(e.Bounds.X+88,e.Bounds.Y+15,e.Bounds.Width-96,24),Navy,TextFormatFlags.EndEllipsis);
            string sub;try{sub=Pricing.Money(Pricing.Calculate(q).Suggested)+"  ·  "+q.Grams.ToString("N2")+" g";}catch{sub="Informe material e tempo";}
            TextRenderer.DrawText(e.Graphics,sub,Font,new Rectangle(e.Bounds.X+88,e.Bounds.Y+43,e.Bounds.Width-96,22),Muted,TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();
        };
        catalog.SelectedIndexChanged+=(_,_)=>LoadCurrent();
        var menu=new ContextMenuStrip();
        menu.Items.Add("Editar orçamento",null,(_,_)=>EditCurrent());
        menu.Items.Add("Abrir projeto",null,(_,_)=>OpenProject());menu.Items.Add("Mover selecionados para grupo…",null,(_,_)=>MoveToGroup());
        menu.Items.Add("Excluir orçamento…",null,(_,_)=>DeleteCurrent());
        menu.Opening+=(_,e)=>{if(Current==null||importCancel!=null)e.Cancel=true;};
        catalog.ContextMenuStrip=menu;
        catalog.MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Right){var i=catalog.IndexFromPoint(e.Location);if(i>=0&&!catalog.GetSelected(i)){catalog.ClearSelected();catalog.SetSelected(i,true);}}};
        catalog.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.F2){EditCurrent();e.Handled=true;}else if(e.KeyCode==Keys.Delete){DeleteCurrent();e.Handled=true;}};
        catalog.DoubleClick+=(_,_)=>EditCurrent();
        editButton.Click+=(_,_)=>EditCurrent();deleteButton.Click+=(_,_)=>DeleteCurrent();undoButton.Click+=(_,_)=>UndoDelete();
        importButton.Click+=async(_,_)=>{using var d=new OpenFileDialog{Filter="Projetos e fatiamentos|*.3mf;*.gcode|Projeto 3MF|*.3mf|G-code|*.gcode",Multiselect=true,Title="Importar para precificar"};if(d.ShowDialog(this)==DialogResult.OK)await Import(d.FileNames);};
        cancelButton.Click+=(_,_)=>importCancel?.Cancel();
        manualButton.Click+=(_,_)=>{var q=new Quote {Manual=true,Settings=data.Defaults.Copy(),Plates=[new Plate{Index=1}]};data.Quotes.Insert(0,q);searchBox.Clear();groupFilter.SelectedIndex=0;RefreshCatalog(q.Id);Save();};
        foreach(var n in new[]{units,quantity,extra,other,grams,minutes,percent,fixedFee}.Concat(costs.Values))n.ValueChanged+=(_,_)=>Edited();
        name.TextChanged+=(_,_)=>Edited();shopee.CheckedChanged+=(_,_)=>Edited();
        saveTimer.Tick+=(_,_)=>{saveTimer.Stop();Save();};
        DragEnter+=(_,e)=>e.Effect=e.Data?.GetDataPresent(DataFormats.FileDrop)==true&&importCancel==null?DragDropEffects.Copy:DragDropEffects.None;
        DragDrop+=async(_,e)=>{if(e.Data?.GetData(DataFormats.FileDrop) is string[] paths)await Import(paths);};
    }
    void Set(NumericUpDown n,decimal v)=>n.Value=Math.Clamp(v,n.Minimum,n.Maximum);
    void LoadCurrent()
    {
        UpdateActions();
        if(Current is not{}q)
        {
            meshCancel?.Cancel();meshQuote=null;viewer.SetMesh(null,"Selecione um projeto para visualizar em 3D.");platePicker.Items.Clear();meshInfo.Text="";
            loading=true;
            try{name.Text="";title.Text="Selecione ou importe um projeto";stats.Text="";method.Text="";costValue.Text=priceValue.Text=orderValue.Text="—";minimum.Text="Escolha um orçamento na lista.";breakdown.Rows.Clear();plates.Rows.Clear();details.Clear();picture.Image?.Dispose();picture.Image=null;}
            finally{loading=false;}
            return;
        }
        loading=true;
        try
        {
            name.Text=q.Name;Set(units,q.UnitsInFile);Set(quantity,q.OrderQuantity);Set(extra,q.ExtraGrams);Set(other,q.OtherPerUnit);Set(grams,q.Grams);Set(minutes,q.Seconds/60);laborBasis.SelectedIndex=(int)q.Settings.LaborBasis;
            grams.ReadOnly=minutes.ReadOnly=!q.Manual;grams.Enabled=minutes.Enabled=q.Manual;
            shopee.Checked=q.Shopee;Set(percent,q.ShopeePercent);Set(fixedFee,q.ShopeeFixed);
            var s=q.Settings;Set(costs["kg"],s.FilamentPerKg);Set(costs["energy"],s.EnergyPerKwh);Set(costs["watts"],s.Watts);Set(costs["machine"],s.MachinePerHour);Set(costs["time"],s.TimePerHour);Set(costs["labor"],s.Labor);Set(costs["markup"],s.MarkupPercent);
            picture.Image?.Dispose();picture.Image=null;
            try{var path=q.Thumbnail;if(File.Exists(path)){using var im=Image.FromFile(path);picture.Image=new Bitmap(im);}}catch{}
            RefreshPlatePicker(q);
            details.Text=$"{q.Method}\r\n\r\nArquivo: {q.Source}\r\nPerfil: {q.Profile}\r\nSHA-256: {q.Sha256}\r\nImportado: {q.Created:dd/MM/yyyy HH:mm}\r\n\r\n{q.Notes}\r\n{string.Join("\r\n",q.Plates.Where(x=>!string.IsNullOrWhiteSpace(x.Warning)).Select(x=>$"Mesa {x.Index}: {x.Warning}"))}\r\n\r\nO tempo e o consumo são estimativas do fatiador. Purga inicial/calibração e falhas podem exigir filamento extra. Outros custos por unidade podem incluir embalagem, acessórios e frete.\r\n\r\nPesquisa inicial: 17/11/2026. Cemig B1 convencional: R$ 0,90329/kWh antes dos impostos, com bandeira verde. R$ 1,20/kWh e 120 W são hipóteses de orçamento, não medições da conta ou máquina. Reserva da máquina: R$ 1/h estimado.\r\nhttps://www.cemig.com.br/valores-e-tarifas/tarifas-vigentes/\r\nhttps://forum.bambulab.com/t/power-consumption-for-a1-printer/92095/14\r\n\r\nAs taxas Shopee são parâmetros fornecidos pelo usuário, não uma tabela oficial atualizada.\r\n\r\nDados locais: {store.DataPath}";
        }finally{loading=false;}RefreshPrice();
    }
    void Edited()
    {
        if(loading||Current is not{}q)return;
        q.Name=string.IsNullOrWhiteSpace(name.Text)?"Sem nome":name.Text.Trim();q.UnitsInFile=(int)units.Value;q.OrderQuantity=(int)quantity.Value;q.ExtraGrams=extra.Value;q.OtherPerUnit=other.Value;
        q.Shopee=shopee.Checked;q.ShopeePercent=percent.Value;q.ShopeeFixed=fixedFee.Value;
        if(q.Manual)q.Plates=[new Plate{Index=1,Grams=grams.Value,Seconds=minutes.Value*60}];
        var s=q.Settings;s.FilamentPerKg=costs["kg"].Value;s.EnergyPerKwh=costs["energy"].Value;s.Watts=costs["watts"].Value;s.MachinePerHour=costs["machine"].Value;s.TimePerHour=costs["time"].Value;s.Labor=costs["labor"].Value;s.MarkupPercent=costs["markup"].Value;if(laborBasis.SelectedIndex>=0)s.LaborBasis=(LaborBasis)laborBasis.SelectedIndex;
        RefreshPrice();catalog.Invalidate();saveTimer.Stop();saveTimer.Start();
    }
    void RefreshPrice()
    {
        if(Current is not{}q)return;title.Text=q.Name;stats.Text=$"{q.Grams:N2} g de filamento  ·  {Pricing.Duration(q.Seconds)}  ·  {q.Plates.Count} mesa(s)";method.Text=q.Method;
        percent.Enabled=fixedFee.Enabled=q.Shopee;breakdown.Rows.Clear();
        try
        {
            var r=Pricing.Calculate(q);costValue.Text=Pricing.Money(r.PrintCost);priceValue.Text=Pricing.Money(r.Suggested);orderValue.Text=Pricing.Money(r.OrderTotal);
            minimum.Text=$"Mínimo por unidade: {Pricing.Money(r.Minimum)}  ·  Sugestão arredondada para cima  ·  Pedido: {q.OrderQuantity} unidade(s)";
            void Row(string text,decimal v)=>breakdown.Rows.Add(text,Pricing.Money(v));
            Row("Filamento",r.Material);Row("Energia estimada",r.Energy);Row("Reserva da máquina",r.Machine);Row("Tempo adicional",r.Time);Row("Outros custos",r.Other);Row("Custo de impressão",r.PrintCost);Row("Lucro após taxas e mão de obra*",r.Profit);Row("Mão de obra",r.Labor);Row("Taxa Shopee*",r.Fee);Row("Preço mínimo",r.Minimum);RefreshPlatePrices(q);
            breakdown.Rows[^1].DefaultCellStyle.Font=new Font("Segoe UI Semibold",10);
            breakdown.AccessibleDescription="Composição no preço mínimo. A sugestão arredondada pode gerar lucro maior.";
        }
        catch(InvalidDataException ex){plates.Rows.Clear();costValue.Text=priceValue.Text=orderValue.Text="—";minimum.Text=ex.Message;}
    }
    async Task Import(IEnumerable<string> paths)
    {
        if(importCancel!=null)return;importCancel=new CancellationTokenSource();importButton.Enabled=manualButton.Enabled=false;cancelButton.Enabled=true;UpdateActions();
        int success=0;var errors=new List<string>();
        try
        {
            foreach(var file in paths)
            {
                try
                {
                    var progress=new Progress<string>(s=>status.Text=Path.GetFileName(file)+": "+s);
                    var token=importCancel.Token;
                    var q=await Task.Run(()=>Slicer.ImportAsync(file,data.BambuPath,store,progress,token),token);
                    q.Settings=data.Defaults.Copy();data.Quotes.Insert(0,q);searchBox.Clear();groupFilter.SelectedIndex=0;RefreshCatalog(q.Id);Save();success++;
                }
                catch(OperationCanceledException){status.Text="Análise cancelada. Os arquivos originais foram preservados.";return;}
                catch(Exception ex){errors.Add(Path.GetFileName(file)+": "+ex.Message);}
            }
            status.Text=$"{success} arquivo(s) importado(s). "+(errors.Count>0?$"{errors.Count} arquivo(s) precisam de revisão.":"Preços calculados. Confira a unidade de venda e os custos.");
            if(errors.Count>0)MessageBox.Show(this,string.Join("\r\n\r\n",errors),"Arquivos que precisam de revisão",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        finally{importCancel.Dispose();importCancel=null;importButton.Enabled=manualButton.Enabled=true;cancelButton.Enabled=false;UpdateActions();}
    }
    void Save()
    {
        try{store.Save(data);}catch(Exception ex){status.Text="Não foi possível salvar os dados: "+ex.Message;}
    }
    void BrowseBambu()
    {
        using var d=new OpenFileDialog{Title="Selecione o Bambu Studio instalado",Filter="Bambu Studio|bambu-studio.exe"};
        if(d.ShowDialog(this)==DialogResult.OK){data.BambuPath=d.FileName;bambuPath.Text=d.FileName;Save();}
    }
    void Export()
    {
        if(Current is not{}q)return;
        try
        {
            _=Pricing.Calculate(q);
            using var d=new SaveFileDialog{Filter="PDF para cliente|*.pdf|PDF detalhado interno|*.pdf|Relatório HTML interno|*.html|Planilha CSV|*.csv",DefaultExt="pdf",FileName=string.Join("_",q.Name.Split(Path.GetInvalidFileNameChars()))+" - Orçamento"};
            if(d.ShowDialog(this)!=DialogResult.OK)return;
            if(d.FilterIndex==1&&string.IsNullOrWhiteSpace(data.Company.Name)){tabs.SelectedIndex=2;status.Text="Configure e salve os dados da sua empresa antes de exportar o PDF para cliente.";MessageBox.Show(this,"Informe pelo menos o nome da empresa e salve na aba Empresa / PDF do cliente. Depois exporte novamente.","Personalizar o PDF");return;}
            switch(d.FilterIndex){case 1:PdfReports.Save(d.FileName,q,false,data.Company);break;case 2:PdfReports.Save(d.FileName,q,true);break;case 3:Reports.Html(d.FileName,q);break;case 4:Reports.Csv(d.FileName,q);break;}
            status.Text="Orçamento exportado: "+d.FileName;
        }catch(Exception ex){MessageBox.Show(this,ex.Message,"Exportar orçamento",MessageBoxButtons.OK,MessageBoxIcon.Information);}
    }
    void Backup()
    {
        using var d=new SaveFileDialog{Filter="Backup do precificador|*.json",FileName="FG-Precificador-Backup-"+DateTime.Now.ToString("yyyy-MM-dd")+".json"};
        if(d.ShowDialog(this)!=DialogResult.OK)return;
        try{File.WriteAllText(d.FileName,JsonSerializer.Serialize(data,Store.Json));status.Text="Backup dos custos e orçamentos salvo. As miniaturas ficam na pasta de dados do aplicativo.";}catch(Exception ex){MessageBox.Show(this,ex.Message);}
    }
    Image? ListThumbnail(Quote q)
    {
        if(string.IsNullOrWhiteSpace(q.Thumbnail)||!File.Exists(q.Thumbnail))return null;
        if(thumbnailCache.TryGetValue(q.Thumbnail,out var cached))return cached;
        Image? result=null;
        try
        {
            using var file=File.OpenRead(q.Thumbnail);using var source=Image.FromStream(file);
            var bitmap=new Bitmap(144,144);using(var g=Graphics.FromImage(bitmap))
            {g.Clear(Color.White);g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;var scale=Math.Min(136f/source.Width,136f/source.Height);var w=source.Width*scale;var h=source.Height*scale;g.DrawImage(source,(144-w)/2,(144-h)/2,w,h);}
            result=bitmap;
        }catch{}
        thumbnailCache[q.Thumbnail]=result;return result;
    }
    void UpdateActions()
    {editButton.Enabled=openProjectButton.Enabled=deleteButton.Enabled=Current!=null&&catalog.SelectedItems.Count==1&&importCancel==null;moveGroupButton.Enabled=Current!=null&&importCancel==null;undoButton.Enabled=data.DeletedQuotes.Count>0&&importCancel==null;resliceButton.Enabled=Current!=null&&importCancel==null;}
    void EditCurrent()
    {
        if(Current==null||importCancel!=null)return;
        tabs.SelectedIndex=0;name.Focus();name.SelectAll();status.Text="Edite os campos do orçamento selecionado. As alterações são salvas automaticamente.";
    }
    void DeleteCurrent()
    {
        if(Current is not{}q||importCancel!=null)return;
        if(MessageBox.Show(this,$"Excluir o orçamento “{q.Name}” da lista?\r\n\r\nO arquivo do projeto será mantido. Você poderá usar Desfazer exclusão para recuperar o orçamento.","Excluir orçamento",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
        saveTimer.Stop();
        try
        {
            var index=catalog.SelectedIndex;if(!store.DeleteQuote(data,q.Id))return;
            catalog.Items.Remove(q);if(catalog.Items.Count>0)catalog.SelectedIndex=Math.Min(index,catalog.Items.Count-1);else LoadCurrent();
            UpdateActions();status.Text="Orçamento excluído. Use Desfazer exclusão para recuperá-lo.";
        }catch(Exception ex){MessageBox.Show(this,"Não foi possível excluir o orçamento: "+ex.Message);}
    }
    void UndoDelete()
    {
        if(importCancel!=null)return;saveTimer.Stop();
        try
        {
            var q=store.RestoreLastDeleted(data);if(q==null)return;
            catalog.Items.Insert(data.Quotes.IndexOf(q),q);catalog.SelectedItem=q;UpdateActions();status.Text="Orçamento recuperado com seus custos e miniatura.";
        }catch(Exception ex){MessageBox.Show(this,"Não foi possível recuperar o orçamento: "+ex.Message);}
    }
}
