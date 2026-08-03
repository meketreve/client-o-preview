# Memory

> Chronological action log. Hooks and AI append to this file automatically.
> Old sessions are consolidated by the daemon weekly.

## 2026-08-02 — Region Focus

- Spec veio de conversa no Discord (Epic Suicide × meketreve): mostrar só um pedaço da janela do cliente (painel de drones / capacitor), com pontos pré-definidos (cantos + centro) **e** área ajustável, salva pelo nome do piloto.
- Criados: `RegionPickerWindow.xaml(.cs)`, `RegionOverlayWindow.xaml(.cs)`, `Views/RegionPage.xaml(.cs)`.
- Editados: `csharp/Models/Settings.cs` (`RegionPreset`, `RegionSettings`), `csharp/Services/SettingsService.cs` (bloco `regions`), `StreamWindow.xaml(.cs)` (crop via `rcSource`, letterbox, badge, botão ▣, `FitToRegion`), `MainWindow.xaml(.cs)` (nav + wiring + reaplicação ao abrir stream), `README.md`.
- Fix durante o desenvolvimento: aliases `using` por causa do WinForms habilitado; overlay passou a guardar o retângulo normalizado como fonte da verdade.
- `~/.dotnet/dotnet build` → Build succeeded, 0 warnings.

## 2026-08-02 — Release v0.6.0

- Merge de `feat/region-focus` em `main` (`2338678`), `.wolf/`+`.claude/`+`CLAUDE.md` versionados (token do dashboard e settings.local ignorados).
- Versão deixou de ser texto chumbado no `AboutPage.xaml`: `<Version>0.6.0</Version>` no csproj + leitura do assembly (`03916ec`). Antes mostrava 0.5.0 mesmo depois do commit "v0.5.1".
- Tag `v0.6.0` + release no GitHub com o `.exe` single-file (framework-dependent, precisa do .NET 8 Desktop Runtime).
- `openwolf update` (já estava em 2.0.1, hooks refeitos) + `bin`/`obj`/`publish` adicionados aos excludes do anatomy scan: 181 → 44 arquivos indexados.

## Session: 2026-08-03 00:15

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|
| 04:10 | i18n completo (pt-BR/en) com string table + markup extension `{loc:Tr Key}` | Localization/Loc.cs (novo) | 2 idiomas, troca em runtime sem restart | ~9k |
| 04:20 | Aba Idioma nova + nav | Views/LanguagePage.xaml(.cs), MainWindow.xaml(.cs) | rádio pt-BR/English, salva em settings.json `language` | ~2k |
| 04:35 | Aba General fundida na Thumbnail; duplicata "Previews always on top" removida | Views/ThumbnailPage.*, Views/GeneralPage.* (deletados) | 1 página só, 8 botões → 7 na sidebar | ~4k |
| 04:45 | Opção "só no topo enquanto um cliente estiver em foco" | Settings.cs, SettingsService.cs, MainWindow.xaml.cs | UpdateTopmostForFocus() no timer de 400ms | ~3k |
| 05:00 | Fix bug-001: preset de região que "não altera" | MainWindow.xaml.cs | assignment resolvido por HWND (_liveRegions) + fallback por título | ~3k |
| 05:10 | Fluxo "cria preset primeiro, depois só seleciona" + presets imutáveis | Views/RegionPage.*, RegionPickerWindow.* | botão Novo preset…, IsNameTaken recusa sobrescrita | ~3k |
| 05:20 | Build validado | — | Build succeeded, 0 warnings | ~1k |
| 05:40 | Build de teste publicada | ClientOPreview.csproj (0.6.0 → 0.7.0-dev) | `client-o-preview-0.7.0-dev.exe` (284 KB) em C:\Users\Meketreve\Downloads | ~1k |

### Resumo da sessão (2026-08-03)
Pacote de 4 pedidos do usuário, todos entregues e compilando (0 warnings):
1. **i18n pt-BR/en** — `Localization/Loc.cs` (string table + `{loc:Tr Key}`), troca em runtime.
2. **Aba Idioma** — `Views/LanguagePage.*`, persistida em `settings.json` → `"language"`.
3. **General fundida na Thumbnail** — `Views/GeneralPage.*` deletado, duplicata de topmost removida, + opção nova "só no topo enquanto um cliente estiver em foco".
4. **Região** — bug-001 (preset que não pegava) corrigido via `_liveRegions` por HWND; fluxo "cria preset uma vez → seleciona nas outras contas" + presets imutáveis.

**Estado:** build de teste `client-o-preview-0.7.0-dev.exe` na pasta Downloads do Windows, **ainda não executada** (WPF não roda no WSL). Nada commitado — 27 modificados, 3 novos (`Localization/`, `Views/LanguagePage.*`), 2 deletados (`Views/GeneralPage.*`).
**Próximo passo:** feedback do teste in-game → ajustar termos pt-BR → tirar o sufixo `-dev` do csproj → commit + release v0.7.0.
| 06:00 | Teste in-game aprovado pelo usuário → versão final | ClientOPreview.csproj (0.7.0-dev → 0.7.0) | publish refeito, 284 KB | ~1k |
| 06:15 | Commit + push + release v0.7.0 | branch feat/i18n-thumbnail-merge → main | ed79992 na main, tag v0.7.0 com ClientOPreview.exe (284 KB) | ~2k |
