# FG Precificador 3D — 1.4.1

Correção 1.4.1: pesquisa, grupos, lista e botões têm áreas separadas. Projetos recém-importados ficam visíveis no início da lista, sem serem cobertos pelos filtros. O histórico não precisa ser reimportado.

Novidade 1.4.0: visualização 3D, ajustes com refatiamento em cópia, placas com custos, mão de obra por projeto ou placa, pesquisa e grupos/subgrupos, abertura de projeto e aviso opcional de atualizações.

Novidade 1.3.0: motor BambuStudio 02.08.02.60 integrado ao instalador e à versão portátil. O fatiamento de projetos configurados não exige instalar o Bambu Studio. O motor usa dados temporários próprios, sem conta ou plugin de rede. Mantenha a pasta Engine junto do executável. Veja também Engine/MOTOR-E-FONTES.md para origem, fontes e licenças.

Novidades 1.2.0: cadastro da empresa, logo e contatos próprios no PDF para cliente. A marca FG é mantida no programa e no relatório interno. Novas instalações começam sem produtos ou dados pessoais do autor.

Novidades 1.1.0: exportação direta em PDF para cliente ou detalhado interno, miniaturas na lista, menu do botão direito para editar/excluir, botões de organização, recuperação da última exclusão e atalho na área de trabalho.

Correção 1.0.1: projetos salvos em outra versão do Bambu Studio deixam de ser bloqueados quando apenas o campo de identificação `version` é atualizado. Os parâmetros de impressão continuam sendo conferidos. Compatibilidade conferida entre as versões 02.07.01.57 e 02.08.02.60. O histórico e os custos existentes usam a mesma pasta de dados.

Programa para Windows 10/11 de 64 bits, desenvolvido pela FG Design Print. Calcula custos e preços de impressão 3D a partir de arquivos analisados localmente.

O cabeçalho tem a identidade da FG e o Instagram **@fgdesigneprint**. A identidade de quem utiliza o programa pode ser cadastrada separadamente para aparecer no PDF para cliente.

## Instalar ou usar sem instalar

- Instalador: abra `FG-Precificador-3D-1.4.1-x64.msi`. A instalação é por usuário; os atalhos aparecem no menu Iniciar e na área de trabalho como **FG Precificador 3D**.
- Portátil: extraia o ZIP inteiro e abra `FG.Precificador.exe`.
- O programa inclui o runtime e o motor de fatiamento necessários. O Bambu Studio não precisa estar instalado. Extraia sempre o ZIP completo.
- Instalador e programa não possuem assinatura de editor Authenticode nesta primeira versão.

## Importar e calcular

1. Clique em **Importar arquivo** ou arraste um ou mais arquivos para a janela.
2. Selecione um `.3mf` configurado no Bambu Studio. Um `.3mf` fatiado ou `.gcode` com peso e tempo no cabeçalho também pode ser lido.
3. Se o projeto ainda não tiver resultados, o programa chama o motor integrado, em uma cópia local do arquivo. As configurações são conferidas e o original fica intacto.
4. Revise **Orçamento**. **Arquivo e mesas** mostra o consumo de cada mesa, o perfil e a origem do resultado.
5. Ajuste custos ou a taxa Shopee quando necessário. As alterações do orçamento são salvas automaticamente no histórico local.

A importação é local: não há envio do arquivo para um servidor nem para a impressora. O motor incluído não contém o plugin opcional de rede. Ele usa uma pasta de dados separada por análise, removida ao terminar.

O fatiamento automático foi conferido com Bambu Studio **02.08.02.60** e projetos da A1. Versões ou projetos diferentes podem precisar de revisão. Se houver configuração não preservada, pós-processamento externo, mesa incompleta ou formato não reconhecido, o programa pede para exportar um projeto já fatiado. Não inventa peso nem tempo.

STL e OBJ precisam primeiro ser configurados no Bambu Studio e salvos como projeto 3MF. Não é possível determinar o consumo correto apenas pelo modelo sem perfil de impressão.

