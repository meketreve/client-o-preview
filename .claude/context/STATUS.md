---
updated: 2026-10-03
tier: 2
---

# Status — tier 2+

<!-- TETO: 60 linhas FORA do bloco auto (o bloco é do script e não conta). Resumir "Concluído" quando crescer. -->

## Estado atual

Versão Linux (MVP) em `linux/` — Avalonia + XComposite/XRender, X11 só. Compila, 63 testes verdes, testada ao vivo
no Cinnamon com preview do Steam (recorte por preset, opacidade, hotkeys registradas). **Falta validação in-game
pelo usuário** (Perpetuum via Proton): foco por clique/hotkey, ciclo, posição salva.
Windows segue v0.9.0, build 0 warnings.

## Próxima fase

1. Usuário testa o Linux in-game → corrigir o que aparecer (foco `_NET_ACTIVE_WINDOW`, grab de teclado do jogo)
2. Release v0.10.0 com os dois binários (`build.bat` no Windows + `./build-linux.sh`)
3. Paridade Linux: seletor visual de região, aba Zoom, mapeamento hotkey → título, bandeja
4. CI no GitHub Actions: build dos dois projetos + testes

## Pendências e bloqueios

- Windows: executar/validar só no Windows; usuário testa in-game
- Caminho de foco/hotkey sem teste automatizado — checklist manual nos dois OS

## Concluído (recente)

- 2026-10-11: versão Linux MVP (`linux/`), testes de `KeyNames`, `build-linux.sh`, README

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
