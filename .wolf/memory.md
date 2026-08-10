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

## Session: 2026-08-09 21:33

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|

## Session: 2026-08-08 — Auditoria de manutenibilidade (vibecode)

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|
| — | `openwolf scan` (anatomy estava stale desde o merge) | .wolf/anatomy-index.json | 45 arquivos indexados em 63 ms | ~1k |
| — | Leitura integral do código-fonte | 20 arquivos .cs/.xaml + csproj/gitignore/build.bat/WARP.md | mapa completo do fluxo App→MainWindow→StreamWindow→DWM | ~35k |
| — | Checagem de paridade i18n por script | Localization/Loc.cs | 94 chaves em cada tabela, 0 faltando, 0 duplicada; `RegionOfSelected` órfã | ~1k |
| — | anatomy.md reescrito com descrições reais | .wolf/anatomy.md | cada arquivo diz o que faz e onde dói (god object, código morto, doc stale) | ~3k |
| — | cerebrum: Mapa do código + 11 learnings + 3 decisões + 3 do-not-repeat | .wolf/cerebrum.md | próxima sessão sabe "quero mexer em X → abro Y" sem reler nada | ~3k |
| — | 4 bugs novos logados com root cause + fix | .wolf/buglog.json | bug-002 (miniatura não persiste), 003 (occurrence index), 004 (error.log no CWD), 005 (catch vazio) | ~2k |
| — | STATUS: quest de refactor em 5 fases | .wolf/STATUS.md | Fase 0 limpeza → 1 bugs → 2 SettingsService → 3 quebrar MainWindow → 4 testes | ~3k |

### Resumo da sessão (2026-08-08)
Pedido: analisar o projeto todo e popular o OpenWolf, mirando manutenção + edição por vibecode. Nenhum código de app alterado — só a base de conhecimento.

**Achados principais:**
1. `MainWindow.xaml.cs` com 836 linhas / ~7,6k tokens acumula streams + hotkeys + layout + região + foreground + wiring. Toda tarefa paga esse arquivo inteiro.
2. Estado duplicado (8 campos-espelho de settings) → **bug-002**: largura/altura/opacidade/fonte da miniatura não persistem entre execuções. Bug real, presente na v0.7.0.
3. Adicionar uma config custa 5 edições porque o `SettingsService` escreve JSON à mão.
4. `obj/` (15 arquivos) e `__pycache__/*.pyc` versionados apesar do `.gitignore` — origem do ritual `git checkout -- obj/`.
5. `Views/OverlayPage.*` é inalcançável (sem botão na sidebar); `WARP.md` aponta para pasta que não existe.
6. Todo `catch` é vazio; nenhum teste; i18n saudável (94/94).

**Próximo passo:** executar a Fase 0 (limpeza, risco zero) e a Fase 1 (bugs 002–005).