## Unidades no arquivo e quantidade do pedido

**Unidades já contidas no arquivo** divide o peso e o tempo total entre os produtos vendáveis que o arquivo realmente contém. O padrão é **1**, tratando o arquivo inteiro como uma unidade de venda. Um kit com várias partes também pode ser uma unidade.

**Quantidade do pedido** multiplica o preço de venda. Ela **não divide o consumo** e não simula automaticamente uma nova disposição em lote. Para obter economia real de um lote, configure esse lote no Bambu Studio e importe o arquivo correspondente. Em arquivos com produtos diferentes, não divida o consumo igualmente entre eles; importe cada produto separadamente ou trate o conjunto como kit.

## Abrir, pesquisar e organizar

Use **Abrir projeto** no orçamento selecionado para acessar a aba Projeto 3D. **Abrir no aplicativo associado** abre o original no programa configurado no Windows. Se o original não for encontrado, localize-o novamente: a importação cria outro orçamento, sem associar dados antigos a um arquivo diferente.

O campo **Pesquisar modelo** filtra nomes, ignorando maiúsculas e acentos. O filtro de grupos inclui seus subgrupos. **Novo grupo** cria no nível principal; selecione um grupo e use **Subgrupo** para criar dentro dele. Selecione vários orçamentos com Ctrl ou Shift e use **Mover para grupo**. **Sem grupo** remove a associação. O orçamento mantém a mesma identificação e os próprios custos.

## Placas e custos

A aba **Placas e custos** mostra miniaturas disponíveis no projeto, peso, tempo, material, energia, máquina, tempo extra, outros custos, mão de obra e mínimo rateado por placa. Role horizontalmente para ver todas as colunas.

Os valores são do arquivo inteiro. O mínimo rateado soma o mínimo por unidade multiplicado pelas unidades presentes no arquivo; não representa venda independente de cada placa. Mão de obra por projeto é rateada pelo tempo. Filamento extra e outros custos são rateados pelo material; a mão de obra por placa aplica o valor configurado a cada placa. O arredondamento final do rateio mantém a soma exata. O PDF interno inclui esse detalhamento.

## Projeto 3D e ajustes de impressão

Na aba **Projeto 3D**, escolha todas as placas ou uma placa específica. Se o arquivo tiver sido alterado desde a criação do orçamento, importe-o novamente para conferir a geometria e os custos juntos. Arraste para girar, use o botão direito para mover e a roda para ampliar. **Centralizar** restaura a vista. Cores são ilustrativas por objeto, não reproduzem a pintura do fatiador. Malhas grandes têm exibição simplificada; o fatiamento continua com a geometria completa.

**Ajustar e refatiar** permite marcar os parâmetros que deseja alterar: camada, preenchimento, paredes, suporte (mantendo o tipo configurado), tipo e largura do brim. Os valores marcados se aplicam a todas as placas e às sobreposições de objetos. Os demais parâmetros e as cores são mantidos. Escolha outro nome para salvar: o original não pode ser sobrescrito. O motor refaz o fatiamento e cria um novo orçamento com os custos e a organização do anterior.

Camada acima de 80% do menor bico é rejeitada. Projetos com perfil de camadas adaptativas devem ter esse perfil removido em um fatiador compatível antes de alterar a camada aqui. A versão não altera orientação, pintura, disposição ou suportes manualmente, nem prepara STL/OBJ sem configuração. Conferência digital não substitui uma impressão de teste.

## Avisos de atualização

Em **Custos e padrões**, use **Verificar atualizações**. Marque **Avisar sobre atualizações ao abrir** se desejar uma consulta automática ao iniciar (começa desligada). A consulta acessa somente a versão pública mais recente no GitHub; arquivos e orçamentos não são enviados. Quando existir versão mais nova, o programa oferece abrir a página oficial de download. Não baixa nem executa instaladores automaticamente. Sem internet, o uso local continua disponível.

