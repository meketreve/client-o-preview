# client-o-preview

Aplicativo Windows para pré-visualizar janelas em miniaturas ao vivo. Feito com IA:
- C# (WPF + DWM Thumbnails) neste diretório

##  Build C# (WPF)

- Executar (dev)
  ```powershell
  dotnet run
  ```
- Build (Release / publish single-file)
  ```powershell
  dotnet clean
  dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
  ```
  Saída: `bin/Release/net8.0-windows/win-x64/publish/ClientOPreview.exe`

## Release
  se não quiser fazer a compilação, pode baixar o executável aqui (https://github.com/meketreve/client-o-preview/releases) .


## 🚀 Como Usar

O **client-o-preview** é dividido em categorias na barra lateral. Abaixo está um guia detalhado de cada funcionalidade:

### 1. Selecionando Janelas (Active Clients)
*   Vá na aba **Active Clients**.
*   Clique em **Refresh** para listar todas as janelas abertas no seu Windows.
*   Marque as janelas que deseja monitorar.
*   Clique em **Open Selected Streams**. As miniaturas aparecerão na tela.

### 2. Interagindo com as Miniaturas
*   **Clique Esquerdo**: Foca e traz para frente a janela real correspondente.
*   **Botão Direito (Segurar)**: Permite arrastar a miniatura para qualquer lugar da tela.
*   **Barra de Título**: Clique e arraste para mover a miniatura individualmente.

### 3. Sistema de Zoom (Novo!)
Aba **Zoom** permite configurar como as miniaturas reagem ao mouse:
*   **Resize window on hover**: Aumenta o tamanho físico da miniatura quando você passa o mouse.
*   **Internal zoom (Modo Lupa)**: Amplia o conteúdo sem mudar o tamanho da janela (ideal para economizar espaço).
*   **Magnification Factor**: Ajusta o nível de zoom (ex: 1.5x, 2.0x).
*   **Centering X/Y**: Define o "foco" do zoom. Útil para centralizar em mini-mapas ou áreas específicas da interface do jogo/aplicativo.

### 4. Foco de Região (Region Focus — Novo!)
Mostra **só um pedaço** da janela monitorada (painel de drones, capacitor, minimapa) em vez da janela inteira.

*   Abra a aba **Region Focus** (ou clique no botão **▣** na barra de título de qualquer miniatura).
*   O seletor abre com o preview **ao vivo** da janela escolhida:
    *   **Arraste** sobre o preview para desenhar a área.
    *   **Arraste dentro** do retângulo para mover, use as **alças** nos cantos/bordas para redimensionar.
    *   **Quick anchors**: 9 botões (cantos, laterais e centro) para posicionar rápido; o slider **Anchor size** muda o tamanho do recorte.
    *   O quadro **Result** mostra, ao vivo, exatamente como a miniatura vai ficar.
*   Dê um **nome ao preset** (ex.: o nome do piloto) e clique em **Save**.
*   Opções:
    *   **Keep crop proportions**: mantém o formato original do recorte (sem esticar a imagem).
    *   **Resize preview to the crop**: ajusta a altura da miniatura ao formato da região.
O fluxo recomendado na aba **Region Focus** é em dois passos:

1.  **Crie a região uma vez** — selecione uma preview para usar como tela de exemplo e clique em **Novo preset…**. Presets salvos são **imutáveis**: se você digitar um nome que já existe, o app recusa em vez de sobrescrever.
2.  **Aplique nas outras contas** — selecione a preview e escolha o preset salvo no combo. Também dá para limpar a região (**Limpar região**), editar a região daquela preview (**Editar esta região…**) ou apagar presets antigos.

A região é salva em coordenadas relativas (0–1), então continua correta mesmo se o cliente mudar de resolução. A associação preset ↔ miniatura é lembrada por título + ordem de abertura no `settings.json` e, enquanto a preview estiver aberta, pela própria janela — assim a seleção não "escapa" quando o título do cliente muda (tela de login → nome do piloto). O zoom por hover continua funcionando **dentro** da região escolhida.

### 5. Atalhos de Teclado (Hotkeys)
Na aba **Hotkeys**, você pode configurar uma tecla para alternar entre as janelas abertas:
*   **Cycle Hotkey**: Escolha uma combinação (ex: `Alt + Tab` ou uma tecla única como `F1`).
*   **Device Filter**: O sistema detecta automaticamente seu teclado. Isso garante que a hotkey funcione apenas no dispositivo desejado.
*   Ao pressionar a hotkey, o app trará a próxima janela da lista para o primeiro plano.

### 6. Miniatura (Thumbnail)
A antiga aba **General** foi incorporada aqui — todas as opções de preview ficam em um lugar só.

*   **Previews sempre no topo**: Mantém as miniaturas sempre visíveis sobre outras janelas.
*   **Só no topo enquanto um cliente estiver em foco**: quando você sai para o navegador, Discord ou qualquer outra janela, as previews descem para trás; ao voltar para um cliente (ou para o próprio app), elas sobem de novo.
*   **Opacidade**, **Tamanho do título**, **Cor ativa**, **Largura/Altura** padrão das novas miniaturas.
*   **Minimizar para a bandeja**: Ao fechar o menu principal, o app continua rodando perto do relógio do Windows.
*   **Lembrar a posição das previews** e **Salvar a posição de cada janela separadamente**.
*   **Alinhar à grade ao mover** + **Tamanho da grade**.

### 7. Idioma (Language)
Aba **Idioma**: alterna a interface entre **Português (Brasil)** e **English**. A troca é aplicada na hora, sem reiniciar, e fica salva no `settings.json`. Na primeira execução o idioma segue o do Windows.

---

## 💾 Persistência
Todas as suas preferências, posições de janelas e hotkeys são salvas automaticamente em:
`%APPDATA%/client-o-preview/settings.json`

Se algo der errado, o motivo fica registrado em `%APPDATA%/client-o-preview/error.log`.

## 🐞 Problemas conhecidos (v0.8.0)
- **A hotkey de ciclar entre os clientes (Alt+Tab por padrão) não está funcionando.**
  As hotkeys diretas (Alt + NumPad) podem estar afetadas pelo mesmo motivo — a
  investigação está em aberto. Clicar na miniatura continua focando o cliente
  normalmente. Correção prevista para a próxima versão.

## 📺 Tutorial em Vídeo
Confira o funcionamento básico aqui: [YouTube - Como usar client-o-preview](https://youtu.be/sjbJxVLL4h4)

---

## ⚠️ Sobre
"This program does NOT modify game interface or broadcast inputs. It only shows live previews."
O programa apenas utiliza a API oficial do Windows (DWM) para exibir cópias visuais das janelas, sem qualquer interação com a memória dos processos monitorados.
