---
updated: 2026-10-03
tier: 2
---

# Status — tier 2+

<!-- TETO: 60 linhas FORA do bloco auto (o bloco é do script e não conta). Resumir "Concluído" quando crescer. -->

## Estado atual

v0.9.0 publicada e validada in-game pelo usuário em 2026-08-09; nada pendente desde então (projeto parado ~2 meses).
Hotkeys de ciclo funcionando (causa raiz era UIPI → app roda elevado). Build 0 warnings, 39 testes verdes no último registro.
Histórico detalhado das releases e do refactor: `.wolf/STATUS.md`.

## Próxima fase

Não decidida — escolher com o usuário. Candidatas (custo/benefício):
1. CI no GitHub Actions: `dotnet build` + `dotnet test` no push (novo `.github/workflows/`) → verify: `gh run list` verde
2. Terceiro idioma (es) em `Localization/Loc.cs` + rádio em `Views/LanguagePage`
3. Hotkey para alternar região ↔ janela inteira (`Services/HotkeyManager.cs`)
4. Perfis globais por jogo além do preset por piloto
Antes de tudo: `~/.dotnet/dotnet build && ~/.dotnet/dotnet test tests/ClientOPreview.Tests` → verify: 0 erros, 39 passed

## Pendências e bloqueios

- Executar/validar só no Windows: publicar e copiar `.exe` versionado para `/mnt/c/Users/Meketreve/Downloads/`; usuário testa in-game
- Caminho de foco/hotkey sem teste automatizado — checklist manual

## Concluído (recente)

- v0.9.0: ordem do ciclo arrastável, `requireAdministrator`, Esc/Delete/Backspace limpam tecla
- v0.8.1: foco com `AttachThreadInput`, default `Ctrl+Tab`, aviso de falha de registro
- v0.8.0: refactor (MainWindow 836 → 261 linhas, `SettingsService` declarativo, testes)

## Bruto do git (auto — não editar)

<!-- auto:start -->
data: 2026-10-03

```
2e89aef chore: fechar a sessao com a v0.9.0 validada in-game
092219b chore: registrar a release v0.9.0 na memoria do OpenWolf
fc46330 feat: Esc, Delete e Backspace limpam o campo de tecla
3f76ef9 feat: ordem do ciclo arrastável e elevação obrigatória (v0.9.0)
68c1bff chore: registrar a release v0.8.1 na memoria do OpenWolf
9c52606 fix: hotkeys de foco e registro (v0.8.1)
364da92 chore: release v0.8.0
f7a9840 Merge branch 'refactor/maintainability'
--- status ---
?? .claude/context/
```
<!-- auto:end -->