## Session: 2026-08-08 (parte 2) — Refactor executado (fases 0–4)

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|
| — | Fase 0: destrackear obj/ e __pycache__, csharp/ → raiz, matar OverlayPage, reescrever WARP.md | .gitignore-tracked files, Models/, Services/, Views/OverlayPage.*, WARP.md, Loc.cs | 1 raiz de código; ritual `git checkout -- obj/` extinto | ~4k |
| — | Fase 2 (antes da 1, p/ não reescrever 2x): AppLog + SettingsService declarativo | Services/AppLog.cs (novo), Services/SettingsService.cs | JsonSerializer + SnakeCaseLower; 250 → 158 linhas; .bak + escrita atômica | ~5k |
| — | Fase 1: bug-004 (error.log em %APPDATA%) e bug-005 (catch vazio) | App.xaml.cs, SettingsService.cs, StreamWindow.xaml.cs, WindowEnumerator.cs | falha de I/O deixou de ser invisível | ~2k |
| — | Helpers puros extraídos + Loc livre de WPF | Services/LayoutKey.cs, Services/ThumbnailGeometry.cs, Localization/TrExtension.cs | núcleo testável fora do Windows | ~4k |
| — | Fase 3: MainWindow quebrada em 4 colaboradores (bug-002 e bug-003 caem junto) | Services/StreamManager.cs, RegionCoordinator.cs, HotkeyManager.cs, LayoutStore.cs, MainWindow.xaml.cs | 836 → 261 linhas; nenhum arquivo passa de 311 | ~12k |
| — | Fase 4: projeto de testes net8.0 com source link | tests/ClientOPreview.Tests/* (5 arquivos), ClientOPreview.csproj | 39 testes, rodam no WSL | ~6k |
| — | Build + testes | — | Build succeeded, 0 warnings; 39 passed / 0 failed | ~1k |
| — | OpenWolf atualizado (anatomy, STATUS, cerebrum, buglog) | .wolf/* | bug-002..005 marcados como corrigidos | ~5k |

### Resumo da sessão (2026-08-08, parte 2)
Usuário mandou "pode executar tudo" → as 5 fases do roadmap foram entregues.

**Números:** `MainWindow.xaml.cs` 836 → 261 linhas; `SettingsService` 250 → 158; maior arquivo do projeto agora é `StreamWindow.xaml.cs` com 311. Adicionar uma config caiu de 5 pontos de edição para 2. 39 testes onde antes havia zero, executáveis no WSL.

**4 bugs corrigidos:** 002 (miniatura não persistia), 003 (occurrence index trocava layouts), 004 (error.log no CWD), 005 (catch vazio comendo settings corrompido).

**Estado:** branch `refactor/maintainability`. Compila com 0 warnings e passa nos testes, mas **não rodou no Windows** — WPF não executa no WSL.
**Próximo passo:** teste in-game seguindo o roteiro de 7 itens no STATUS.md → merge em main → v0.8.0.

## Session: 2026-08-09 — Release v0.8.0

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|
| — | Usuário testou in-game: aprovado, exceto a hotkey de ciclar | — | refactor validado; 1 regressão | ~1k |
| — | Causa da regressão rastreada no git (v0.7.0 chamava a ativação 2x por acionamento) | Services/StreamManager.cs | hipótese sólida registrada em bug-006 | ~2k |
| — | Merge do branch + bump de versão + problema conhecido no README | ClientOPreview.csproj, README.md | v0.8.0 na main | ~2k |
| — | bug-006 logado com plano de correção; STATUS aponta para ele | .wolf/buglog.json, .wolf/STATUS.md, .wolf/cerebrum.md | próxima sessão começa pela correção | ~3k |
| — | Publish + tag + release | bin/.../ClientOPreview.exe (285 KB) | tag v0.8.0 no GitHub | ~1k |

### Resumo da sessão (2026-08-09)
v0.8.0 solta com o refactor inteiro (fases 0–4). Usuário validou in-game e pediu para lançar mesmo
com a hotkey de ciclar quebrada, listando como problema conhecido.

**Lição principal:** o refactor unificou `ActivateSourceWindow` + `OnPreviewClicked` em um
`StreamManager.Focus` só. A duplicação parecia acidental, mas o `SetForegroundWindow` duplicado era
o workaround do bloqueio de foreground do Windows — daí a regressão (bug-006). Compilador e 39
testes não pegam esse tipo de coisa; só o teste in-game pegou.

**Próximo passo:** corrigir bug-006 em `StreamManager.Focus` (~4 linhas), verificar também as
hotkeys diretas, tirar "Problemas conhecidos" do README, soltar v0.8.1.

## Session: 2026-08-09 12:54

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|
| 13:00 | bug-006: `Focus` com ativação robusta — 1ª tentativa + fallback `AttachThreadInput`/`BringWindowToTop` na thread do foreground, `AppLog.Warn` se ainda falhar | Services/StreamManager.cs, Native/NativeMethods.cs | build 0 warnings, 39 testes passam; **falta validar in-game no Windows** | ~9k |
| 13:15 | rc1 testado pelo usuário: Ctrl+Tab cicla → fix de bug-006 CONFIRMADO. Tab sem modificador não cicla → bug-007 | — | fix de foco validado | ~2k |
| 13:30 | bug-007: instrumentar registro de hotkey — retorno de RegisterHotKey conferido e logado por hotkey, novo `AppLog.Info` | Services/HotkeyManager.cs, Services/AppLog.cs | build 0 warnings; rc2 em Downloads | ~7k |
| 14:00 | bug-007 fechado: default Alt+Tab (reservado pelo Windows) -> Ctrl+Tab; FailedCombos + aviso na HotkeysPage (i18n HotkeysRegisterFailed) | Models/Settings.cs, Services/HotkeyManager.cs, Views/HotkeysPage.*, MainWindow.xaml.cs, Localization/Loc.cs | build 0 warnings, 39 testes; rc3 em Downloads | ~12k |
| 14:20 | v0.8.1: bump 0.8.0->0.8.1, README (secao de atalhos reescrita, "Problemas conhecidos" removida), cerebrum + STATUS atualizados | ClientOPreview.csproj, README.md, .wolf/* | build 0 warnings, 39 testes | ~8k |
| 14:30 | commit 9c52606 + push main + tag v0.8.1 + release no GitHub com o .exe | — | https://github.com/meketreve/client-o-preview/releases/tag/v0.8.1 | ~5k |
| 15:00 | usuário reporta na v0.8.1: ciclo falha SÓ com o cliente em foco -> bug-008, hipótese UIPI/elevação; instrumentação (Info por WM_HOTKEY, elevação no Attach, win32 error do AttachThreadInput, SwitchToThisWindow como último recurso) | Services/StreamManager.cs, Services/HotkeyManager.cs, Native/NativeMethods.cs | build 0 warnings, 39 testes; rc de diagnóstico em Downloads | ~14k |
| 16:00 | bug-008 CONFIRMADO como UIPI: rodando como administrador funciona, inclusive com Tab puro (derruba a hipótese do hook de teclado em bug-007) | .wolf/buglog.json | causa fechada; falta decidir manifesto x runtime x documentar | ~3k |
| 16:20 | feature: reordenar o ciclo arrastando a lista "previews abertas" — _order explícito no StreamManager, persistido em hotkeys.cycle_order por chave de layout; ListBox com drag-drop | Services/StreamManager.cs, Models/Settings.cs, Views/HotkeysPage.*, MainWindow.xaml.cs, Localization/Loc.cs | build 0 warnings, 39 testes; rc em Downloads | ~18k |
| 16:40 | decisão do usuário: manifesto requireAdministrator. app.manifest criado + ApplicationManifest no csproj, versão 0.9.0, README (seção de elevação + ordem do ciclo, nota errada sobre tecla sem modificador removida) | app.manifest, ClientOPreview.csproj, README.md | build 0 warnings, 39 testes, requireAdministrator confirmado dentro do .exe; rc2 em Downloads | ~9k |
| 16:55 | feature: Esc/Delete/Backspace limpam o campo de tecla (cycle e diretas), hints i18n atualizados | Views/HotkeysPage.xaml.cs, Localization/Loc.cs, README.md | build 0 warnings, 39 testes | ~6k |
