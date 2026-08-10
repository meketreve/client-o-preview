# Cerebrum

> OpenWolf's learning memory. Updated automatically as the AI learns from interactions.
> Do not edit manually unless correcting an error.
> Last updated: 2026-08-08

## User Preferences

- Fala e recebe contexto em PT-BR (inclusive specs vindas de conversa de Discord); UI do app é em inglês, README em PT-BR.
- Modo autônomo: entender o pedido, planejar e aplicar sem ficar perguntando.
- Ao terminar uma tarefa, quer commit + push.
- [2026-08-08] Trabalha o projeto por **vibecode**: quer que cada mudança caiba num pedido curto, sem precisar ler arquivo gigante nem editar a mesma coisa em 3 lugares. Otimizar por "quantos arquivos preciso abrir pra mudar X", não por elegância.

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

## Sessão 2026-08-08 — auditoria de manutenibilidade (vibecode)

### Mapa do código (ler isto antes de abrir arquivo)

Fluxo único do app, em 5 saltos:

1. `App.xaml.cs` cria a `MainWindow`.
2. `MainWindow` ctor: lê settings → resolve idioma → `InitializeComponent()` → instancia as 8 páginas → **faz o wiring de todos os eventos** → liga o timer de foreground (400 ms).
3. Usuário escolhe janelas em `ClientsPage` → `OpenStreamForItem()` cria uma `StreamWindow` por janela monitorada.
4. `StreamWindow` registra um thumbnail DWM e o desenha com `UpdateThumbnailRect()` — é aí que região, zoom e letterbox viram `rcSource`/`rcDestination`.
5. Qualquer alteração de config: página emite `event` → `MainWindow` aplica nas streams + grava via `SettingsService`. **Nenhuma página toca em settings direto.**

Onde mexer para cada tipo de mudança (**atualizado depois do refactor de 2026-08-08**):

| Quero… | Mexo em |
|---|---|
| nova opção de config | `Models/Settings.cs` (o JSON sai de graça, snake_case) + a página + 1 linha de wiring na `MainWindow`. **Nunca** mais mexer no `SettingsService` |
| texto novo na UI | `Localization/Loc.cs` (as DUAS tabelas — o teste `LocTests` reprova se faltar) + `{loc:Tr Chave}` no XAML |
| como a preview é recortada/desenhada | `Services/ThumbnailGeometry.cs` (matemática, testada) e `StreamWindow.UpdateThumbnailRect()` (só aplica) |
| seletor de região | `RegionPickerWindow` (DWM + salvar) e `RegionOverlayWindow` (retângulo) |
| qual preview mostra qual recorte | `Services/RegionCoordinator.cs` |
| hotkey global | `Services/HotkeyManager.cs` |
| onde a preview reabre | `Services/LayoutStore.cs` (efeito) + `Services/LayoutKey.cs` (chave/geometria, testado) |
| abrir/fechar preview, foco, topmost | `Services/StreamManager.cs` |
| ligar uma página nova | `MainWindow.WirePages()` |

### Key Learnings

