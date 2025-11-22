# WARP.md
This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Projetos
- `csharp/ClientOPreview/`: port em C# (WPF) usando DWM Thumbnails para pré-visualizações sem oclusão.

## Comandos úteis (PowerShell)

- C# (cd csharp\ClientOPreview)
  - Requisitos: .NET SDK instalado (net8.0-windows).
  - Build:
    - dotnet build -c Release
  - Executar:
    - dotnet run
  - Saída: .\bin\Release\net8.0-windows\

## Arquitetura de alto nível

- C# (WPF + DWM)
  - Captura/preview: `DwmRegisterThumbnail`/`DwmUpdateThumbnailProperties` para exibir a janela origem dentro de um `Window` WPF (não sofre oclusão por outras janelas).
  - UI: `MainWindow` lista janelas e abre múltiplos `StreamWindow`; cada `StreamWindow` ajusta topmost/local e notifica cliques para focar a janela real.
  - P/Invoke: `NativeMethods` encapsula chamadas a `user32.dll` e `dwmapi.dll`. `WindowEnumerator` aplica filtros (visível, não minimizada, sem owner, com título).

## Notas
- Projeto específico de Windows.