## Custos combinados

- Filamento: R$ 100,00/kg.
- Energia: R$ 1,20/kWh e potência média estimada de 120 W para A1 Combo.
- Reserva para depreciação/manutenção da máquina: R$ 1,00/h.
- Cobrança extra pelo tempo: R$ 0,00/h inicialmente, em campo separado.
- Lucro: 100% sobre os custos de impressão.
- Mão de obra: R$ 20,00 pelo projeto inteiro (padrão dos novos), adicionados **depois** do lucro. Em Custos e padrões, pode escolher por placa. Orçamentos antigos mantêm por unidade para preservar os preços.
- Shopee: desligada em cada novo orçamento. Quando solicitada, o padrão é 20% do preço final mais uma taxa fixa ajustável entre R$ 2,00 e R$ 5,50 por unidade.

**Preço direto por unidade = custo de impressão por unidade × 2 + mão de obra calculada por unidade.** Na modalidade por projeto, divide os R$ 20 pelas unidades contidas no arquivo; por placa, multiplica pelo número de placas e divide pelas unidades.

**Preço Shopee = (preço direto + taxa fixa) ÷ (1 − percentual).**

O mínimo é arredondado para cima ao centavo. A sugestão é arredondada para cima ao real inteiro. O total do pedido usa o preço sugerido. A composição da tela usa o preço mínimo; o arredondamento da sugestão gera um pequeno lucro adicional.

O botão **Usar estes custos nos próximos arquivos** salva o padrão. Os orçamentos anteriores conservam os próprios custos, para manter a referência histórica. Se editar o custo de um orçamento anterior, ele será recalculado com a edição.

## Estimativas e despesas adicionais

Energia e reserva da máquina são hipóteses de orçamento, ajustáveis. Substitua a tarifa pela parcela variável com tributos e bandeira da conta de vocês e o consumo por uma medição do conjunto na tomada. R$ 1/h pode ser refinado com preço pago, horas produtivas esperadas e manutenção.

O material e o tempo vêm do fatiador. Purga inicial e calibração podem consumir material fora do total indicado. Use **Filamento extra do arquivo** para esse acréscimo e perdas. **Outros custos por unidade** pode incluir embalagem, acessórios, frete ou outras despesas. Esses campos começam em zero.

## Exportar e preservar os dados

### Sua empresa no PDF para cliente

Na aba **Empresa / PDF do cliente**, informe o nome da empresa e os contatos que deseja imprimir. Os campos opcionais são CPF/CNPJ, Instagram, telefone/WhatsApp, e-mail, site, endereço, cidade/estado, responsável, condições de pagamento, prazo de produção/entrega, observações e validade em dias. Zero dias deixa a validade sem informação.

Use **Escolher logo PNG / JPG** para selecionar uma imagem de até 10 MB e clique em **Salvar dados da empresa**. A logo é incorporada ao cadastro, permitindo mover a imagem original posteriormente. **Usar somente o nome** gera um PDF sem logo. Instagram aceita nome de usuário, @ ou endereço completo do perfil.

O nome da empresa é obrigatório para exportar o PDF para cliente; campos em branco são omitidos. Esse PDF utiliza somente a identidade cadastrada. O programa, PDF interno e HTML interno conservam a identidade da FG Design Print. Logo e contatos também são incluídos no backup JSON local. Não envie backups e orçamentos internos para o repositório público.

**Exportar orçamento** oferece quatro opções:

- **PDF para cliente**: logo, Instagram, imagem do projeto quando disponível, nome, identificação do orçamento, quantidade, preço por unidade e total. É a opção inicial.
- **PDF detalhado interno**: os dados de venda e a composição de custos, lucro, taxas, parâmetros e origem do cálculo. Use para conferência interna.
- **Relatório HTML interno**: composição completa para abrir no navegador.
- **Planilha CSV**: dados para planilha.

