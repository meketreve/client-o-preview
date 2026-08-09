# STATUS — client-o-preview

> Single source of truth for resuming work. Read this FIRST when starting a session.
> Update this file at the end of every work phase so the next `/clear` resumes in 1 read.
> Last updated: 2026-08-08

---

## ✅ Done

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

## 🚀 Próxima fase — validar in-game e soltar a v0.8.0

**Goal:** _o refactor foi validado só por compilador e testes; falta o teste que importa, com vários clientes abertos._

### Acceptance criteria (roteiro do teste)
1. Abrir 3+ previews, incluindo **dois clientes com o mesmo título** → cada uma reabre na própria posição depois de fechar o app (bug-003).
2. Mudar largura/altura/opacidade/fonte na aba Miniatura → **fechar e reabrir o app**: os valores continuam lá (bug-002).
3. Preset de região continua pegando depois de horas com clientes abrindo/fechando.
4. Topmost por foco sem "piscar" ao alternar rápido entre clientes.
5. Hotkeys (cycle + diretas) funcionam e sobrevivem a mudar a configuração.
6. Trocar idioma em runtime; minimizar para bandeja e restaurar.
7. `%APPDATA%\client-o-preview\error.log` — conferir se apareceu algo inesperado depois da sessão.

### Files to create / edit
| Type | File | Content |
|---|---|---|
| edit | `ClientOPreview.csproj` | `<Version>` 0.7.0 → 0.8.0 depois do teste aprovado |
| edit | `README.md` | nota de release: nada muda na UI; correções de persistência + log em %APPDATA% |

### Closed decisions
- Refactor por extração de colaborador, não MVVM: preserva o padrão "página emite `event` → MainWindow aplica".
- `SettingsService` via `JsonSerializer` + `SnakeCaseLower` mantendo os mesmos nomes JSON.
- Testes em `net8.0` com source link, para rodarem no WSL; o app segue `net8.0-windows`.
- Trabalho ficou no branch `refactor/maintainability` — merge em `main` só depois do teste in-game.

### Open decisions
- CI no GitHub Actions rodando `dotnet build` + `dotnet test` no push?
- Terceiro idioma (es?) — a string table aguenta, é só mais um dicionário + rádio.
- Hotkey para alternar região ↔ janela inteira?
- Presets globais por jogo (perfil) além do preset por piloto?
- Vale extrair um projeto `Core` sem WPF (hoje o source link resolve sem custo de build)?

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
- O refactor **ainda não rodou no Windows** — é a próxima fase.

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
