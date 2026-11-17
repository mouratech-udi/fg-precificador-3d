namespace FG.Precificador;

public sealed partial class MainForm
{
    TabPage CompanyTab()
    {
        var tab=new TabPage("Empresa / PDF do cliente"){BackColor=Color.White,Padding=new Padding(18),AutoScroll=true};
        var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,ColumnStyles={new ColumnStyle(SizeType.Percent,64),new ColumnStyle(SizeType.Percent,36)}};
        var fields=FormTable();fields.ColumnStyles.Clear();fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,36));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,64));fields.Padding=new Padding(0,0,22,0);
        foreach(var item in new[]{(nameof(CompanyProfile.Name),"Nome da empresa *",120),(nameof(CompanyProfile.Document),"CPF / CNPJ (opcional)",40),(nameof(CompanyProfile.Instagram),"Instagram",120),(nameof(CompanyProfile.Phone),"Telefone / WhatsApp",80),(nameof(CompanyProfile.Email),"E-mail",160),(nameof(CompanyProfile.Website),"Site",200),(nameof(CompanyProfile.Address),"Endereço",240),(nameof(CompanyProfile.City),"Cidade / estado",120),(nameof(CompanyProfile.ContactName),"Responsável",120),(nameof(CompanyProfile.PaymentTerms),"Condições de pagamento",500),(nameof(CompanyProfile.DeliveryTerms),"Prazo de produção / entrega",500),(nameof(CompanyProfile.Notes),"Observações do orçamento",1600)})
        {
            var box=new TextBox{Dock=DockStyle.Fill,MaxLength=item.Item3};
            if(item.Item1==nameof(CompanyProfile.Notes)){box.Multiline=true;box.Height=76;box.ScrollBars=ScrollBars.Vertical;}
            companyFields[item.Item1]=box;Field(fields,item.Item2,box);
        }
        Field(fields,"Validade em dias (0 = não informar)",validity);layout.Controls.Add(fields,0,0);
        var side=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(6)};
        side.Controls.Add(TextLabel("Logo no PDF do cliente",true));side.Controls.Add(companyPicture);
        var choose=Button("Escolher logo PNG / JPG");choose.Margin=new Padding(0,12,0,8);choose.Click+=(_,_)=>ChooseCompanyLogo();side.Controls.Add(choose);
        var remove=Button("Usar somente o nome");remove.Click+=(_,_)=>{companyLogo="";CompanyPreview();};side.Controls.Add(remove);
        var info=TextLabel("Os dados desta aba aparecem somente no PDF para cliente. A identidade da FG permanece no programa e nos relatórios internos. Apenas o nome é obrigatório; os campos em branco não são impressos.");info.MaximumSize=new Size(275,0);info.Margin=new Padding(0,18,0,14);side.Controls.Add(info);
        var save=Button("Salvar dados da empresa",true);save.Click+=(_,_)=>SaveCompany();side.Controls.Add(save);
        var backupInfo=TextLabel("A logo fica incluída no backup dos dados. Você pode mover a imagem original depois de salvar.");backupInfo.MaximumSize=new Size(275,0);backupInfo.Margin=new Padding(0,18,0,0);side.Controls.Add(backupInfo);
        layout.Controls.Add(side,1,0);tab.Controls.Add(layout);
        foreach(var field in companyFields)field.Value.Text=(string?)typeof(CompanyProfile).GetProperty(field.Key)!.GetValue(data.Company)??"";
        validity.Value=Math.Clamp(data.Company.ValidityDays,0,3650);companyLogo=data.Company.LogoPngBase64;CompanyPreview();return tab;
    }
    void CompanyPreview()
    {
        companyPicture.Image?.Dispose();companyPicture.Image=null;
        if(companyLogo.Length==0)return;
        try{using var source=new MemoryStream(Convert.FromBase64String(companyLogo));using var image=Image.FromStream(source);companyPicture.Image=new Bitmap(image);}catch{status.Text="A logo não pôde ser lida. Escolha outra imagem.";}
    }
    void ChooseCompanyLogo()
    {
        using var picker=new OpenFileDialog{Title="Escolha a logo da sua empresa",Filter="Logo PNG ou JPG|*.png;*.jpg;*.jpeg"};
        if(picker.ShowDialog(this)!=DialogResult.OK)return;
        try{companyLogo=CompanyProfile.ReadLogo(picker.FileName);CompanyPreview();status.Text="Logo selecionada. Clique em Salvar dados da empresa para aplicá-la ao PDF.";}
        catch(Exception ex){MessageBox.Show(this,ex.Message,"Escolher logo",MessageBoxButtons.OK,MessageBoxIcon.Information);}
    }
    void SaveCompany()
    {
        var profile=new CompanyProfile{LogoPngBase64=companyLogo,ValidityDays=(int)validity.Value};
        foreach(var field in companyFields)typeof(CompanyProfile).GetProperty(field.Key)!.SetValue(profile,field.Value.Text.Trim());
        profile.Instagram=CompanyProfile.NormalizeInstagram(profile.Instagram);
        var previous=data.Company;
        try
        {
            profile.Validate();data.Company=profile;store.Save(data);companyFields[nameof(CompanyProfile.Instagram)].Text=profile.Instagram;
            status.Text="Empresa salva. Os próximos PDFs para cliente usarão sua logo e seus contatos.";
        }
        catch(Exception ex){data.Company=previous;MessageBox.Show(this,ex.Message,"Dados da empresa",MessageBoxButtons.OK,MessageBoxIcon.Information);}
    }
}