- **`MainWindow.xaml.cs` é god object (836 linhas / ~7,6k tokens)**: streams, hotkeys, layout, região, foreground e wiring no mesmo arquivo. Toda tarefa de vibecode paga esse arquivo inteiro em contexto. É o alvo #1 de refactor.
- **Estado duplicado**: `_previewsTopmost`, `_trackLocations`, `_uniqueLayout`, `_thumbWidth`, `_thumbHeight`, `_opacityPct`, `_titleFontSize`, `_activeHighlightColor` são espelhos de `_settings.General`/`_settings.Thumbnail`. Já causou o **bug-002** (tamanho/opacidade da miniatura não persistem). Não criar espelho novo.
- **Adicionar uma config custa 5 edições** (model + Load + Save + página + wiring). Esquecer o Save = perda silenciosa. Foi exatamente assim que o bug-002 nasceu.
- **Duas raízes de código com o mesmo namespace**: `Models/` + `csharp/Models/` (ambos `ClientOPreview.Models`) e `Services/` + `csharp/Services/`. Nada no csproj exige isso — é resquício do port do Python. Confunde qualquer busca por "onde fica o model".
- **`obj/` (15 arquivos) e `__pycache__/app.cpython-313.pyc` estão versionados** mesmo estando no `.gitignore` (entraram antes dele). É a causa do ritual `git checkout -- obj/` depois de cada build no WSL — o ritual é workaround, não solução: `git rm -r --cached obj/ __pycache__/` resolve de vez.
- **Código morto**: `Views/OverlayPage.*` + `Nav_Overlay` na `MainWindow` — a sidebar não tem botão para ela desde a fusão das abas. A chave `RegionOfSelected` no `Loc.cs` também não é usada por ninguém.
- **`WARP.md` está desatualizado**: manda ir para `csharp/ClientOPreview/`, pasta que não existe mais. Agente que ler isso se perde.
- **i18n está saudável**: 94 chaves em cada tabela, paridade exata en↔pt-BR, zero duplicadas. O risco é futuro — `Loc.Get()` devolve o nome da chave quando falta, então uma chave nova só em `En` passa despercebida até aparecer in-game.
- **Todo `catch` é vazio** (`SettingsService.Load/Save`, `CheckForeground`, `ApplySavedGeometry`, `GetWindowTitle`, `SetHighlightColor`, `EnsureThumbnail`, `App`). Debug fica cego: settings.json corrompido volta pro default sem avisar (bug-005).
- **Sem testes e sem CI.** Existem funções puras prontas para teste sem WPF: `Letterbox`, `IsFullWindow`, `Clamp`, `SourceRect`, o parse de geometria `WxH+L+T`, `Loc.Normalize`, `ParseModifiers`, `SanitizeLayoutKey`.
- **`SetActiveState` aloca um `SolidColorBrush` novo a cada tick** do timer de 400 ms, por preview. Cachear os dois brushes e só trocar quando o estado muda.
- **A geometria salva é string** (`"160x90+50+50"`) parseada com `int.Parse` dentro de try/catch vazio. Guardar 4 números seria mais simples de ler e de corrigir.

### Decision Log

- [2026-08-08] Refactor da `MainWindow` será por **extração de colaborador**, não por MVVM: o padrão atual (página emite `event` → MainWindow aplica) já funciona e o usuário edita por vibecode. Trocar por MVVM/DI obrigaria a reaprender o projeto inteiro; extrair `HotkeyManager`, `LayoutStore`, `RegionCoordinator` e `StreamManager` mantém o mesmo modelo mental e derruba a `MainWindow` para ~250 linhas de wiring.
- [2026-08-08] `SettingsService` vai migrar do JSON escrito à mão para `JsonSerializer` com `PropertyNamingPolicy = SnakeCaseLower` (nativo no .NET 8): mata ~230 linhas e reduz "adicionar config" de 5 edições para 2 (model + UI). Compatível com o `settings.json` atual porque os nomes snake_case são os mesmos — validar campo a campo antes de trocar.
- [2026-08-08] `csharp/Models` e `csharp/Services` serão movidos para `Models/` e `Services/` na raiz (namespace já é idêntico, então é `git mv` sem editar `using`).

### Do-Not-Repeat

- [2026-08-08] Não criar campo-espelho de settings na `MainWindow` ("`_algumaCoisa` = `_settings.X.AlgumaCoisa`"). Ler direto do model. Foi o que produziu o bug-002.
- [2026-08-08] Não escrever leitor/escritor de JSON à mão no `SettingsService`. O model **é** o formato; um campo novo já nasce persistido.
- [2026-08-08] Não usar contagem posicional para identificar preview (`Count(w => w.Title == t)`): fechar uma do meio faz a próxima colidir com uma viva. Menor índice livre (`StreamManager.AllocateOccurrence`).
- [2026-08-08] Não deixar `catch { }`. Logar com `AppLog` — debug sem log custou horas nesta base.
- [2026-08-09] Ao "limpar" duplicação em código Win32, **não presumir que a repetição é acidental**. `ActivateSourceWindow` chamava `SetForegroundWindow` duas vezes; parecia copiar-e-colar mal feito, mas a chamada dupla é o workaround do bloqueio de foreground do Windows. Unificar em uma chamada quebrou as hotkeys (bug-006). Antes de remover repetição em P/Invoke, perguntar "isso é bug ou é workaround?" — e testar o caminho, que compilador e teste unitário não pegam.

## Sessão 2026-08-08 (parte 2) — refactor executado

