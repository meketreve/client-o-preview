# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Projetos
- `python/`: implementação original em Python (tkinter + Win32 + Pillow).
- `csharp/ClientOPreview/`: port em C# (WPF) usando DWM Thumbnails para pré-visualizações sem oclusão.

## Comandos úteis (PowerShell)

- Python (cd python)
  - Preparar ambiente:
    - python -m venv .venv
    - .\.venv\Scripts\Activate.ps1
    - pip install --upgrade pip
    - pip install Pillow pyinstaller
  - Executar em desenvolvimento:
    - python app.py
  - Build com PyInstaller:
    - pyinstaller --clean --noconfirm client-o-preview.spec
    - Saída: .\dist\client-o-preview.exe
  - Limpar artefatos:
    - Remove-Item -Recurse -Force .\build, .\dist
  - Lint/Testes: não configurados.

- C# (cd csharp\ClientOPreview)
  - Requisitos: .NET SDK instalado (net8.0-windows).
  - Build:
    - dotnet build -c Release
  - Executar:
    - dotnet run
  - Saída: .\bin\Release\net8.0-windows\

## Arquitetura de alto nível

- Python (tkinter + Win32)
  - Captura: prioriza `PrintWindow` (com `PW_RENDERFULLCONTENT`) e faz fallback para `ImageGrab`.
  - UI: `App` (janela raiz) gerencia páginas e configurações; `StreamWindow` cria janelas por HWND; `LiveThumbnail` roda thread de captura e agenda updates na UI.
  - Persistência: `%APPDATA%/client-o-preview/settings.json` (ou `python/settings.json` como fallback). Layout por PID quando "Unique layout" está ativo.

- C# (WPF + DWM)
  - Captura/preview: `DwmRegisterThumbnail`/`DwmUpdateThumbnailProperties` para exibir a janela origem dentro de um `Window` WPF (não sofre oclusão por outras janelas).
  - UI: `MainWindow` lista janelas e abre múltiplos `StreamWindow`; cada `StreamWindow` ajusta topmost/local e notifica cliques para focar a janela real.
  - P/Invoke: `NativeMethods` encapsula chamadas a `user32.dll` e `dwmapi.dll`. `WindowEnumerator` aplica filtros (visível, não minimizada, sem owner, com título).

## Notas
- Projeto específico de Windows.
