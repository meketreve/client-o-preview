# STATUS — client-o-preview

> Single source of truth for resuming work. Read this FIRST when starting a session.
> Update this file at the end of every work phase so the next `/clear` resumes in 1 read.
> Last updated: 2026-08-02

---

## ✅ Done

### Region Focus (crop de região da janela monitorada)
- `RegionPreset` / `RegionSettings` em `csharp/Models/Settings.cs` — crop normalizado (0–1) + presets nomeados + assignments por stream.
- Persistência do bloco `regions` em `csharp/Services/SettingsService.cs` (`%APPDATA%/client-o-preview/settings.json`).
- `StreamWindow` aplica o crop via `rcSource` do DWM, com letterbox opcional (`LockAspect`), badge `▣ nome` na barra de título, botão `▣` e `FitToRegion()`.
- `RegionPickerWindow` + `RegionOverlayWindow`: seletor com thumbnail ao vivo, retângulo arrastável/redimensionável, 9 quick anchors, slider de tamanho e preview "Result" do recorte ao vivo.
- `Views/RegionPage` + nav "Region Focus" no `MainWindow`: aplicar/limpar/apagar presets nas previews abertas.
- Região reaplicada automaticamente ao reabrir previews (`OpenStreamForItem`).
- README: seção 4 "Foco de Região" (numeração das seções seguintes corrigida).
- Build validado: `dotnet build` → **Build succeeded**, 0 warnings.

### Versão / release
- `<Version>0.6.0</Version>` no `ClientOPreview.csproj` virou **fonte única**; `Views/AboutPage.xaml.cs` lê do assembly (antes era texto chumbado no XAML e já estava defasado em 0.5.0). Bumpar = editar 1 linha do csproj.
- Tag `v0.6.0` + release publicada com o `.exe`: https://github.com/meketreve/client-o-preview/releases/tag/v0.6.0
- Merge feito em `main` (`2338678`), commit da versão `03916ec`.

### Infra do repo
- `.wolf/`, `.claude/` e `CLAUDE.md` versionados. Fora do git: `.wolf/dashboard-token` (credencial do dashboard local), `.claude/settings.local.json`, `.wolf/backups/` e o estado de runtime (`.wolf/hooks/_session.json`, `.wolf/token-ledger.json`) que era reescrito a cada sessão.
- `.wolf/config.json`: `bin`, `obj`, `publish` adicionados aos `exclude_patterns` — sem isso o scan indexava 181 arquivos de build em vez de 44 de código.

---

## 🚀 Next phase

**Goal:** _colher o feedback do teste real da v0.6.0 (o usuário e o Epic Suicide vão usar in-game) e corrigir o que aparecer._

### Acceptance criteria
1. Recorte do painel de drones estável ao mover/redimensionar a preview.
2. Preset por piloto reaplicado certo quando 2+ clientes têm o mesmo título (occurrence index).
3. Overlay do seletor alinhado ao thumbnail em monitor com DPI diferente do primário.

### Files to create / edit
| Type | File | Content |
|---|---|---|
| edit | `RegionPickerWindow.xaml.cs` | `SyncOverlay()` usa o DPI da janela do picker; se desalinhar em setup multi-DPI, usar o DPI do monitor de destino |

### Closed decisions
- Crop implementado com `DWM_TNP_RECTSOURCE` (não captura de tela) — mantém o "sem interação com o processo" prometido no README.
- Seleção desenhada em janela transparente separada (`RegionOverlayWindow`) porque o DWM compõe o thumbnail **acima** do conteúdo da janela host.
- Coordenadas normalizadas (0–1) em vez de pixels — sobrevive a mudança de resolução do cliente.
- Picker é **não-modal** e `Topmost`: o usuário precisa clicar no jogo para abrir o painel que vai recortar.

### Open decisions
- Hotkey para alternar região ↔ janela inteira?
- Presets globais por jogo (perfil) além do preset por piloto?
- `.wolf/_scan-state.json` e `.wolf/cron-state.json` também mudam a cada scan/heartbeat — ignorar também?

---

## 📁 Active architecture

- **Stack:** C# / .NET 8 / WPF (`net8.0-windows`, `UseWPF` + `UseWindowsForms`), DWM Thumbnails API.
- **Key modules:** `MainWindow` (orquestra streams/hotkeys/settings), `StreamWindow` (1 preview = 1 thumbnail DWM), `Views/*Page` (UserControls de config), `csharp/Services/SettingsService` (JSON em `%APPDATA%`), `Native/NativeMethods` (P/Invoke).
- **Patterns:** páginas expõem `event` + `LoadFrom(...)`; `MainWindow` faz o wiring e salva settings; nenhuma page toca em settings direto.

---

## ⚠️ External blockers (don't block coding)

- Build/execução real só no Windows (WPF). No WSL há SDK em `~/.dotnet/dotnet` que compila com `EnableWindowsTargeting`.

---

## 🔧 Useful commands

```bash
~/.dotnet/dotnet build                      # compila no WSL (validação de sintaxe/tipos)
~/.dotnet/dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
git checkout -- obj/                        # SEMPRE após build no WSL: obj/ é versionado e recebe paths Linux
dotnet run                                  # dev, no Windows
gh release create vX.Y.Z <exe> --title ... --notes ...
```

---

## 📚 References (read IF needed)

- `.wolf/cerebrum.md` — User Preferences + Do-Not-Repeat + Decision Log
- `.wolf/anatomy.md` — token-efficient file index
- `.wolf/buglog.json` — known bugs + fixes