### Key Learnings

- **Fases pequenas e compiláveis funcionaram**: 0 limpeza → 1 bugs → 2 SettingsService → 3 quebrar MainWindow → 4 testes, com `dotnet build` entre cada uma. Nenhum passo precisou ser desfeito.
- **Ordem importa**: fazer a Fase 2 antes da 1 evitou reescrever o `SettingsService` duas vezes, e a correção do bug-002 saiu de graça dentro da Fase 3 (apagar os campos-espelho *é* a correção).
- **`JsonNamingPolicy.SnakeCaseLower` (nativo no .NET 8)** reproduz exatamente os nomes que o leitor manual escrevia. Cuidado: **não** setar `DictionaryKeyPolicy` — renomearia as chaves de `layouts`/`assignments` (`title:0:EVE`).
- **Coleção com `{ get; set; }` é substituída** pelo `JsonSerializer`, não concatenada — por isso `direct_keys` do arquivo não vira 12 itens em cima dos 10 defaults. Se fosse get-only, viraria.
- **Default do model = default do JSON ausente**: como `TrackLocations`, `PreviewsTopmost`, `InternalZoom` e `LockAspect` já nascem `true`, o padrão antigo `!TryGetProperty(...) || v.GetBoolean()` some sem mudar comportamento.
- **Testar WPF no WSL é impossível, mas testar o *núcleo* não é**: o projeto de teste é `net8.0` e faz *source link* (`<Compile Include="../../Services/LayoutKey.cs" />`) dos arquivos livres de WPF, em vez de `ProjectReference`. `ProjectReference` puxaria `Microsoft.WindowsDesktop.App`, que não existe fora do Windows. Efeito colateral bom: `internal` continua visível (mesmo assembly), sem precisar de `InternalsVisibleTo`.
- **Para isso valer, o núcleo tem que ficar limpo**: `Loc.cs` só saiu do WPF porque `Binding.IndexerName` virou a constante `"Item[]"` e o `TrExtension` foi para outro arquivo.
- **`<Compile Remove="tests/**" />` é obrigatório** no csproj do app: o globbing SDK-style da raiz engoliria os arquivos de teste.
- **Escrita atômica de settings** (`.tmp` + `File.Move(overwrite: true)`) custou 2 linhas e elimina o risco de truncar o arquivo num crash.
- **`Assert.Empty(x.Where(...))` dispara xUnit2029.** Usar `Assert.All` com mensagem — dá diagnóstico melhor e não gera warning.

### Decision Log

- [2026-08-09] v0.8.0 saiu **com** a regressão da hotkey de ciclar documentada no README, a pedido do usuário, em vez de segurar a release. Preferência dele: entregar o ganho e listar o problema conhecido.
- [2026-08-08] Testes por **source link** em vez de `ProjectReference`, e alvo `net8.0` em vez de `net8.0-windows`: torna `dotnet test` executável no WSL, que é onde o desenvolvimento acontece. O custo é manter a lista de arquivos no csproj de teste.
- [2026-08-08] Geometria e chaves saíram de dentro das janelas para `Services/ThumbnailGeometry.cs` e `Services/LayoutKey.cs`. O critério de corte foi "dá para testar sem abrir uma janela", não "é bonito".
- [2026-08-08] O refactor ficou em `refactor/maintainability`, não direto na `main`: não dá para rodar WPF no WSL, então merge só depois do teste in-game.

## Sessão 2026-08-09 (parte 2) — hotkeys: foco e registro

### Key Learnings

