using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace FG.Precificador;

public static class PdfReports
{
    public static void Save(string path,Quote quote,bool internalDetails=false,CompanyProfile? company=null)
    {
        var price=Pricing.Calculate(quote);
        var identity=internalDetails?CompanyProfile.Fg():company??throw new InvalidDataException("Configure os dados da empresa antes de exportar o PDF para cliente.");
        if(!internalDetails)identity.Validate();
        using var report=new ReportCanvas(quote,internalDetails,identity);
        report.Product();
        report.Row("Preço por unidade",Pricing.Money(price.Suggested));
        report.Row("Quantidade do pedido",quote.OrderQuantity.ToString("N0")+" unidade(s)");
        report.Total("TOTAL DO PEDIDO",Pricing.Money(price.OrderTotal));
        if(internalDetails)
        {
            report.Section("Composição por unidade");
            foreach(var row in new[]{("Filamento",price.Material),("Energia estimada",price.Energy),("Reserva da máquina",price.Machine),("Tempo adicional",price.Time),("Outros custos",price.Other),("Custo de impressão",price.PrintCost),("Mão de obra",price.Labor),("Taxa Shopee no preço mínimo",price.Fee),("Lucro no preço mínimo, após taxas e mão de obra",price.Profit),("Preço mínimo",price.Minimum)})report.Row(row.Item1,Pricing.Money(row.Item2),true);
            report.Section("Placas do arquivo inteiro");
            report.Paragraph("Mão de obra: "+Pricing.LaborLabel(quote.Settings.LaborBasis)+". O preço mínimo por placa é um rateio do total do arquivo, não um preço independente de venda. Mão de obra por projeto é rateada pelo tempo; filamento extra e outros custos, pelo material.",9);
            foreach(var p in Pricing.ByPlate(quote))
            {
                report.Section("Placa "+p.Plate.Index);report.PlatePreview(p.Plate.Thumbnail);
                report.Paragraph($"{p.Plate.Grams:N2} g | {Pricing.Duration(p.Plate.Seconds)}. Material: {Pricing.Money(p.Material)}; energia: {Pricing.Money(p.Energy)}; máquina: {Pricing.Money(p.Machine)}; tempo extra: {Pricing.Money(p.Time)}; outros: {Pricing.Money(p.Other)}.",9);
                report.Row("Custo de impressão",Pricing.Money(p.Cost),true);report.Row("Mão de obra",Pricing.Money(p.Labor),true);report.Row("Mínimo rateado",Pricing.Money(p.Minimum),true);
            }
            report.Row("Mínimo do arquivo inteiro",Pricing.Money(price.Minimum*quote.UnitsInFile));
            report.Section("Dados e parâmetros utilizados");
            var s=quote.Settings;
            report.Paragraph($"Arquivo: {quote.Grams:N2} g de filamento | {Pricing.Duration(quote.Seconds)} | {quote.Plates.Count} mesa(s) | {quote.UnitsInFile} unidade(s) de venda contida(s).",9);
            report.Paragraph($"Filamento: {Pricing.Money(s.FilamentPerKg)}/kg. Energia: {Pricing.Money(s.EnergyPerKwh)}/kWh e {s.Watts:N0} W. Máquina: {Pricing.Money(s.MachinePerHour)}/h. Tempo adicional: {Pricing.Money(s.TimePerHour)}/h. Lucro sobre custo: {s.MarkupPercent:N2}%.",9);
            report.Paragraph($"Filamento extra por arquivo: {quote.ExtraGrams:N2} g. Outros custos por unidade: {Pricing.Money(quote.OtherPerUnit)}. Shopee: "+(quote.Shopee?$"{quote.ShopeePercent:N2}% mais {Pricing.Money(quote.ShopeeFixed)} por unidade.":"desativada."),9);
            report.Paragraph("O preço direto aplica o lucro aos custos de impressão e acrescenta a mão de obra ao final. Com Shopee, a taxa é repassada para preservar a receita pretendida. O mínimo é arredondado para cima ao centavo; a sugestão, ao real inteiro. Valores intermediários não são arredondados.",9);
            report.Paragraph("Energia e reserva da máquina são estimativas ajustáveis. Purga inicial, calibração, perdas e embalagem entram quando informadas nos custos adicionais. A quantidade do pedido multiplica o preço; não representa novo fatiamento em lote.",9);
            report.Section("Origem do cálculo");
            report.Paragraph(quote.Method,9);report.Paragraph(quote.Profile,9);
            report.Paragraph("Arquivo: "+quote.Source,8);
            if(quote.Sha256.Length>0)report.Paragraph("SHA-256: "+quote.Sha256,8);
            if(quote.Notes.Length>0)report.Paragraph(quote.Notes,8);
        }
        else
        {
            report.Section("Condições do orçamento");
            report.Paragraph("Valores em reais para o produto e a quantidade informados. Para personalizações ou outra quantidade, solicite um novo orçamento.",10);
            if(identity.ValidityDays>0)report.Paragraph($"Validade: {identity.ValidityDays} dia(s) a partir da emissão, até {DateTime.Today.AddDays(identity.ValidityDays):dd/MM/yyyy}.",10);
            if(identity.PaymentTerms.Length>0)report.Paragraph("Pagamento: "+identity.PaymentTerms,10);
            if(identity.DeliveryTerms.Length>0)report.Paragraph("Produção / entrega: "+identity.DeliveryTerms,10);
            if(identity.Notes.Length>0)report.Paragraph(identity.Notes,10);
            report.ContactDetails(identity.ContactLines());
        }
        report.Finish(path);
    }

