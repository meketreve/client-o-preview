# STATUS — client-o-preview

> Single source of truth for resuming work. Read this FIRST when starting a session.
> Update this file at the end of every work phase so the next `/clear` resumes in 1 read.
> Last updated: 2026-08-09

---

## ✅ Done

### Release v0.9.0 (2026-08-09) — ordem do ciclo, elevação, limpar atalho
- **Causa real do bug de foco encontrada: UIPI.** O cliente do jogo roda elevado; um processo de integridade menor não consegue ativar a janela dele (`AttachThreadInput` negado). Testado: com o app elevado funciona, **inclusive com Tab sem modificador** — o que derrubou a hipótese anterior de que o jogo engolia a tecla (bug-007 hipótese 2 estava errada).
- **`app.manifest` com `requireAdministrator`** + `<ApplicationManifest>` no csproj (decisão do usuário, sobre detectar em runtime ou só documentar). Verificado que a string vai embutida no `.exe` publicado.
- **Ordem do ciclo arrastável**: `StreamManager` mantém `_order` explícito (antes dependia da ordem do `Dictionary`, que não promete nada). Lista de previews abertas virou `ListBox` com drag-drop; ordem salva em `hotkeys.cycle_order` com a chave de layout, então sobrevive ao restart. Arrastar preserva o ponteiro do ciclo.
- **Esc / Delete / Backspace limpam o campo de tecla** (`HotkeysPage.NameOf`) — nome vazio simplesmente não registra nada.
- README: seção de elevação, ordem do ciclo, como limpar atalho; nota errada sobre "tecla sem modificador é engolida pelo jogo" removida.

### Release v0.8.1 (2026-08-09) — hotkeys consertadas
- **bug-006 corrigido e validado in-game**: `StreamManager.Focus` confere o resultado com `GetForegroundWindow()` e, se o Windows recusou, refaz a ativação com `AttachThreadInput` na thread do foreground atual + `BringWindowToTop`. Usuário confirmou: Ctrl+Tab cicla corretamente.
- **bug-007 (achado durante o teste)**: o default era `Alt+Tab`, **reservado pelo Windows** — `RegisterHotKey` sempre falhava (1409), silenciosamente. Default virou `Ctrl+Tab`; `settings.json` existente não é migrado.
- **Falha de registro agora é visível**: `HotkeyManager.FailedCombos` + aviso laranja na aba Hotkeys (`HotkeysRegisterFailed`, en + pt-BR) + linha no `error.log` com o código win32. Novo `AppLog.Info`.
- **Limite conhecido e documentado**: hotkey **sem modificador** pode ser engolida pelo cliente em foco (hook `WH_KEYBOARD_LL` / fullscreen exclusivo) — roda antes do processamento de hotkey do Windows, sem contorno via API. README orienta usar modificador.
- README: "Problemas conhecidos" removido, seção de atalhos reescrita com as duas armadilhas.

### Release v0.8.0 (2026-08-09)
- Refactor **validado in-game** pelo usuário; branch `refactor/maintainability` mergeada na `main`.
- `<Version>` 0.8.0, tag `v0.8.0`, release com o `.exe` single-file.
- **Solta com regressão conhecida documentada** (README → "Problemas conhecidos"): a hotkey de ciclar entre clientes parou de funcionar. Ver **bug-006**.

### Refactor para manutenção / vibecode (2026-08-08) — fases 0 a 4, todas entregues
Build: **Build succeeded, 0 warnings**. Testes: **39 passed, 0 failed**. Nada mudou de comportamento
para o usuário, exceto as 4 correções de bug abaixo.

**Fase 0 — limpeza**
- `obj/` (15 arquivos) e `__pycache__/*.pyc` **destrackeados** → acabou o ritual `git checkout -- obj/` depois de todo build no WSL.
- `csharp/Models` e `csharp/Services` movidos para `Models/` e `Services/` na raiz; pasta `csharp/` extinta. Uma raiz de código só.
- `Views/OverlayPage.*` (inalcançável), `Nav_Overlay` e as chaves `OverlayComingSoon`/`RegionOfSelected` do `Loc.cs` apagados.
- `WARP.md` reescrito (apontava para `csharp/ClientOPreview/`, pasta inexistente).

