# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

> Contexto mais rico (mapa do código, dívida técnica, bugs conhecidos) vive em `.wolf/`.
> Comece por `.wolf/STATUS.md` e `.wolf/cerebrum.md`.

## Projeto

Aplicação única na raiz do repositório: **client-o-preview**, C# / .NET 8 / WPF (`net8.0-windows`,
`UseWPF` + `UseWindowsForms`), sem dependência NuGet. Usa a API de DWM Thumbnails para exibir
previews ao vivo de outras janelas sem oclusão e sem interagir com o processo monitorado.

Testes em `tests/ClientOPreview.Tests/` (`net8.0`): fazem *source link* dos arquivos livres de WPF,
então rodam em qualquer SO.

## Comandos

```powershell
dotnet build                # compila
dotnet run                  # executa (só Windows)
dotnet test                 # roda os testes
.\build.bat                 # clean + publish Release win-x64 single-file
```

No WSL: `~/.dotnet/dotnet build` compila graças a `EnableWindowsTargeting`; executar a app exige Windows.

## Estrutura

| Pasta | Conteúdo |
|---|---|
| raiz | `App`, `MainWindow`, `StreamWindow`, `RegionPickerWindow`, `RegionOverlayWindow`, `TrayHelper` |
| `Views/` | UserControls de configuração (uma página por aba da sidebar) |
| `Models/` | `SettingsData` e agregados; `WindowItem` |
| `Services/` | `SettingsService` (JSON), `StreamManager`, `HotkeyManager`, `LayoutStore`, `RegionCoordinator`, `WindowEnumerator`, helpers puros |
| `Native/` | todo o P/Invoke (`user32.dll`, `dwmapi.dll`) |
| `Localization/` | string table en / pt-BR + markup extension `{loc:Tr Chave}` |

## Arquitetura de alto nível

- **Preview:** `DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties` desenham a janela origem
  dentro de um `Window` WPF. O recorte de região e o zoom viram `rcSource`;
  o letterbox vira `rcDestination` (`Services/ThumbnailGeometry.cs`).
- **UI:** `MainWindow` só faz wiring — cada página expõe `event` + `LoadFrom(...)` e nunca toca em
  settings direto; a `MainWindow` aplica o efeito e manda salvar.
- **Estado:** `SettingsService` serializa `SettingsData` para
  `%APPDATA%/client-o-preview/settings.json` (snake_case).

## Notas

- Projeto específico de Windows.
- `UseWindowsForms` está ligado (por causa do `NotifyIcon`), então `Point`, `Rectangle`, `Color`,
  `MouseEventArgs` e `KeyEventArgs` ficam ambíguos: fixe o tipo WPF com `using X = System.Windows...;`
  no topo do arquivo.
