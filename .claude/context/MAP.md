---
updated: 2026-10-03
tier: 2
---

# Mapa do projeto

<!-- TETO: 60 linhas FORA do bloco auto (o bloco é do script e não conta). O `git log` guarda o resto. -->

## Comandos

App só roda no Windows (WPF); no WSL compila e testa (`EnableWindowsTargeting`, testes em `net8.0`).

- `~/.dotnet/dotnet build` — compila no WSL (Windows: `dotnet build`)
- `~/.dotnet/dotnet test tests/ClientOPreview.Tests` — xUnit, 39 testes, roda no WSL
- `dotnet run` — executar, só no Windows (pede UAC: `requireAdministrator`)
- `build.bat` / `dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false` — `.exe` em `bin/Release/net8.0-windows/win-x64/publish/`
- Release: `<Version>` no `ClientOPreview.csproj` → tag `vX.Y.Z` → `gh release create vX.Y.Z <exe>`

Primeiro nível (auto):

<!-- auto:start -->
gerado em: 2026-10-03

primeiro nível:

```
app.manifest
App.xaml
App.xaml.cs
build.bat
CLAUDE.md
ClientOPreview.csproj
csharp
Localization
MainWindow.xaml
MainWindow.xaml.cs
Models
Native
README.md
RegionOverlayWindow.xaml
RegionOverlayWindow.xaml.cs
RegionPickerWindow.xaml
RegionPickerWindow.xaml.cs
Services
StreamWindow.xaml
StreamWindow.xaml.cs
tests
TrayHelper.cs
Views
WARP.md
```
<!-- auto:end -->

## Onde fica cada coisa

- raiz — janelas: `MainWindow` (só wiring, `WirePages()`), `StreamWindow` (uma por preview), `RegionPickerWindow`/`RegionOverlayWindow` (seletor de recorte), `TrayHelper`
- `Views/` — uma página por aba da sidebar; expõe `event` + `LoadFrom(...)`, nunca toca em settings
- `Models/Settings.cs` — `SettingsData`; **o model é o formato do JSON** (snake_case automático)
- `Services/StreamManager.cs` — abrir/fechar preview, foco (`Focus` + `AttachThreadInput`), topmost, ordem do ciclo (`_order`)
- `Services/HotkeyManager.cs` — `RegisterHotKey`/`WM_HOTKEY`; `FailedCombos` alimenta o aviso da aba Hotkeys
- `Services/RegionCoordinator.cs` — qual preview usa qual recorte (mapa por HWND)
- `Services/LayoutStore.cs` + `LayoutKey.cs` — onde a preview reabre; chave `title:occurrence`
- `Services/ThumbnailGeometry.cs` — região/zoom → `rcSource`, letterbox → `rcDestination` (puro, testado)
- `Services/SettingsService.cs` — `%APPDATA%/client-o-preview/settings.json` (atômico + `.bak`); `AppLog.cs` → `error.log` lá
- `Native/NativeMethods.cs` — todo P/Invoke (`user32`, `dwmapi`)
- `Localization/Loc.cs` — string table en + pt-BR (sem WPF); `TrExtension.cs` = `{loc:Tr Chave}`
- `tests/ClientOPreview.Tests/` — *source link* dos arquivos sem WPF (lista no `.csproj` de teste)
- `.wolf/` — memória do OpenWolf (STATUS, cerebrum, anatomy, buglog); detalhe histórico mora lá
- Lixo local não versionado na raiz: `csharp/` (vazia), `bin/`, `obj/`, `__pycache__/`, `error.log`

## Pontos de entrada e fluxos principais

- Boot: `App.xaml.cs` → `MainWindow` ctor: lê settings → resolve idioma (antes do `InitializeComponent`) → cria páginas e os 4 colaboradores → wiring → timer de foreground 400 ms
- Preview: `ClientsPage` → `StreamManager` abre `StreamWindow` → `DwmRegisterThumbnail` → `UpdateThumbnailRect()` aplica `ThumbnailGeometry`
- Config: página emite `event` → `MainWindow` aplica nas streams → `SettingsService.Save`
- Mudar X → mexer em: nova config = `Models/Settings.cs` + página + 1 linha em `WirePages()`; texto novo = as DUAS tabelas do `Loc.cs` (`LocTests` reprova paridade)
