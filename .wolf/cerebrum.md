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
- **Build no WSL funciona** com `~/.dotnet/dotnet build` graças a `EnableWindowsTargeting=true` (só compila; rodar, só no Windows). O Windows tem só runtime (`Microsoft.WindowsDesktop.App 8.0.28`), sem SDK.
- **`obj/` é versionado no repo** (entrou antes do `.gitignore`): todo build no WSL reescreve com paths Linux → rodar `git checkout -- obj/` depois de compilar.
- **Versão do app**: `<Version>` no csproj + `AboutPage` lendo do assembly. Release = tag `vX.Y.Z` + `gh release create` com o `.exe` single-file.
- Copiar `.exe` pra `/mnt/c/...` falha com I/O error se a versão anterior estiver rodando no Windows — salvar com nome versionado.

## Do-Not-Repeat

- [2026-08-02] Em WPF, não tratar coordenadas de seleção em pixels quando a janela é reposicionada/redimensionada: guardar o retângulo normalizado (0–1) como fonte da verdade e reprojetar no `SizeChanged`. A primeira versão do overlay perdia a seleção inicial porque o `SizeChanged` (PreviousSize = 0) sobrescrevia com tela cheia.

## Decision Log

- [2026-08-02] Region Focus via `DWM_TNP_RECTSOURCE` em vez de captura de tela (PrintWindow/BitBlt): mantém a promessa do README de não interagir com o processo do jogo e funciona em cliente DX fullscreen, onde PrintWindow costuma retornar preto.
- [2026-08-02] Presets nomeados (nome do piloto) + assignment por `title:occurrence:title`, reaproveitando o esquema de chave dos layouts.
- [2026-08-02] Picker não-modal e `Topmost=true`: o usuário precisa clicar no jogo para exibir o painel que vai recortar enquanto o seletor está aberto.

## Sessão 2026-08-03 — i18n, fusão de abas, topmost por foco

### Key Learnings
- **i18n em WPF sem .resx**: string table estática (`Localization/Loc.cs`) + `MarkupExtension` `{loc:Tr Key}` que devolve um `Binding` para o indexer `Loc.Instance[key]`. Trocar idioma = disparar `PropertyChanged` com `Binding.IndexerName` ("Item[]") → todo XAML bound reavalia sem restart.
- Chaves da string table **sem ponto** (PascalCase). `{Binding [a.b]}` é armadilha: o parser de `PropertyPath` trata o conteúdo do indexer de forma especial.
- `Loc.cs` também bate na ambiguidade WinForms: `Binding`/`BindingMode` precisam de alias `using Binding = System.Windows.Data.Binding;`.
- Strings montadas em code-behind (labels de DataTemplate, item "(None)", MessageBox) não seguem binding → assinar `Loc.LanguageChanged` e reconstruir o ItemsSource. Item "(None)" não pode mais ser reconhecido pelo texto: usar flag (`ThumbnailOption.IsNone`).
- O idioma tem que ser resolvido **antes** do `InitializeComponent()` da MainWindow (settings lidos primeiro no ctor), senão a primeira renderização sai no idioma default.
- `UserControl.Loaded` dispara a cada troca de página no `ContentHost` → nunca assinar evento de controle lá dentro (empilha handlers). Assinar no XAML.

### Decision Log
- [2026-08-03] String table em C# em vez de `.resx`/satellite assemblies: o app é single-file self-contained e só tem 2 idiomas; um arquivo evita configuração de build e mantém a troca em runtime trivial.
- [2026-08-03] Aba **General** fundida na **Thumbnail** (a pedido do usuário) — havia "Previews always on top" duplicado nas duas. `Views/GeneralPage.*` deletado; o model `General` continua igual no settings.json (compatibilidade).
- [2026-08-03] "Só no topo enquanto um cliente estiver em foco" resolvido no timer de foreground que já rodava a 400ms (`UpdateTopmostForFocus`), comparando o HWND de foreground com os clientes monitorados + janelas do próprio app (`Application.Current.Windows`). Sem hook global novo.
- [2026-08-03] Presets de região viraram **imutáveis**: o picker recusa salvar com nome já existente (`IsNameTaken`), exceto quando está editando aquele mesmo preset. Fluxo pedido pelo usuário: criar a região uma vez numa tela de exemplo, depois só selecionar nas outras contas.

### Do-Not-Repeat
- [2026-08-03] Não resolver a associação preview ↔ preset de região só pela chave por título (`title:occurrence:title`): o título do cliente muda em runtime e a seleção "não pega". Manter mapa por HWND enquanto a preview vive (ver bug-001 no buglog).
