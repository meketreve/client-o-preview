# anatomy.md

> Auto-maintained by OpenWolf. Last scanned: 2026-08-10T00:24:25.176Z
> Files: 57 tracked | Anatomy hits: 0 | Misses: 0

## ./

- `.gitattributes` — Git attributes (~18 tok)
- `.gitignore` — ignora bin/obj/publish, .wolf runtime, .pyc legado (~203 tok)
- `app.manifest` (~288 tok)
- `App.xaml` — resources vazios; o startup é o handler OnStartup (~72 tok)
- `App.xaml.cs` — handler global de exceção: loga via AppLog e mostra o caminho do error.log no MessageBox (~257 tok)
- `build.bat` — clean + publish Release win-x64 single-file framework-dependent. Só roda no Windows (~195 tok)
- `CLAUDE.md` — aponta para .wolf/OPENWOLF.md (~57 tok)
- `ClientOPreview.csproj` (~224 tok)
- `MainWindow.xaml` — sidebar de 7 botões + ContentHost (~369 tok)
- `MainWindow.xaml.cs` — Shell of the app: owns the settings, builds the collaborators and wires the settings pages to them. Every page exposes `event` + `LoadFrom(...)` an... (~2579 tok)
- `README.md` — Project documentation (~1730 tok)
- `RegionOverlayWindow.xaml` — camada transparente (AllowsTransparency, #01000000) sobre o thumbnail DWM do picker (~302 tok)
- `RegionOverlayWindow.xaml.cs` — retângulo de seleção: drag/move/resize por 8 alças, dim ao redor, ESC/ENTER. Fonte da verdade = Rect normalizado 0–1 (~2299 tok)
- `RegionPickerWindow.xaml` — janela do seletor: preview ao vivo, 9 quick anchors, slider de tamanho, preview "Result", nome do preset (~1221 tok)
- `RegionPickerWindow.xaml.cs` — 2 thumbnails DWM (full + crop), converte DIP↔px, IsNameTaken bloqueia sobrescrever preset salvo, emite RegionSaved (~2639 tok)
- `StreamWindow.xaml` — 1 preview: TitleBar (título + badge ▣ + botão ▣) sobre a área do thumbnail (~504 tok)
- `StreamWindow.xaml.cs` — 1 thumbnail DWM: chama ThumbnailGeometry para rcSource/letterbox, snap-to-grid, drag, zoom no hover, esconde do Alt+Tab (~2651 tok)
- `TrayHelper.cs` — NotifyIcon estático (WinForms): menu Abrir/Sair, balloon ao minimizar, reassina Loc.LanguageChanged (~475 tok)
- `WARP.md` — guia para o WARP: estrutura real de pastas, comandos, arquitetura (~605 tok)

## .claude/

- `settings.json` — hooks do OpenWolf + permissões (~514 tok)
- `settings.local.json` — permissões locais, fora do git (~28 tok)

## .claude/commands/

- `reframe.md` — Mode: migrate [framework] (~551 tok)
- `security-audit.md` — Layer 1 — Dependencies (~510 tok)

## .claude/rules/

- `openwolf.md` — regras de sessão (STATUS primeiro, anatomy antes de ler, buglog antes de corrigir) (~328 tok)

## Localization/

- `Loc.cs` — Raised after the language changed, for code-built strings that XAML cannot bind. (~3882 tok)
- `TrExtension.cs` — `{loc:Tr Chave}`: MarkupExtension que devolve Binding no indexer de Loc. Separado para Loc.cs não depender de WPF (~257 tok)

## Models/

- `Settings.cs` — SettingsData: Clone, UpsertPreset, RemovePreset (~1226 tok)
- `WindowItem.cs` — HWnd + Title + Display de uma janela listável (~63 tok)

## Native/

- `NativeMethods.cs` — Class: NativeMethods (~1334 tok)

## Services/

- `AppLog.cs` — logger de arquivo em %APPDATA%/client-o-preview/error.log, com corte em 256 KB. Substituiu os catch vazios (~592 tok)
- `HotkeyManager.cs` — Owns the global hotkeys: registration with Windows, the WM_HOTKEY hook and the translation from the strings stored in settings to virtual key codes... (~1746 tok)
- `LayoutKey.cs` — PURO: sanitiza título, monta `title:occ:titulo`, formata/parseia `WxH+L+T`. Sem WPF nem Win32 → testado (~758 tok)
- `LayoutStore.cs` — onde cada preview reabre: aplica geometria salva (ou escalona), migra chave legada, lembra as previews abertas (~854 tok)
- `RegionCoordinator.cs` — qual recorte cada preview mostra. Mapa por HWND vence a chave por título (bug-001). Abre o picker, apaga preset, monta as linhas da página (~1291 tok)
- `SettingsService.cs` — JsonSerializer + SnakeCaseLower ⇒ o model É o formato. Escrita atômica (.tmp→move), .bak quando o JSON está corrompido (~1238 tok)
- `StreamManager.cs` — Owns the open previews: one <see cref="StreamWindow"/> per monitored client. Also runs the foreground poll that highlights the active client, reaps... (~3343 tok)
- `ThumbnailGeometry.cs` — PURO: região+zoom → rcSource, letterbox → rcDestination. Sem WPF nem Win32 → testado (~720 tok)
- `WindowEnumerator.cs` — EnumWindows filtrando (visível, não minimizada, sem owner, com título) + GetTitle(hwnd) (~437 tok)

## Views/

- `AboutPage.xaml` — versão + créditos (~143 tok)
- `AboutPage.xaml.cs` — lê a versão do AssemblyInformationalVersion (nunca desvia do csproj) (~233 tok)
- `ClientsPage.xaml` — lista de janelas + 4 botões (~362 tok)
- `ClientsPage.xaml.cs` — só eventos: Refresh/Open/CloseSelected/CloseAll + SelectedWindows (~323 tok)
- `HotkeysPage.xaml` (~2071 tok)
- `HotkeysPage.xaml.cs` — Window handle of the client, the only thing that tells two same-title clients apart. (~3785 tok)
- `LanguagePage.xaml` — rádio pt-BR / English (~276 tok)
- `LanguagePage.xaml.cs` — LoadFrom + evento LanguageSelected com o code na Tag do rádio (~244 tok)
- `RegionPage.xaml` — 2 listas (previews / presets) + combo de atribuição + Novo preset…/Definir/Limpar/Apagar (~751 tok)
- `RegionPage.xaml.cs` — StreamEntry/PresetEntry; guarda as listas para reconstruir no LanguageChanged; flag _loading evita eco de SelectionChanged (~1331 tok)
- `ThumbnailPage.xaml` — seções "Previews" e "Geral" (absorveu a antiga aba General) (~1306 tok)
- `ThumbnailPage.xaml.cs` — record ThumbnailArgs no Apply + 7 eventos booleanos; a MainWindow grava no model (~1105 tok)
- `ZoomPage.xaml` — resize on hover / zoom interno / magnificação / offsets (~628 tok)
- `ZoomPage.xaml.cs` — muta o objeto Zoom compartilhado in-place e emite ZoomChanged (~331 tok)

## tests/ClientOPreview.Tests/

- `ClientOPreview.Tests.csproj` — xUnit em net8.0; faz source link dos arquivos sem WPF ⇒ `dotnet test` roda no WSL também (~397 tok)
- `LayoutKeyTests.cs` — chave por occurrence, chave legada, round-trip de `WxH+L+T`, rejeição de lixo (~478 tok)
- `LocTests.cs` — paridade de chaves en↔pt-BR, sem tradução vazia, sem ponto na chave, placeholders iguais, Normalize (~789 tok)
- `SettingsRoundTripTests.cs` — todo campo da UI sobrevive ao restart (guarda do bug-002), nomes snake_case, settings.json da v0.7.0, arquivo corrompido → .bak (~2160 tok)
- `ThumbnailGeometryTests.cs` — recorte normalizado→pixels, zoom dentro da região, clamp na borda, letterbox centralizado (~947 tok)
