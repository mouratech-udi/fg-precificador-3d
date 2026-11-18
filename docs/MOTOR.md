# Motor de fatiamento integrado

O FG Precificador distribui os componentes de fatiamento do **BambuStudio 02.08.02.60** sem alterações nos binários. Não é um produto oficial da Bambu Lab. A interface e o precificador são desenvolvidos pela FG Design Print.

## Funcionamento

O programa chama o motor local em segundo plano, com os parâmetros do projeto 3MF. Peso e tempo totais são obtidos do resultado do fatiamento. As configurações da cópia de entrada (inclusive as alterações explicitamente escolhidas pelo usuário) são comparadas à exportação do motor; alterações reais interrompem o cálculo. A identificação da versão do Studio pode ser atualizada sem mudar os parâmetros de impressão.

O motor funciona na pasta `Engine`, com bibliotecas de execução locais e uma pasta temporária própria por análise. O aplicativo não depende de uma instalação do Bambu Studio, de perfis particulares nem de conta Bambu. Não inclui o plugin opcional de rede e não envia G-code para a impressora.

Esta etapa trabalha com **projetos 3MF já configurados**. O precificador permite ajustar camada, preenchimento, paredes, ativação de suporte e brim, salvando outra cópia e refatiando-a. Orientação, disposição, pintura e edição manual de suportes continuam em um fatiador compatível. STL/OBJ devem ser configurados e salvos como projeto em um fatiador compatível antes da importação. O resultado é uma estimativa de produção, não uma validação física da peça.

## Origem e código correspondente

- Binários oficiais: https://github.com/bambulab/BambuStudio/releases/tag/v02.08.02.60
- Código exato e scripts de compilação: https://github.com/bambulab/BambuStudio/tree/v02.08.02.60
- Arquivo de fontes: https://github.com/bambulab/BambuStudio/archive/refs/tags/v02.08.02.60.zip
- Cópia do arquivo de fontes também é disponibilizada nos downloads da versão do FG que inclui este motor: https://github.com/mouratech-udi/fg-precificador-3d/releases
- Instruções de compilação: https://github.com/bambulab/BambuStudio/wiki/Windows-Compile-Guide

O arquivo de fontes contém o código e os scripts de obtenção/compilação das dependências. Suas versões e URLs constam no diretório `deps` do código exato acima. Os scripts e patches originais são preservados. Dependências incluem Open CASCADE, FFmpeg, FreeType, GMP e MPFR, além dos componentes estáticos identificados nas fontes. Nenhum binário do motor foi modificado nesta distribuição; apenas o conjunto de arquivos distribuídos foi reduzido aos binários raiz e perfis necessários à execução local.

O download dos binários, fontes e runtime é fixado por SHA-256 em `scripts/engine.lock.json`. O empacotamento reproduzível está em `scripts/Preparar-Motor.ps1`. As bibliotecas Visual C++ são extraídas do redistribuível oficial da Microsoft, com seu contrato em `Engine/Licenses/Microsoft-Visual-C-Runtime.rtf`. Bibliotecas de sistema do Windows não são substituídas.

## Licenças

BambuStudio, PrusaSlicer e Slic3r são disponibilizados sob **GNU Affero General Public License, versão 3**. A licença integral e os avisos de componentes constam em `Engine/Licenses`, nos fontes correspondentes e em `THIRD-PARTY-NOTICES`. O código do FG Precificador desta distribuição é disponibilizado sob AGPL-3.0. A licença não oferece garantia; consulte o texto integral em `LICENSE`.

Os componentes de terceiros conservam seus termos próprios. Marcas identificam seus respectivos autores. O código correspondente e a mesma licença estão disponíveis para quem recebe o programa; não há bloqueio de substituição do motor. Os arquivos necessários devem permanecer compatíveis com a integração e os resultados devem ser conferidos novamente após uma alteração.

## Visualização 3D

O leitor local considera malhas, componentes e transformações conforme a [especificação 3MF Core](https://github.com/3MFConsortium/spec_core/blob/master/3MF%20Core%20Specification.md). A associação de objetos e instâncias às placas usa Metadata/model_settings.config do projeto Bambu. As cores são ilustrativas; o projeto original conserva os dados de pintura. A simplificação ocorre somente na tela.
