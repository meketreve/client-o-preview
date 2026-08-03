# Cerebrum

> OpenWolf's learning memory. Updated automatically as the AI learns from interactions.
> Do not edit manually unless correcting an error.
> Last updated: 2026-08-02

## User Preferences

- Fala e recebe contexto em PT-BR (inclusive specs vindas de conversa de Discord); UI do app é em inglês, README em PT-BR.
- Modo autônomo: entender o pedido, planejar e aplicar sem ficar perguntando.
- Ao terminar uma tarefa, quer commit + push.

## Key Learnings

- **Project:** client-o-preview
- **Description:** Aplicativo Windows para pré-visualizar janelas em miniaturas ao vivo. Feito com IA:
- **Stack real:** C# .NET 8 + WPF + DWM Thumbnails (`net8.0-windows`, UseWPF + UseWindowsForms). `csharp/` guarda Models/Services, as janelas ficam na raiz, `Views/` tem as páginas de config.
- **Usuários:** jogadores multi-cliente (Star Citizen e afins) — querem vigiar painéis específicos (drones, capacitor) sem gastar espaço de tela.
- **WinForms habilitado no csproj** → `Point`, `Rectangle`, `Color`, `MouseEventArgs`, `KeyEventArgs` ficam ambíguos. Usar alias `using X = System.Windows...;` no topo do arquivo.
- **DWM compõe o thumbnail acima do conteúdo da janela host** → overlay de UI sobre o preview precisa ser outra janela (`AllowsTransparency`, `Background="#01000000"` para manter hit-test).
- **Build no WSL funciona** com `~/.dotnet/dotnet build` graças a `EnableWindowsTargeting=true` (só compila; rodar, só no Windows). O Windows tem só runtime, sem SDK.

## Do-Not-Repeat

- [2026-08-02] Em WPF, não tratar coordenadas de seleção em pixels quando a janela é reposicionada/redimensionada: guardar o retângulo normalizado (0–1) como fonte da verdade e reprojetar no `SizeChanged`. A primeira versão do overlay perdia a seleção inicial porque o `SizeChanged` (PreviousSize = 0) sobrescrevia com tela cheia.

## Decision Log

- [2026-08-02] Region Focus via `DWM_TNP_RECTSOURCE` em vez de captura de tela (PrintWindow/BitBlt): mantém a promessa do README de não interagir com o processo do jogo e funciona em cliente DX fullscreen, onde PrintWindow costuma retornar preto.
- [2026-08-02] Presets nomeados (nome do piloto) + assignment por `title:occurrence:title`, reaproveitando o esquema de chave dos layouts.
- [2026-08-02] Picker não-modal e `Topmost=true`: o usuário precisa clicar no jogo para exibir o painel que vai recortar enquanto o seletor está aberto.
