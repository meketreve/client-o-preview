---
updated: 2026-10-03
tier: 2
---

# Aprendizados

<!-- TETO: 100 linhas fora de blocos auto. Uma linha por fato, formato grepável: `- [AAAA-MM-DD] [tag] fato — consequência`.
     O que foi tentado ou decidido leva veredito depois da tag: [✓] testado e mantido, [✗] tentado e descartado (nunca apagar), [↻ AAAA-MM-DD] substituído pela entrada dessa data. -->
<!-- Resumo do essencial. Histórico completo (por sessão): `.wolf/cerebrum.md`; bugs: `.wolf/buglog.json`. -->

## Preferências do usuário

- [2026-08-08] [prefs] fala PT-BR; README em PT-BR, UI com en + pt-BR — manter os dois idiomas em todo texto novo
- [2026-08-08] [prefs] trabalha por vibecode — otimizar por "quantos arquivos abro para mudar X", não por elegância
- [2026-08-09] [prefs] prefere soltar release com problema conhecido documentado a segurar a entrega

## Pegadinhas (versão, API, CI)

- [2026-08-08] [wsl] app `net8.0-windows` compila no WSL (`EnableWindowsTargeting`) mas só roda no Windows — validação real é in-game pelo usuário
- [2026-08-08] [wpf] `UseWindowsForms` ligado deixa `Point`, `Color`, `MouseEventArgs`, `DragEventArgs`, `Binding`… ambíguos — alias `using X = System.Windows...;`
- [2026-08-08] [testes] testes fazem source link (não `ProjectReference`) dos arquivos sem WPF — arquivo novo testável entra na lista do `.csproj` de teste; nada de WPF nele
- [2026-08-08] [json] `SnakeCaseLower` sem `DictionaryKeyPolicy` — senão renomeia as chaves de `layouts`/`assignments`
- [2026-08-03] [i18n] chaves do `Loc` em PascalCase sem ponto; idioma resolvido antes do `InitializeComponent()`
- [2026-08-03] [wpf] `UserControl.Loaded` dispara a cada troca de aba — não assinar evento lá (empilha handlers)
- [2026-08-09] [win32] `Alt+Tab` é reservado: `RegisterHotKey` falha (1409) — default é `Ctrl+Tab`
- [2026-08-09] [win32] cliente do jogo elevado + app comum = UIPI nega foco — por isso o app roda `requireAdministrator`
- [2026-08-09] [win32] hotkey sem modificador pode ser engolida pelo jogo (`WH_KEYBOARD_LL`) — não sugerir como padrão
- [2026-08-02] [dwm] thumbnail DWM é composto acima do host — overlay de UI precisa ser outra janela
- [2026-08-08] [deploy] copiar `.exe` para `/mnt/c/...` falha se a versão anterior estiver rodando — nome versionado

- [2026-10-11] [x11] error handler padrão do Xlib chama exit() — instalar o nosso depois do Avalonia; BadWindow é rotina
- [2026-10-11] [avalonia] não grava `_NET_WM_PID` — excluir janelas próprias por handle (bug-009)
- [2026-10-11] [x11] Wayland não captura janela alheia — build Linux é só X11 (Xorg)
- [2026-10-11] [x11] atalho que o desktop já usa → BadAccess no XGrabKey; jogo com XGrabKeyboard engole atalhos

- [2026-10-11] [x11] multi-monitor deixa cantos mortos no desktop (ex.: (0,0) fora de qualquer tela) — posicionar relativo a `Screens`, nunca ao (0,0)
- [2026-10-11] [avalonia] `BeginMoveDrag` não move preview sem borda no Cinnamon — arraste manual (bug-010)
- [2026-10-11] [x11] `wmctrl -lG` dobra x/y com WM que reparenta — conferir posição com `XTranslateCoordinates`

- [2026-10-12] [avalonia] GLX na NVIDIA com ~10 janelas = ~90% de CPU na thread de render; Software = ~8% — padrão Software (bug-011)
- [2026-10-12] [x11] janela Avalonia em software é ARGB: byte de alfa 0 vindo do XGetImage deixa a preview transparente — forçar 0xFF
- [2026-10-12] [x11] captura lê o pixmap do frame do compositor (sem VRAM extra); redirect do cliente só como fallback
- [2026-10-12] [perpetuum] 1 cliente com janela 1928x1074 sai corrompido na captura em qualquer modo (janela real ok) — provável superfície Vulkan filha do Wine; não resolvido

## Erros a não repetir

- [2026-08-08] [settings] campo-espelho de settings na `MainWindow` causou bug-002 — ler direto do model
- [2026-08-08] [log] `catch { }` cegou o debug — logar com `AppLog`
- [2026-08-09] [win32] chamada "duplicada" em P/Invoke pode ser workaround (bug-006) — perguntar antes de unificar
- [2026-08-09] [win32] bool ignorado de `RegisterHotKey`/`SetForegroundWindow` vira "não acontece nada" — conferir e logar
- [2026-08-09] [teste] "validado" a partir de teste parcial — perguntar o cenário exato antes de declarar corrigido

## Decisões e o porquê

- [2026-08-08] [arquitetura] [✓] extração de colaboradores (StreamManager, HotkeyManager, LayoutStore, RegionCoordinator) — preserva "página emite event → MainWindow aplica"
- [2026-08-08] [arquitetura] [✗] MVVM/DI — obrigaria reaprender o projeto inteiro
- [2026-08-02] [regiao] [✓] recorte via `rcSource` do DWM — [✗] PrintWindow/BitBlt: interage com o jogo e dá preto em fullscreen DX
- [2026-08-03] [i18n] [✓] string table em C# — [✗] `.resx`: build mais complexo para só 2 idiomas
- [2026-08-09] [elevacao] [✓] manifesto `requireAdministrator` — escolha do usuário; detecção em runtime descartada
- [2026-10-11] [linux] [✓] Avalonia + X11 no mesmo repo, núcleo por source link — [✗] repo separado em Rust: perde o núcleo C#
- [2026-10-11] [linux] [✓] XRender escala no servidor + XGetImage pequeno → WriteableBitmap (UI toda no Avalonia)
- [2026-08-09] [settings] [✓] mudança de default não migra `settings.json` existente — não mexer em escolha do usuário
