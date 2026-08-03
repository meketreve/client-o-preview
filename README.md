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
*   Na aba **Region Focus** você reaplica qualquer preset salvo em qualquer miniatura pelo combo, limpa a região (**Clear region**) ou apaga presets antigos.

A região é salva em coordenadas relativas (0–1), então continua correta mesmo se o cliente mudar de resolução. A associação preset ↔ miniatura é lembrada por título + ordem de abertura, e o zoom por hover continua funcionando **dentro** da região escolhida.

### 5. Atalhos de Teclado (Hotkeys)
Na aba **Hotkeys**, você pode configurar uma tecla para alternar entre as janelas abertas:
*   **Cycle Hotkey**: Escolha uma combinação (ex: `Alt + Tab` ou uma tecla única como `F1`).
*   **Device Filter**: O sistema detecta automaticamente seu teclado. Isso garante que a hotkey funcione apenas no dispositivo desejado.
*   Ao pressionar a hotkey, o app trará a próxima janela da lista para o primeiro plano.

### 6. Configurações Gerais (General)
*   **Previews always on top**: Mantém as miniaturas sempre visíveis sobre outras janelas.
*   **Minimize to System Tray**: Ao fechar o menu principal, o app continua rodando perto do relógio do Windows.
*   **Unique layout**: Salva a posição de cada miniatura individualmente por título de janela.

### 7. Personalização Visual (Thumbnail)
*   Ajuste a **Opacidade** para deixar os previews semitransparentes.
*   Defina a **Largura/Altura** padrão para todas as novas miniaturas.

---

## 💾 Persistência
Todas as suas preferências, posições de janelas e hotkeys são salvas automaticamente em:
`%APPDATA%/client-o-preview/settings.json`

## 📺 Tutorial em Vídeo
Confira o funcionamento básico aqui: [YouTube - Como usar client-o-preview](https://youtu.be/sjbJxVLL4h4)

---

## ⚠️ Sobre
"This program does NOT modify game interface or broadcast inputs. It only shows live previews."
O programa apenas utiliza a API oficial do Windows (DWM) para exibir cópias visuais das janelas, sem qualquer interação com a memória dos processos monitorados.
