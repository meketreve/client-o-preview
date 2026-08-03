# STATUS — client-o-preview

> Single source of truth for resuming work. Read this FIRST when starting a session.
> Update this file at the end of every work phase so the next `/clear` resumes in 1 read.
> Last updated: 2026-08-03

---

## ✅ Done

### i18n pt-BR / en (2026-08-03)
- `Localization/Loc.cs`: string table (~110 chaves) + `{loc:Tr Key}` (MarkupExtension → Binding no indexer). Troca de idioma **em runtime**, sem restart.
- Aba **Idioma** nova (`Views/LanguagePage.*`), rádio Português (Brasil) / English; persistido em `settings.json` → `"language"`. Primeira execução segue o idioma do Windows (`Loc.SystemDefault()`).
- Todo texto visível traduzido: sidebar, 7 páginas, StreamWindow (tooltip ▣), RegionPickerWindow, menu da bandeja, MessageBox de erro do App.
- Strings montadas em código seguem `Loc.LanguageChanged` (labels dos hotkeys, "(None)", combo "— nenhuma —").

### Aba General fundida na Thumbnail (2026-08-03)
- `Views/GeneralPage.*` **deletado**; tudo migrou para `ThumbnailPage` (seções "Previews" e "Geral"). Duplicata de "Previews always on top" removida — sobrou uma só.
- Sidebar: 7 botões (Miniatura, Atalhos, Zoom, Foco de Região, Clientes Ativos, Idioma, Sobre).
- Novo: **"Só no topo enquanto um cliente estiver em foco"** (`General.TopmostOnlyWhenClientFocused`, JSON `topmost_only_when_client_focused`). Implementado em `UpdateTopmostForFocus()`, dentro do timer de foreground de 400ms que já existia.

### Release v0.7.0 (2026-08-03)
- Build testada in-game pelo usuário e aprovada.
- `<Version>` 0.7.0 no csproj (sem sufixo), tag `v0.7.0` + release com o `.exe` single-file.

### Region Focus — confiabilidade + fluxo de preset (2026-08-03)
- **bug-001 corrigido**: seleção de preset que "às vezes não altera". Assignment agora resolvido por HWND (`_liveRegions`) enquanto a preview vive; chave por título só como persistência/fallback (o título do cliente muda em runtime).
- Fluxo em 2 passos na página: **1. Novo preset…** (desenha uma vez numa preview de exemplo) → **2. escolhe o preset salvo** em cada outra conta.
- Presets **imutáveis**: o picker recusa salvar por cima de um nome já existente (`IsNameTaken`), exceto ao editar aquele mesmo preset.
- README atualizado (seções 4, 6 e 7).

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

**Goal:** _colher o uso real da v0.7.0 (idioma + topmost por foco + fluxo novo de preset de região) e decidir o próximo recurso._

### Acceptance criteria
1. Nenhum texto sobrando em inglês com o app em pt-BR (e vice-versa) — inclusive telas que só aparecem em erro.
2. Topmost por foco sem "piscar" quando o usuário alterna rápido entre clientes.
3. Preset de região continua pegando depois de horas com vários clientes abrindo/fechando.

### Files to create / edit
| Type | File | Content |
|---|---|---|
| edit | `Localization/Loc.cs` | ajustar termos pt-BR que soarem estranhos in-game; novas chaves seguem o padrão PascalCase sem ponto |

### Closed decisions
- String table em C# (não `.resx`): 2 idiomas, app single-file, troca em runtime sem build extra.
- `General` continua existindo no model/JSON mesmo sem a aba — não quebra `settings.json` de quem já usa.
- Topmost por foco reaproveita o timer de 400ms; sem hook global novo.
- Assignment de região resolvido por HWND em sessão, título só como persistência.

### Open decisions
- Terceiro idioma (es?) — a string table aguenta, é só mais um dicionário + rádio.
- Hotkey para alternar região ↔ janela inteira?
- Presets globais por jogo (perfil) além do preset por piloto?
- `.wolf/_scan-state.json` e `.wolf/cron-state.json` também mudam a cada scan/heartbeat — ignorar também?

---

## 📁 Active architecture

- **Stack:** C# / .NET 8 / WPF (`net8.0-windows`, `UseWPF` + `UseWindowsForms`), DWM Thumbnails API.
- **Key modules:** `MainWindow` (orquestra streams/hotkeys/settings/idioma), `StreamWindow` (1 preview = 1 thumbnail DWM), `Views/*Page` (UserControls de config), `csharp/Services/SettingsService` (JSON em `%APPDATA%`), `Native/NativeMethods` (P/Invoke), `Localization/Loc.cs` (string table pt-BR/en + `{loc:Tr}`).
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