**Fase 1 — bugs achados na auditoria** (detalhe em `.wolf/buglog.json`)
- **bug-002**: tamanho/opacidade/fonte da miniatura não persistiam. Campos-espelho apagados, tudo lê e escreve no model.
- **bug-003**: occurrence index posicional trocava layouts entre clientes de mesmo título. Agora é o menor índice livre entre as previews vivas.
- **bug-004**: `error.log` ia para o CWD. Novo `Services/AppLog.cs` grava em `%APPDATA%/client-o-preview/`.
- **bug-005**: catch vazio comia settings corrompido. Log + `settings.json.bak` + gravação atômica.

**Fase 2 — `SettingsService` declarativo**
- `JsonSerializer` + `PropertyNamingPolicy = SnakeCaseLower`: 250 → 158 linhas, e o **model virou a fonte única do formato**.
- Compatibilidade verificada por teste com um `settings.json` da v0.7.0 (inclusive o legado `zoom_on_hover`).
- Novo `RemoveLayout` (o TODO "or some way to delete" morreu).

**Fase 3 — `MainWindow` quebrada: 836 → 261 linhas**
- `Services/StreamManager.cs` (253) — previews abertas, occurrence index, timer de foreground, topmost por foco.
- `Services/RegionCoordinator.cs` (139) — recorte por preview, mapa por HWND, picker, linhas da página.
- `Services/HotkeyManager.cs` (128) — RegisterHotKey, hook WM_HOTKEY, string→virtual key.
- `Services/LayoutStore.cs` (91) — geometria salva, migração de chave legada, previews a reabrir.
- `Services/LayoutKey.cs` (74) e `Services/ThumbnailGeometry.cs` (64) — **puros**, sem WPF nem Win32.
- `Localization/Loc.cs` ficou livre de WPF (o `{loc:Tr}` foi para `Localization/TrExtension.cs`).
- `StreamWindow` parou de alocar um brush por tick do timer de 400 ms.

**Fase 4 — rede de segurança**
- `tests/ClientOPreview.Tests/` em **net8.0** (não `-windows`): faz *source link* dos arquivos sem WPF, então `dotnet test` **roda no WSL**, não só no Windows.
- 39 testes: geometria do recorte/letterbox, chaves e geometria de layout, paridade en↔pt-BR, round-trip de settings (incl. arquivo da v0.7.0 e arquivo corrompido).

### Auditoria de manutenibilidade (2026-08-08)
- Projeto inteiro lido; resultado em `.wolf/anatomy.md` (descrição real por arquivo), `.wolf/cerebrum.md` (Mapa do código) e `.wolf/buglog.json` (4 bugs novos).

### i18n pt-BR / en (2026-08-03)
- `Localization/Loc.cs`: string table (94 chaves por idioma) + `{loc:Tr Key}`. Troca de idioma **em runtime**, sem restart.
- Aba **Idioma** (`Views/LanguagePage.*`), persistida em `settings.json` → `"language"`. Primeira execução segue o idioma do Windows.

### Aba General fundida na Thumbnail (2026-08-03)
- `Views/GeneralPage.*` deletado; sidebar com 7 botões. Nova opção **"Só no topo enquanto um cliente estiver em foco"**.

### Release v0.7.0 (2026-08-03)
- Build testada in-game e aprovada. Tag `v0.7.0` + release com o `.exe` single-file.

### Region Focus (2026-08-02/03)
- Crop normalizado (0–1) via `rcSource` do DWM, presets nomeados imutáveis, fluxo "cria uma vez → seleciona nas outras contas", **bug-001** corrigido por mapa HWND.

---

## 🚀 Próxima fase — não decidida (v0.9.0 fechou o ciclo das hotkeys)

**Verificação pendente da v0.9.0** — a release saiu a pedido do usuário **sem** o teste in-game da build final. Confirmar na primeira oportunidade:
1. UAC ao abrir, e Ctrl+Tab ciclando com o cliente em foco.
2. Arrastar a lista reordena, numeração acompanha, ciclo segue a ordem; ordem preservada depois de fechar e reabrir.
3. Esc/Delete/Backspace esvaziam o campo de tecla e o atalho para de disparar.
4. Hotkeys diretas (Alt+NumPad) seguem a mesma ordem.

