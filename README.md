# client-o-preview

Aplicativo desktop em Python (tkinter) para exibir miniaturas ao vivo de janelas (HWND) no Windows. Usa APIs Win32 via `ctypes` (user32/gdi32) e Pillow para captura e renderização. Não modifica a interface de outros aplicativos nem envia entradas — apenas mostra prévias.

## Requisitos
- Windows (somente) com interface gráfica ativa
- Python 3 com Tkinter disponível
- Pip para instalar dependências

## Instalação
```powershell
# (opcional) criar e ativar um ambiente virtual
python -m venv .venv
.\.venv\Scripts\Activate.ps1

# atualizar pip e instalar dependências
pip install --upgrade pip
pip install Pillow pyinstaller
```

## Execução (desenvolvimento)
```powershell
python app.py
```

## Build (empacotar executável)
Este projeto usa PyInstaller e já inclui `client-o-preview.spec`.
```powershell
# gerar executável GUI em .\dist\client-o-preview.exe
pyinstaller --clean --noconfirm client-o-preview.spec

# limpar artefatos de build (opcional)
Remove-Item -Recurse -Force .\build, .\dist
```

## Configuração e persistência
As preferências são salvas automaticamente em:
- `%APPDATA%/client-o-preview/settings.json` (fallback: `settings.json` ao lado do script)

Estrutura principal:
- `general`
  - `minimize_to_tray`: minimizar para bandeja (não implementado totalmente)
  - `track_locations`: salvar/restaurar posição/tamanho das janelas de preview
  - `hide_active_preview`: esconde o preview da janela que estiver ativa
  - `minimize_inactive`: ao focar um preview, minimiza outros clientes
  - `previews_topmost`: prévias “sempre no topo” por padrão
  - `hide_when_not_active`: esconde todas as prévias quando o cliente alvo não estiver ativo
  - `unique_layout`: salva layout por processo (`pid:PID`) em vez de um layout `default`
- `thumbnail`
  - `width`, `height`: tamanho base das miniaturas
  - `opacity_pct`: opacidade das janelas de preview (20–100)
- `layouts`: geometrias por chave (ex.: `pid:1234` ou `default`)

## Uso
1. Inicie o app: `python app.py`.
2. Aba “Active Clients”:
   - Clique em “Refresh” para listar janelas topo-de-linha válidas.
   - Selecione uma ou mais janelas e clique em “Open streams”. Cada uma abre um `Toplevel` com a miniatura ao vivo.
3. Interações no preview:
   - Clique no preview para focar a janela real (restaura se estiver minimizada). Se `minimize_inactive` estiver ligado, outros clientes são minimizados.
   - Ajuste “Sempre ao topo” por preview na própria janela, ou globalmente em “General”.
   - Em “Thumbnail” altere opacidade, largura e altura; aplica-se às janelas abertas e persiste no settings.

## Limitações conhecidas
- Windows apenas (usa `user32`/`gdi32`).
- Algumas janelas podem não suportar `PrintWindow` (resultando em imagem preta). O app então tenta `ImageGrab` (que pode sofrer oclusão por outras janelas).
- Janelas minimizadas não são capturadas via fallback de tela; o app tenta restaurá-las antes de focar/capturar.

## Créditos / Sobre
- Nome do app: client-o-preview (About mostra “client-o-preview 1.0”).
- “This program does NOT modify game interface or broadcast inputs. It only shows live previews.”
