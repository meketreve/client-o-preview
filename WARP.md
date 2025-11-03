# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Visão geral

Aplicativo desktop em Python (tkinter) para pré-visualização ao vivo de janelas no Windows. Usa Win32 via `ctypes` (user32/gdi32) para enumerar janelas e capturar frames, e Pillow para processamento de imagem. A UI é baseada em `tkinter` com janelas independentes de preview por cliente.

## Comandos úteis (PowerShell)

- Preparar ambiente
  - python -m venv .venv
  - .\.venv\Scripts\Activate.ps1
  - pip install --upgrade pip
  - pip install Pillow pyinstaller

- Executar em desenvolvimento
  - python app.py

- Empacotar (build) com PyInstaller
  - pyinstaller --clean --noconfirm client-o-preview.spec
  - Binário resultante: .\dist\client-o-preview.exe

- Limpar artefatos de build
  - Remove-Item -Recurse -Force .\build, .\dist

- Lint
  - Não há linter configurado neste repositório.

- Testes
  - Não há suíte de testes configurada. (Logo, não há comando para rodar um único teste.)

## Arquitetura de alto nível

- Captura de janelas (Win32 + GDI + Pillow)
  - Enumeração de janelas: `EnumWindows` com filtros (visível, não minimizada, sem owner, com título).
  - Captura prioritária via `PrintWindow` (flag `PW_RENDERFULLCONTENT` com fallback para `0`). Caso falhe, fallback para `ImageGrab.grab` (Pillow) na área do retângulo da janela.
  - Pipeline de bitmap: `GetWindowDC` → `CreateCompatibleDC`/`CreateCompatibleBitmap` → `PrintWindow` → `GetDIBits` → `PIL.Image` em RGBA → `ImageTk.PhotoImage`.

- UI (tkinter)
  - `App` (janela raiz): navegação lateral (General, Thumbnail, Zoom, Overlay, Active Clients, About), lista de janelas ativas, timer periódico (`_check_foreground`) para esconder/mostrar previews conforme foco, aplica configurações globais (topmost, opacidade, tamanho das miniaturas).
  - `StreamWindow` (uma por HWND): `Toplevel` com barra de controles (topmost), `Canvas` para renderização, clique traz a janela real ao foco (e opcionalmente minimiza outras), ajusta opacidade/tamanho, persiste geometria via eventos `Configure`.
  - `LiveThumbnail`: thread dedicada de atualização periódica que captura a imagem e agenda atualização do `Canvas` pela thread principal via `after`.

- Persistência e layout
  - Configurações e layouts em `%APPDATA%/client-o-preview/settings.json` (fallback para `settings.json` ao lado do script).
  - Chave de layout: se "Unique layout" habilitado, por processo (`pid:PID`); caso contrário, `default`.
  - Estrutura salva: `general` (flags de comportamento), `thumbnail` (width/height/opacity_pct), `layouts` (geometrias por chave).

- Empacotamento
  - `client-o-preview.spec` define build GUI (`console=False`), nome `client-o-preview`, `upx=True`. Artefatos em `build/` e `dist/`.

## Notas

- Projeto específico de Windows (usa `user32`/`gdi32`). Não tente executar em macOS/Linux.