Candidatas seguintes, em ordem de custo/benefício:

1. **CI no GitHub Actions** — `dotnet build` + `dotnet test` no push. Barato (os testes já rodam em `net8.0`, sem Windows) e evita que um refactor volte a quebrar o núcleo sem ninguém ver. Único item que protege o que já existe.
2. Terceiro idioma (es?) — a string table aguenta, é só mais um dicionário + rádio.
3. Hotkey para alternar região ↔ janela inteira.
4. Presets globais por jogo (perfil) além do preset por piloto.

### Closed decisions
- Elevação: **manifesto `requireAdministrator`**, não detecção em runtime. Escolha do usuário — o cliente dele roda elevado sempre, então o UAC é inevitável de qualquer jeito.
- Ordem do ciclo mora no `StreamManager` (`_order`) e é persistida por chave de layout, não por título puro: título repete entre clientes.
- Ativação de janela: tentar → **conferir com `GetForegroundWindow()`** → `AttachThreadInput` como fallback. Não confiar no bool de `SetForegroundWindow`.
- Default de hotkey de ciclo: `Ctrl+Tab`. `Alt+Tab` é reservado pelo Windows e nunca registra.
- `settings.json` existente **não** é migrado quando um default muda.
- Caminho de foco fica sem teste automatizado (exige janela real) — checklist manual.
- Refactor por extração de colaborador, não MVVM: preserva o padrão "página emite `event` → MainWindow aplica".
- `SettingsService` via `JsonSerializer` + `SnakeCaseLower` mantendo os mesmos nomes JSON.
- Testes em `net8.0` com source link, para rodarem no WSL; o app segue `net8.0-windows`.

---

## 📁 Active architecture

- **Stack:** C# / .NET 8 / WPF (`net8.0-windows`, `UseWPF` + `UseWindowsForms`), DWM Thumbnails API. Zero dependência NuGet no app.
- **Fluxo:** `App` → `MainWindow` (lê settings, resolve idioma, cria os 4 colaboradores, faz o wiring) → `StreamManager` abre uma `StreamWindow` por cliente → `ThumbnailGeometry` traduz região + zoom em `rcSource`/`rcDestination`.
- **Colaboradores:** `StreamManager` (previews + foco), `RegionCoordinator` (recortes), `LayoutStore` (posições), `HotkeyManager` (atalhos globais). A `MainWindow` só liga os fios.
- **Padrão:** páginas expõem `event` + `LoadFrom(...)`; a `MainWindow` aplica e salva; **nenhuma page toca em settings direto**.
- **Persistência:** `SettingsService` → `%APPDATA%/client-o-preview/settings.json`. O **model é o formato** — adicionar config = editar `Models/Settings.cs` + a UI.
- **Diagnóstico:** `AppLog` → `%APPDATA%/client-o-preview/error.log`.
- **Onde mexer para cada tipo de mudança:** tabela no `.wolf/cerebrum.md`, seção "Mapa do código".

---

## ⚠️ External blockers (don't block coding)

- Executar a app só no Windows (WPF). No WSL, `~/.dotnet/dotnet build` compila e `dotnet test` roda (os testes são `net8.0`).
- Fluxo de teste que funcionou: publicar e copiar o `.exe` para `/mnt/c/Users/Meketreve/Downloads/` com nome `ClientOPreview-vX.Y.Z-rcN.exe`; o usuário roda no Windows e reporta.

---

## 🔧 Useful commands

```bash
~/.dotnet/dotnet build                      # compila no WSL (validação de sintaxe/tipos)
~/.dotnet/dotnet test tests/ClientOPreview.Tests    # 39 testes, rodam no WSL
~/.dotnet/dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
dotnet run                                  # dev, no Windows
gh release create vX.Y.Z <exe> --title ... --notes ...
openwolf scan                               # regenera .wolf/anatomy.md
```

---

## 📚 References (read IF needed)

- `.wolf/cerebrum.md` — **Mapa do código** + User Preferences + Do-Not-Repeat + Decision Log
- `.wolf/anatomy.md` — índice de arquivos com o que cada um faz
- `.wolf/buglog.json` — bug-001 a bug-005, todos corrigidos
