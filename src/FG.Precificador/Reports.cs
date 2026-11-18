using System.Net;
using System.Text;

namespace FG.Precificador;

public static class Reports
{
    public static void Html(string path,Quote q)
    {
        var r=Pricing.Calculate(q);var s=q.Settings;string E(string x)=>WebUtility.HtmlEncode(x);var rows=new StringBuilder();
        var plateRows=string.Join("",Pricing.ByPlate(q).Select(p=>$"<tr><td>{p.Plate.Index}</td><td>{p.Plate.Grams:N2} g</td><td>{Pricing.Duration(p.Plate.Seconds)}</td><td>{Pricing.Money(p.Cost)}</td><td>{Pricing.Money(p.Labor)}</td><td>{Pricing.Money(p.Minimum)}</td></tr>"));
        var shopeeText=q.Shopee?$"{q.ShopeePercent:N2}% + {Pricing.Money(q.ShopeeFixed)} por unidade":"desativada";
        string logo="";
        using(var source=typeof(Reports).Assembly.GetManifestResourceStream("FG.Marca"))
        {if(source!=null){using var buffer=new MemoryStream();source.CopyTo(buffer);logo="data:image/png;base64,"+Convert.ToBase64String(buffer.ToArray());}}
        foreach(var x in new[]{("Filamento",r.Material),("Energia estimada",r.Energy),("Reserva da máquina",r.Machine),("Tempo adicional",r.Time),("Outros custos",r.Other),("Custo de impressão",r.PrintCost),("Mão de obra",r.Labor),("Shopee no preço mínimo",r.Fee),("Lucro no preço mínimo, após taxas e mão de obra",r.Profit),("Preço mínimo",r.Minimum),("Preço sugerido",r.Suggested)}) rows.Append($"<tr><td>{E(x.Item1)}</td><td>{Pricing.Money(x.Item2)}</td></tr>");
        File.WriteAllText(path,$$"""
        <!doctype html><html lang="pt-BR"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{{E(q.Name)}} — FG Precificador 3D</title>
        <style>body{font:16px 'Segoe UI',sans-serif;color:#12273e;max-width:900px;margin:40px auto;padding:0 24px;line-height:1.5}h1{font-size:28px}table{border-collapse:collapse;width:100%;margin:24px 0}td,th{padding:10px;border-bottom:1px solid #dce5ea;text-align:left}td:last-child{text-align:right}.price{font-size:30px;color:#006f68}small{color:#526372}@media print{body{margin:0}button{display:none} }</style>
        <header><img src="{{logo}}" alt="FG Design Print · @fgdesigneprint" style="display:block;width:220px;max-width:100%;height:auto"><small>FG DESIGN PRINT · ORÇAMENTO DE PRODUÇÃO · {{DateTimeOffset.Now:dd/MM/yyyy HH:mm}}</small><h1>{{E(q.Name)}}</h1></header>
        <p class="price">{{Pricing.Money(r.Suggested)}} / unidade</p><p>Pedido: {{q.OrderQuantity}} unidade(s) · Total sugerido: <strong>{{Pricing.Money(r.OrderTotal)}}</strong></p>
        <p>Arquivo: {{q.Grams:N2}} g · {{Pricing.Duration(q.Seconds)}} · {{q.Plates.Count}} mesa(s) · {{q.UnitsInFile}} unidade(s) de venda contida(s) no arquivo.</p>
        <table><thead><tr><th>Composição por unidade</th><th>Valor</th></tr></thead><tbody>{{rows}}</tbody></table>
        <h2>Placas do arquivo inteiro</h2><p>Mão de obra: {{E(Pricing.LaborLabel(s.LaborBasis))}}. O mínimo de cada placa é um rateio do total do arquivo.</p><table><tr><th>Placa</th><th>Material</th><th>Tempo</th><th>Custo</th><th>Mão de obra</th><th>Mínimo rateado</th></tr>{{plateRows}}</table><p>Mínimo do arquivo: {{Pricing.Money(r.Minimum*q.UnitsInFile)}}</p>
        <p>Filamento: {{Pricing.Money(s.FilamentPerKg)}}/kg · Energia: {{Pricing.Money(s.EnergyPerKwh)}}/kWh · {{s.Watts:N0}} W · Máquina: {{Pricing.Money(s.MachinePerHour)}}/h · Tempo extra: {{Pricing.Money(s.TimePerHour)}}/h · Lucro sobre custo: {{s.MarkupPercent:N2}}%.</p>
        <p>Shopee: {{shopeeText}}. Filamento extra: {{q.ExtraGrams:N2}} g por arquivo. Outros custos: {{Pricing.Money(q.OtherPerUnit)}} por unidade.</p>
        <p>Preço direto = custo de impressão × (1 + lucro %) + mão de obra. Com Shopee: (preço direto + taxa fixa) ÷ (1 − percentual). O preço mínimo é arredondado para cima ao centavo; a sugestão, ao real inteiro. Valores intermediários são calculados sem arredondamento.</p>
        <p><small>Estimativa do fatiador. Energia e reserva da máquina são hipóteses ajustáveis. Purga inicial, calibração, perdas e embalagem só entram se incluídas nos campos de custos. A quantidade do pedido não representa novo fatiamento em lote.</small></p>
        <p><small>{{E(q.Method)}}<br>{{E(q.Profile)}}<br>Origem: {{E(q.Source)}}<br>SHA-256: {{E(q.Sha256)}}<br>{{E(q.Notes)}}</small></p>
        <p><small>Referências de energia consultadas em 17/11/2026: <a href="https://www.cemig.com.br/valores-e-tarifas/tarifas-vigentes/">Cemig</a> e <a href="https://forum.bambulab.com/t/power-consumption-for-a1-printer/92095/14">fórum Bambu</a>. Os parâmetros padrão R$ 1,20/kWh, 120 W e R$ 1/h são estimativas de orçamento.</small></p></html>
        """,new UTF8Encoding(true));
    }
    public static void Csv(string path,Quote q)
    {
        var r=Pricing.Calculate(q);string V(string s)
        {
            if(s.Length>0&&"=+-@\t\r\n".Contains(s.TrimStart().FirstOrDefault()))s="'"+s;
            return "\""+s.Replace("\"","\"\"")+"\"";
        }
        File.WriteAllLines(path,new[]{"Produto;Filamento arquivo (g);Tempo arquivo (s);Unidades arquivo;Quantidade pedido;Custo unitário;Mínimo unitário;Sugerido unitário;Total pedido;Shopee;Filamento R$/kg;Energia R$/kWh;Watts;Máquina R$/h;Tempo extra R$/h;Mão de obra calculada R$/unidade;Cobrança de mão de obra;Valor configurado R$;Lucro %;Extra g;Outros R$/unidade;Taxa %;Taxa fixa;Origem",string.Join(";",new[]{V(q.Name),q.Grams.ToString(),q.Seconds.ToString(),q.UnitsInFile.ToString(),q.OrderQuantity.ToString(),r.PrintCost.ToString(),r.Minimum.ToString(),r.Suggested.ToString(),r.OrderTotal.ToString(),q.Shopee?"Sim":"Não",q.Settings.FilamentPerKg.ToString(),q.Settings.EnergyPerKwh.ToString(),q.Settings.Watts.ToString(),q.Settings.MachinePerHour.ToString(),q.Settings.TimePerHour.ToString(),r.Labor.ToString(),V(Pricing.LaborLabel(q.Settings.LaborBasis)),q.Settings.Labor.ToString(),q.Settings.MarkupPercent.ToString(),q.ExtraGrams.ToString(),q.OtherPerUnit.ToString(),q.ShopeePercent.ToString(),q.ShopeeFixed.ToString(),V(q.Source)})},new UTF8Encoding(true));
    }
}