O PDF é gerado diretamente pelo programa, sem precisar instalar uma impressora PDF. A marca é incorporada ao arquivo. O PDF para cliente não inclui custos, lucro ou caminhos internos. O PDF detalhado, o HTML e o CSV contêm informações de custos; confira o formato escolhido antes de compartilhar.

## Miniaturas, edição e exclusão

Os orçamentos aparecem com a miniatura extraída do projeto. Quando o arquivo não contém uma imagem ou ela não está disponível, aparece o indicador 3D.

Clique com o botão direito sobre um orçamento para **Editar orçamento** ou **Excluir orçamento**. Os botões abaixo da lista oferecem as mesmas ações. Duplo clique ou F2 leva aos campos de edição; as alterações são salvas automaticamente. Com a lista em foco, Delete abre a confirmação de exclusão.

A exclusão remove somente o orçamento da lista. O arquivo 3MF/G-code e a imagem do projeto são preservados. **Desfazer exclusão** recupera o último orçamento removido com os custos e a miniatura, inclusive após fechar e reabrir o programa. Exclusões anteriores podem ser desfeitas sucessivamente.

## Atalho na área de trabalho

O instalador cria o atalho automaticamente. Se precisar recriá-lo, use **Atalho na área de trabalho** na barra superior. Na versão portátil, o atalho aponta para a pasta atual do executável; se mover essa pasta, recrie o atalho.

**Backup dos dados** salva os custos e o histórico em JSON. As miniaturas são armazenadas separadamente na pasta de dados. O histórico principal fica em:

`%LOCALAPPDATA%\FG Design Print\Precificador\orcamentos.json`

A última gravação anterior fica em `orcamentos.json.bak`. Para restaurar um backup, feche o programa, preserve uma cópia do arquivo atual e copie seu backup para essa pasta com o nome `orcamentos.json`. O caminho completo aparece na aba **Arquivo e mesas**. A desinstalação não apaga o histórico dessa pasta.

## Pesquisa inicial de energia — 17/11/2026

- [Cemig — tarifas vigentes](https://www.cemig.com.br/valores-e-tarifas/tarifas-vigentes/): tarifa B1 convencional com bandeira verde de R$ 0,90329/kWh **antes dos impostos** na consulta. R$ 1,20/kWh é uma hipótese para orçamento, não uma tarifa confirmada da fatura.
- [Fórum Bambu](https://forum.bambulab.com/t/power-consumption-for-a1-printer/92095/14): usuário reproduz referência de 95 W médios para A1 imprimindo PLA a 220 V. A consulta direta ao wiki não ficou disponível. Os 120 W adotados para A1 Combo são uma hipótese própria com folga; não uma medição do equipamento do usuário.
- Taxas Shopee são os parâmetros fornecidos pelo usuário, não uma consulta à política vigente da plataforma.

## Reprodução e verificação

O código-fonte do FG é disponibilizado no GitHub sob AGPL-3.0. O código correspondente do motor BambuStudio é disponibilizado como download separado na mesma versão, além de sua origem oficial. Projeto C# / .NET 10, Windows Forms x64. A exportação PDF usa PDFsharp-GDI 6.2.4 (MIT). Requer SDK .NET 10 para recompilar o FG e WiX 4 para preparar a distribuição com motor. As fontes e as instruções de compilação do motor estão em MOTOR.md. As licenças acompanham a distribuição.

```powershell
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publicado
```

O executável aceita arquivos 3MF/G-code como argumentos. Para executar o autoteste, informe um arquivo de saída em pasta existente:

```powershell
.\FG.Precificador.exe --self-test resultado.json
```

O ensaio usa dados fictícios e pasta própria. Verifica cálculos, taxas, lotes, preservação de originais, múltiplas mesas, rejeição de fatiamento parcial, gravação do histórico, PDFs e exclusão/recuperação após reabertura. Os projetos reais foram conferidos separadamente por importação automática e comparados à base deste chat. Isso verifica o cálculo digital e não a impressão física.
