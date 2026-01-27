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


### Uso
- Sidebar: General, Thumbnail, Zoom, Overlay, Active Clients, About.
- Active Clients: Refresh → selecione janelas → Open streams.
- Clique no preview para focar a janela real; opções: “Minimize inactive” ou “Send inactive to back”.
- General: “Previews always on top”, “Minimize to System Tray”, “Track client locations”, “Hide preview of active client”, “Hide when not active”, “Unique layout for each client”.
- Thumbnail: ajuste Opacity/Width/Height (aplica nas janelas abertas).
- Configuração persiste em `%APPDATA%/client-o-preview/settings.json` .


## Sobre
- “This program does NOT modify game interface or broadcast inputs. It only shows live previews.”