- **`SetForegroundWindow` de um processo em background é recusado pelo Windows.** O padrão que funciona: tentar uma vez e **conferir com `GetForegroundWindow()`** (a API retorna `true` mesmo quando só piscou a taskbar); se não pegou, `AttachThreadInput(nossaThread, threadDoForegroundAtual, true)` + `BringWindowToTop` + nova tentativa, sempre desanexando no `finally`. Confirmado in-game: consertou o ciclo entre clientes.
- **`Alt+Tab` é reservado pelo sistema** — `RegisterHotKey` sempre falha (win32 1409). Era o **default do app**, ou seja, em instalação nova a hotkey de ciclar nunca funcionou. Um default que a plataforma recusa é um bug silencioso: escolher combos que o SO permite.
- **Retorno de API Win32 ignorado = bug indistinguível.** `RegisterHotKey` falhando e `SetForegroundWindow` falhando produzem o mesmo sintoma ("aperto e não acontece nada"). Conferir e logar o retorno separou as duas causas em um teste só.
- **Hotkey sem modificador pode ser engolida pela janela em foco.** Hook `WH_KEYBOARD_LL` (comum em jogo/anticheat) e fullscreen exclusivo rodam **antes** do processamento de hotkey do Windows — não existe API que faça `RegisterHotKey` ganhar disso. Sintoma-assinatura: "funciona fora do jogo, não funciona dentro". Único remédio é orientar o uso de modificador.
- **Sintoma pode provar o registro.** "Só funciona depois de clicar fora" já prova que a hotkey **registrou** (senão não funcionaria em lugar nenhum) — isso descarta a hipótese de registro sem precisar de log.

### Decision Log

- [2026-08-09] Default de `CycleModifiers` mudou de `Alt` para `Ctrl`. Não migra `settings.json` existente: mexer em config que o usuário escolheu é pior que deixar o aviso aparecer.
- [2026-08-09] Falha de registro de hotkey virou **aviso na própria aba** (laranja, via `HotkeysPage.ShowRegistrationResult` alimentado pela `MainWindow`), não só linha de log. Erro que o usuário provoca na UI tem que aparecer na UI.
- [2026-08-09] Caminho de foco continua **sem teste automatizado** — exige janela real. Cobertura por checklist manual, como já estava previsto.

### Do-Not-Repeat

- [2026-08-09] Não ignorar o retorno de `RegisterHotKey`/`SetForegroundWindow`. Bool ignorado em P/Invoke vira "não acontece nada" sem pista nenhuma.
- [2026-08-09] Não oferecer/definir hotkey sem modificador como padrão nem sugerir ao usuário: o jogo em foco engole a tecla.

## Sessão 2026-08-09 (parte 3) — v0.9.0

### Key Learnings

- **UIPI foi a causa raiz das hotkeys "quebradas", não o refactor.** Cliente de jogo rodando elevado + app comum = `AttachThreadInput` negado (win32 5) e nenhuma forma de trazer a janela para frente. Assinatura do sintoma: **funciona quando o foreground é uma janela comum, falha quando é o jogo**. Antes de investigar API de foco, perguntar "o alvo roda elevado?".
- **Hipótese confirmada por teste parcial é hipótese não confirmada.** "Ctrl+Tab funcionou" foi tomado como validação de bug-006, mas o teste tinha sido feito com o foreground fora do jogo. Pedir o cenário exato do teste, não só o resultado.
- **`Dictionary` não promete ordem.** O ciclo dependia da ordem de `_streams.Keys` — funcionava por acaso e não dava para reordenar. Ordem que o usuário enxerga precisa de `List` explícita.
- **Título não identifica preview** (clientes com o mesmo título). Handle identifica em runtime; `LayoutKey.For(title, occurrence)` identifica entre sessões. A lista da UI carrega o `IntPtr` junto por isso.
- **`net8.0-windows` com `UseWindowsForms` ambiguiza tipos de UI** (`Point`, `MouseEventArgs`, `DragEventArgs`, `DragDropEffects` existem em WinForms e WPF). Em code-behind novo, qualificar `System.Windows.*` desde o começo.

### Decision Log

- [2026-08-09] Elevação por **manifesto `requireAdministrator`**, escolhido pelo usuário entre: detectar em runtime e oferecer reinício, manifesto, ou só documentar. Motivo: o cliente dele roda elevado sempre.
- [2026-08-09] Ordem do ciclo **persistida** (`hotkeys.cycle_order`), não só de sessão, reusando a chave dos layouts.
- [2026-08-09] Esc/Delete/Backspace limpam o campo de tecla; nome vazio já não registrava nada, então a feature é uma linha (`HotkeysPage.NameOf`) e não um caminho novo.
- [2026-08-09] v0.9.0 saiu **sem teste in-game da build final**, a pedido do usuário. Pendência anotada no STATUS.

### Do-Not-Repeat

- [2026-08-09] Não anunciar bug como corrigido a partir de confirmação parcial do usuário. Perguntar em que cenário o teste rodou antes de escrever "validado" em release notes.
