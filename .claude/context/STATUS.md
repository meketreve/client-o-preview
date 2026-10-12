---
updated: 2026-10-03
tier: 2
---

# Status — tier 2+

<!-- TETO: 60 linhas FORA do bloco auto (o bloco é do script e não conta). Resumir "Concluído" quando crescer. -->

## Estado atual

v0.10.0 publicada (2026-10-12): Windows (sem mudança de comportamento) + primeira versão Linux (Avalonia + X11).
Linux testado in-game pelo usuário com Perpetuum/Proton: previews, arraste, hotkeys ok; interface sem travar
(captura em thread própria, render Software). Windows desta release não foi testado no Windows (só Loc.cs mudou).

## Próxima fase

1. Cliente com janela 1928x1074 sai corrompido na captura (provável superfície Vulkan filha do Wine)
2. Paridade Linux: seletor visual de região, aba Zoom, mapeamento hotkey → título, bandeja
3. CI no GitHub Actions: build dos dois projetos + testes

## Pendências e bloqueios

- Windows: executar/validar só no Windows; usuário testa in-game
- Caminho de foco/hotkey sem teste automatizado — checklist manual nos dois OS

## Concluído (recente)

- v0.10.0: versão Linux; fixes de arraste/multi-monitor (bug-010) e desempenho/VRAM (bug-011)

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