    sealed class ReportCanvas : IDisposable
    {
        readonly PdfDocument document=new();
        readonly Quote quote;
        readonly bool internalDetails;
        readonly CompanyProfile identity;
        readonly List<XFont> fonts=[];
        readonly XColor navy=XColor.FromArgb(18,39,62), teal=XColor.FromArgb(0,111,104), muted=XColor.FromArgb(82,99,114), line=XColor.FromArgb(222,231,237);
        PdfPage page=null!;
        XGraphics graphics=null!;
        double y;
        const double Left=42, Width=511, Bottom=780;
        public ReportCanvas(Quote quote,bool internalDetails,CompanyProfile identity)
        {
            this.quote=quote;this.internalDetails=internalDetails;this.identity=identity;
            document.Info.Title=quote.Name+" - Orçamento "+identity.Name;document.Info.Author=identity.Name;
            document.Info.Subject=internalDetails?"Orçamento detalhado de uso interno":"Orçamento para cliente";
            NewPage();
        }
        XFont Font(double size,bool bold=false)
        {
            var font=new XFont("Segoe UI",size,bold?XFontStyleEx.Bold:XFontStyleEx.Regular);
            fonts.Add(font);return font;
        }
        void Text(string text,double x,double top,double width,double height,double size=10,bool bold=false,XColor? color=null,bool right=false)
        {
            if(right)while(size>8&&graphics.MeasureString(text,Font(size,bold)).Width>width)size-=.5;
            graphics.DrawString(text,Font(size,bold),new XSolidBrush(color??navy),new XRect(x,top,width,height),right?XStringFormats.TopRight:XStringFormats.TopLeft);
        }
        void NewPage()
        {
            graphics?.Dispose();page=document.AddPage();page.Size=PageSize.A4;graphics=XGraphics.FromPdfPage(page);
            if(identity.LogoPngBase64.Length>0)
            {
                using var stream=new MemoryStream(Convert.FromBase64String(identity.LogoPngBase64));using var image=XImage.FromStream(stream);FitImage(image,Left,26,145,118);
            }
            else{var lines=Wrap(identity.Name,145,15,true);for(var i=0;i<Math.Min(lines.Count,5);i++)Text(lines[i],Left,39+i*20,145,22,15,true);}
            Text("ORÇAMENTO",252,43,301,30,23,true);
            var names=Wrap(internalDetails?"Uso interno - custos e preços":identity.Name,301,10);
            for(var i=0;i<Math.Min(names.Count,2);i++)Text(names[i],252,77+i*14,301,18,10,false,muted);
            Text(DateTimeOffset.Now.ToString("dd/MM/yyyy"),252,112,301,18,10,false,muted);
            var contact=identity.Instagram.Length>0?CompanyProfile.NormalizeInstagram(identity.Instagram):identity.Email.Length>0?identity.Email:identity.Phone;
            Text(Wrap(contact,301,10).FirstOrDefault()??"",252,132,301,18,10,false,teal);
            graphics.DrawLine(new XPen(line,1),Left,157,Left+Width,157);y=177;
        }
        void Ensure(double height){if(y+height>Bottom)NewPage();}
        void FitImage(XImage image,double x,double top,double width,double height)
        {
            double scale=Math.Min(width/image.PixelWidth,height/image.PixelHeight);
            var w=image.PixelWidth*scale;var h=image.PixelHeight*scale;graphics.DrawImage(image,x+(width-w)/2,top+(height-h)/2,w,h);
        }
        List<string> Wrap(string text,double width,double size,bool bold=false)
        {
            var font=Font(size,bold);var lines=new List<string>();
            foreach(var paragraph in text.Replace("\r","").Split('\n'))
            {
                var current="";
                foreach(var word in paragraph.Split(' ',StringSplitOptions.RemoveEmptyEntries))
                {
                    if(graphics.MeasureString(current.Length==0?word:current+" "+word,font).Width<=width){current=current.Length==0?word:current+" "+word;continue;}
                    if(current.Length>0){lines.Add(current);current="";}
                    foreach(var ch in word)
                    {
                        if(graphics.MeasureString(current+ch,font).Width>width&&current.Length>0){lines.Add(current);current="";}
                        current+=ch;
                    }
                }
                if(current.Length>0)lines.Add(current);
            }
            return lines;
        }
        public void Product()
        {
            var nameLines=Wrap(quote.Name,Width-142,17,true);double block=Math.Max(122,nameLines.Count*23+46);Ensure(Math.Min(block,Bottom-177));
            var top=y;
            if(File.Exists(quote.Thumbnail))
            {try{using var image=XImage.FromFile(quote.Thumbnail);FitImage(image,Left,top,120,112);}catch{}}
            var textX=File.Exists(quote.Thumbnail)?Left+142:Left;
            var textWidth=File.Exists(quote.Thumbnail)?Width-142:Width;
            nameLines=Wrap(quote.Name,textWidth,17,true);
            foreach(var item in nameLines){Ensure(25);Text(item,textX,y,textWidth,25,17,true);y+=23;}
            Ensure(34);Text("ORC-"+quote.Created.ToString("yyyyMMdd")+"-"+quote.Id.ToString("N")[..8].ToUpperInvariant(),textX,y+9,textWidth,20,9,false,muted);
            y=Math.Max(y+45,top+122);
        }
        public void Paragraph(string text,double size=10)
        {
            foreach(var item in Wrap(text,Width,size)){Ensure(size*1.5+4);Text(item,Left,y,Width,size*1.6,size,false,muted);y+=size*1.5;}
            y+=9;
        }
        public void PlatePreview(string path)
        {
            if(!File.Exists(path))return;
            try{using var image=XImage.FromFile(path);Ensure(92);FitImage(image,Left,y,120,84);y+=92;}catch{ }
        }
        public void Section(string text)
        {Ensure(65);y+=18;Text(text,Left,y,Width,24,12,true);y+=31;}
        public void ContactDetails(IEnumerable<string> lines)
        {
            var text=string.Join(" | ",lines);var count=Wrap(text,Width,9).Count;
            Ensure(58+count*13.5);Section("Dados da empresa");Paragraph(text,9);
        }
        public void Row(string label,string value,bool compact=false)
        {
            var size=compact?10:11;var lines=Wrap(label,Width-150,size);var height=Math.Max(compact?26:32,lines.Count*15+11);Ensure(height+4);
            for(var i=0;i<lines.Count;i++)Text(lines[i],Left,y+5+i*15,Width-150,17,size);
            Text(value,Left+Width-145,y+5,145,20,size,true,null,true);
            graphics.DrawLine(new XPen(line,.6),Left,y+height,Left+Width,y+height);y+=height;
        }
        public void Total(string label,string value)
        {
            Ensure(86);y+=15;graphics.DrawRectangle(new XSolidBrush(teal),Left,y,Width,62);
            Text(label,Left+17,y+23,Width*.51,20,10,true,XColors.White);
            Text(value,Left+Width*.48,y+16,Width*.52-17,36,23,true,XColors.White,true);y+=78;
        }
        public void Finish(string path)
        {
            var footer=identity.Name+(identity.Instagram.Length>0?" | "+CompanyProfile.NormalizeInstagram(identity.Instagram):"");
            var footerLine=Wrap(footer,Width-80,8).FirstOrDefault()??"";
            graphics.Dispose();graphics=null!;
            for(var i=0;i<document.Pages.Count;i++)
            {
                using var g=XGraphics.FromPdfPage(document.Pages[i],XGraphicsPdfPageOptions.Append);
                g.DrawLine(new XPen(line,.6),Left,801,Left+Width,801);
                g.DrawString(footerLine,Font(8),new XSolidBrush(muted),new XRect(Left,811,Width-80,16),XStringFormats.TopLeft);
                g.DrawString($"{i+1} / {document.Pages.Count}",Font(8),new XSolidBrush(muted),new XRect(Left+Width-80,811,80,16),XStringFormats.TopRight);
            }
            // Write to a sibling temporary file so an interrupted export does not corrupt an existing PDF.
            var temp=Path.GetFullPath(path)+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{document.Save(temp);File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public void Dispose(){graphics?.Dispose();document.Dispose();}
    }
}
