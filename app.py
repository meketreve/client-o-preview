import ctypes
from ctypes import wintypes
import math
import tkinter as tk
from tkinter import messagebox
from PIL import ImageGrab, Image, ImageTk
import threading
import time
import json
import os
import sys

user32 = ctypes.WinDLL("user32", use_last_error=True)
gdi32 = ctypes.WinDLL("gdi32", use_last_error=True)

# Estrutura para retângulo
if not hasattr(wintypes, 'RECT'):
    class RECT(ctypes.Structure):
        _fields_ = [("left", ctypes.c_long),
                    ("top", ctypes.c_long),
                    ("right", ctypes.c_long),
                    ("bottom", ctypes.c_long)]
else:
    RECT = wintypes.RECT

# Prototypes
EnumWindowsProc = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
user32.EnumWindows.argtypes = [EnumWindowsProc, wintypes.LPARAM]
user32.EnumWindows.restype = wintypes.BOOL

user32.IsWindowVisible.argtypes = [wintypes.HWND]
user32.IsWindowVisible.restype = wintypes.BOOL

# Validate HWNDs
user32.IsWindow.argtypes = [wintypes.HWND]
user32.IsWindow.restype = wintypes.BOOL

user32.GetWindowTextLengthW.argtypes = [wintypes.HWND]
user32.GetWindowTextLengthW.restype = ctypes.c_int

user32.GetWindowTextW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
user32.GetWindowTextW.restype = ctypes.c_int

user32.GetWindow.argtypes = [wintypes.HWND, ctypes.c_uint]
user32.GetWindow.restype = wintypes.HWND
GW_OWNER = 4

# Minimized?
user32.IsIconic = user32.IsIconic
user32.IsIconic.argtypes = [wintypes.HWND]
user32.IsIconic.restype = wintypes.BOOL

# Foreground window / PID
user32.GetForegroundWindow.argtypes = []
user32.GetForegroundWindow.restype = wintypes.HWND
user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
user32.GetWindowThreadProcessId.restype = wintypes.DWORD

user32.GetWindowRect.argtypes = [wintypes.HWND, ctypes.POINTER(RECT)]
user32.GetWindowRect.restype = wintypes.BOOL

# Ativar janelas
user32.SetForegroundWindow.argtypes = [wintypes.HWND]
user32.SetForegroundWindow.restype = wintypes.BOOL
user32.ShowWindow.argtypes = [wintypes.HWND, ctypes.c_int]
user32.ShowWindow.restype = wintypes.BOOL

# DC helpers
user32.GetWindowDC.argtypes = [wintypes.HWND]
user32.GetWindowDC.restype = wintypes.HDC
user32.ReleaseDC.argtypes = [wintypes.HWND, wintypes.HDC]
user32.ReleaseDC.restype = ctypes.c_int

# GDI prototypes
gdi32.CreateCompatibleDC.argtypes = [wintypes.HDC]
gdi32.CreateCompatibleDC.restype = wintypes.HDC

gdi32.CreateCompatibleBitmap.argtypes = [wintypes.HDC, ctypes.c_int, ctypes.c_int]
gdi32.CreateCompatibleBitmap.restype = wintypes.HANDLE

gdi32.SelectObject.argtypes = [wintypes.HDC, wintypes.HANDLE]
gdi32.SelectObject.restype = wintypes.HANDLE

gdi32.DeleteObject.argtypes = [wintypes.HANDLE]
gdi32.DeleteObject.restype = wintypes.BOOL

gdi32.DeleteDC.argtypes = [wintypes.HDC]
gdi32.DeleteDC.restype = wintypes.BOOL

gdi32.GetDIBits.argtypes = [wintypes.HDC, wintypes.HANDLE, wintypes.UINT, wintypes.UINT, wintypes.LPVOID, wintypes.LPVOID, wintypes.UINT]
gdi32.GetDIBits.restype = ctypes.c_int

# PrintWindow
user32.PrintWindow.argtypes = [wintypes.HWND, wintypes.HDC, wintypes.UINT]
user32.PrintWindow.restype = wintypes.BOOL

SW_RESTORE = 9
SW_MINIMIZE = 6
SW_SHOW = 5
PW_CLIENTONLY = 0x00000001
PW_RENDERFULLCONTENT = 0x00000002
BI_RGB = 0
DIB_RGB_COLORS = 0

# Util
def get_window_rect(hwnd):
    """Obter coordenadas da janela"""
    rect = RECT()
    if user32.GetWindowRect(wintypes.HWND(hwnd), ctypes.byref(rect)):
        return (rect.left, rect.top, rect.right, rect.bottom)
    return None

class RGBQUAD(ctypes.Structure):
    _fields_ = [
        ("rgbBlue", ctypes.c_ubyte),
        ("rgbGreen", ctypes.c_ubyte),
        ("rgbRed", ctypes.c_ubyte),
        ("rgbReserved", ctypes.c_ubyte),
    ]

class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [
        ("biSize", wintypes.DWORD),
        ("biWidth", ctypes.c_long),
        ("biHeight", ctypes.c_long),
        ("biPlanes", wintypes.WORD),
        ("biBitCount", wintypes.WORD),
        ("biCompression", wintypes.DWORD),
        ("biSizeImage", wintypes.DWORD),
        ("biXPelsPerMeter", ctypes.c_long),
        ("biYPelsPerMeter", ctypes.c_long),
        ("biClrUsed", wintypes.DWORD),
        ("biClrImportant", wintypes.DWORD),
    ]

class BITMAPINFO(ctypes.Structure):
    _fields_ = [
        ("bmiHeader", BITMAPINFOHEADER),
        ("bmiColors", RGBQUAD * 1),
    ]


def capture_via_printwindow(hwnd):
    bbox = get_window_rect(hwnd)
    if not bbox:
        return None
    left, top, right, bottom = bbox
    width = max(1, right - left)
    height = max(1, bottom - top)

    hdc_window = user32.GetWindowDC(wintypes.HWND(hwnd))
    if not hdc_window:
        return None
    hdc_mem = gdi32.CreateCompatibleDC(hdc_window)
    if not hdc_mem:
        user32.ReleaseDC(wintypes.HWND(hwnd), hdc_window)
        return None
    hbm = gdi32.CreateCompatibleBitmap(hdc_window, width, height)
    if not hbm:
        gdi32.DeleteDC(hdc_mem)
        user32.ReleaseDC(wintypes.HWND(hwnd), hdc_window)
        return None
    old = gdi32.SelectObject(hdc_mem, hbm)
    try:
        # Tentar full content; se falhar, tentar padrão
        ok = user32.PrintWindow(wintypes.HWND(hwnd), hdc_mem, PW_RENDERFULLCONTENT)
        if not ok:
            ok = user32.PrintWindow(wintypes.HWND(hwnd), hdc_mem, 0)
        if not ok:
            return None
        bmi = BITMAPINFO()
        ctypes.memset(ctypes.byref(bmi), 0, ctypes.sizeof(bmi))
        bmi.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
        bmi.bmiHeader.biWidth = width
        bmi.bmiHeader.biHeight = -height  # top-down
        bmi.bmiHeader.biPlanes = 1
        bmi.bmiHeader.biBitCount = 32
        bmi.bmiHeader.biCompression = BI_RGB
        buf_size = width * height * 4
        buf = (ctypes.c_ubyte * buf_size)()
        lines = gdi32.GetDIBits(hdc_mem, hbm, 0, height, ctypes.byref(buf), ctypes.byref(bmi), DIB_RGB_COLORS)
        if lines == 0:
            return None
        # Criar PIL Image a partir do buffer BGRA
        img = Image.frombuffer('RGBA', (width, height), bytes(buf), 'raw', 'BGRA', 0, 1)
        return img
    finally:
        gdi32.SelectObject(hdc_mem, old)
        gdi32.DeleteObject(hbm)
        gdi32.DeleteDC(hdc_mem)
        user32.ReleaseDC(wintypes.HWND(hwnd), hdc_window)

def get_window_title(hwnd):
    length = user32.GetWindowTextLengthW(hwnd)
    if length == 0:
        return ""
    buf = ctypes.create_unicode_buffer(length + 1)
    user32.GetWindowTextW(hwnd, buf, length + 1)
    return buf.value


def is_top_level(hwnd):
    # Visível, válido, sem owner, com título e não minimizada
    if not user32.IsWindow(hwnd):
        return False
    if not user32.IsWindowVisible(hwnd):
        return False
    if user32.IsIconic(hwnd):  # evita janelas minimizadas
        return False
    if user32.GetWindow(hwnd, GW_OWNER):
        return False
    title = get_window_title(hwnd)
    return bool(title.strip())

def enum_windows(exclude_hwnd=None):
    res = []
    seen = set()
    @EnumWindowsProc
    def callback(hwnd, lparam):
        # hwnd here is a c_void_p; compare by value to avoid type mismatches
        hval = hwnd if isinstance(hwnd, int) else hwnd.value
        if exclude_hwnd and hval == int(exclude_hwnd):
            return True
        if is_top_level(hwnd):
            if hval not in seen:
                seen.add(hval)
                title = get_window_title(hwnd)
                res.append((hval, title))
        return True
    user32.EnumWindows(callback, 0)
    return res

class LiveThumbnail:
    def __init__(self, hwnd, canvas, update_interval=0.1):
        self.hwnd = hwnd
        self.canvas = canvas
        self.update_interval = update_interval
        self.canvas_item = None
        self.running = False
        self.thread = None
        self.photo_image = None
        
    def start(self, x, y, width, height):
        """Iniciar/atualizar captura ao vivo"""
        self.x = x
        self.y = y
        self.width = width
        self.height = height
        if self.running:
            # Já rodando: próxima iteração redesenhará com nova geometria
            return
        self.running = True
        self.thread = threading.Thread(target=self._update_loop, daemon=True)
        self.thread.start()
        
    def stop(self):
        """Parar captura"""
        self.running = False
        if self.thread:
            self.thread.join(timeout=1)
        if self.canvas_item:
            self.canvas.delete(self.canvas_item)
            self.canvas_item = None
            
    def _capture_window(self):
        """Capturar janela como imagem (prioriza PrintWindow para evitar oclusão)"""
        try:
            if not user32.IsWindow(wintypes.HWND(self.hwnd)):
                return None
            # Tentar PrintWindow (não depende da janela estar visível por trás de outras)
            img = capture_via_printwindow(self.hwnd)
            if img is None:
                # Fallback: captura área da tela (pode sofrer oclusão)
                bbox = get_window_rect(self.hwnd)
                if not bbox:
                    return None
                img = ImageGrab.grab(bbox)
            if img:
                img.thumbnail((self.width, self.height), Image.Resampling.LANCZOS)
                return img
        except Exception as e:
            print(f"Erro capturando janela 0x{self.hwnd:X}: {e}")
        return None
        
    def _update_loop(self):
        """Loop de atualização em thread separada"""
        while self.running:
            try:
                img = self._capture_window()
                if img:
                    # Converter para formato Tkinter
                    photo = ImageTk.PhotoImage(img)
                    # Atualizar UI na thread principal
                    self.canvas.after(0, self._update_canvas, photo)
                time.sleep(self.update_interval)
            except Exception as e:
                print(f"Erro no loop de atualização: {e}")
                break
                
    def _update_canvas(self, photo):
        """Atualizar canvas na thread principal"""
        try:
            if self.running:
                if self.canvas_item:
                    self.canvas.delete(self.canvas_item)
                self.canvas_item = self.canvas.create_image(
                    self.x + self.width//2, self.y + self.height//2, 
                    image=photo
                )
                # Manter referência para evitar garbage collection
                self.photo_image = photo
        except Exception as e:
            print(f"Erro atualizando canvas: {e}")

class StreamWindow:
    def __init__(self, app, root, hwnd, title, on_close_cb=None, topmost_default=False, opacity=1.0, default_size=None):
        self.app = app
        self.root = root
        self.hwnd = hwnd
        self.title = title
        self.on_close_cb = on_close_cb

        self.win = tk.Toplevel(root)
        self.win.title(f"Stream: {title}")
        # Tamanho inicial
        if default_size and isinstance(default_size, tuple):
            w0, h0 = default_size
            self.win.geometry(f"{max(120,w0)+16}x{max(90,h0)+48}")
        else:
            self.win.geometry("420x260")
        self.win.protocol("WM_DELETE_WINDOW", self.on_close)

        # Barra de controles compacta
        bar = tk.Frame(self.win)
        bar.pack(fill=tk.X, padx=6, pady=4)
        self.var_topmost = tk.BooleanVar(value=bool(topmost_default))
        tk.Checkbutton(bar, text="Sempre ao topo", variable=self.var_topmost,
                       command=self.on_toggle_topmost).pack(side=tk.LEFT)

        self.canvas = tk.Canvas(self.win, bg="#101010", highlightthickness=0)
        self.canvas.pack(fill=tk.BOTH, expand=True)

        self.thumb = LiveThumbnail(hwnd, self.canvas, update_interval=0.1)
        # Opacidade inicial
        try:
            self.win.attributes('-alpha', float(opacity))
        except Exception:
            pass

        # Interações
        self.win.bind("<Button-1>", self.on_click)
        self.canvas.bind("<Button-1>", self.on_click)
        self.canvas.bind("<Configure>", self.on_resize)
        # Detectar movimentação/tamanho para salvar layout
        self.win.bind("<Configure>", self.on_window_configure)

        # Estado inicial de topmost
        self.on_toggle_topmost()

        # Inicializar captura com tamanho atual
        self.win.update_idletasks()
        w = max(1, self.canvas.winfo_width())
        h = max(1, self.canvas.winfo_height())
        self.thumb.start(0, 0, w, h)

    def on_resize(self, event):
        self.thumb.start(0, 0, max(1, event.width), max(1, event.height))

    def on_click(self, event):
        try:
            # Minimizar demais clientes, se configurado
            if getattr(self.app, 'var_minimize_inactive', None) and self.app.var_minimize_inactive.get():
                for other_hwnd, win in list(self.app.streams.items()):
                    if other_hwnd != self.hwnd and user32.IsWindow(wintypes.HWND(other_hwnd)):
                        try:
                            user32.ShowWindow(wintypes.HWND(other_hwnd), SW_MINIMIZE)
                        except Exception:
                            pass
            if user32.IsIconic(wintypes.HWND(self.hwnd)):
                user32.ShowWindow(wintypes.HWND(self.hwnd), SW_RESTORE)
            user32.SetForegroundWindow(wintypes.HWND(self.hwnd))
        except Exception as e:
            print(f"Erro ativando janela 0x{int(self.hwnd):X}: {e}")

    def on_toggle_topmost(self):
        try:
            self.win.attributes('-topmost', bool(self.var_topmost.get()))
        except Exception:
            pass

    def set_opacity(self, alpha):
        try:
            self.win.attributes('-alpha', float(alpha))
        except Exception:
            pass

    def set_size(self, w, h):
        try:
            self.win.geometry(f"{int(w)+16}x{int(h)+48}")
        except Exception:
            pass

    def on_window_configure(self, event):
        # Salvar layout, se habilitado
        if getattr(self.app, 'var_track_locations', None) and self.app.var_track_locations.get():
            try:
                geom = self.win.winfo_geometry()
                self.app.save_layout_for_hwnd(self.hwnd, geom)
            except Exception:
                pass

    def on_close(self):
        try:
            self.thumb.stop()
        finally:
            try:
                self.win.destroy()
            except Exception:
                pass
            if self.on_close_cb:
                self.on_close_cb(self.hwnd)


class App:
    def __init__(self):
        self.root = tk.Tk()
        self.root.title("client-o-preview")
        # Janela principal mais compacta
        self.root.geometry("520x400")
        # Garante que a janela foi criada de fato antes de capturar o HWND
        self.root.update_idletasks()
        # Garante janela realmente criada e mostrada
        self.root.update()
        self.dest_hwnd = self.root.winfo_id()

        # Estado
        self.windows = []     # [(hwnd, title)]
        self.streams = {}     # hwnd -> StreamWindow
        self.selected_hwnds = []
        self.settings_path = self._compute_settings_path()
        self.layouts = {}

        # Variáveis de configuração (defaults)
        self.var_minimize_to_tray = tk.BooleanVar(value=False)
        self.var_track_locations = tk.BooleanVar(value=True)
        self.var_hide_active_preview = tk.BooleanVar(value=True)
        self.var_minimize_inactive = tk.BooleanVar(value=False)
        self.var_previews_topmost = tk.BooleanVar(value=True)
        self.var_hide_when_not_active = tk.BooleanVar(value=False)
        self.var_unique_layout = tk.BooleanVar(value=True)
        self.var_thumb_width = tk.IntVar(value=384)
        self.var_thumb_height = tk.IntVar(value=216)
        self.var_opacity_pct = tk.IntVar(value=90)  # 90%

        # Carregar settings
        self.load_settings()

        # Layout estilo EVE-O: sidebar + conteúdo
        container = tk.Frame(self.root)
        container.pack(fill=tk.BOTH, expand=True)

        sidebar = tk.Frame(container, bg="#d0d0d0", width=140)
        sidebar.pack(side=tk.LEFT, fill=tk.Y)

        self.content = tk.Frame(container)
        self.content.pack(side=tk.RIGHT, fill=tk.BOTH, expand=True)

        # Botões de navegação
        nav_buttons = [
            ("General", lambda: self.show_page('general')),
            ("Thumbnail", lambda: self.show_page('thumbnail')),
            ("Zoom", lambda: self.show_page('zoom')),
            ("Overlay", lambda: self.show_page('overlay')),
            ("Active Clients", lambda: self.show_page('clients')),
            ("About", lambda: self.show_page('about')),
        ]
        for text, cmd in nav_buttons:
            b = tk.Button(sidebar, text=text, relief=tk.RIDGE, width=16, command=cmd)
            b.pack(fill=tk.X, padx=6, pady=4)

        # Criar páginas
        self.pages = {}
        self._build_pages()
        self.show_page('clients')

        # Timer de checagem de foco
        self.root.after(400, self._check_foreground)

        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

    def refresh_windows(self):
        all_wins = enum_windows(exclude_hwnd=self.dest_hwnd)
        # Ocultar nossas próprias janelas de stream
        self.windows = [(h, t) for (h, t) in all_wins if not t.startswith("Stream:")]
        if hasattr(self, 'listbox_clients'):
            self.listbox_clients.delete(0, tk.END)
            for hwnd, title in self.windows:
                self.listbox_clients.insert(tk.END, f"{title}  (0x{int(hwnd):X})")

    def show_selected(self):
        # Criar janelas para os selecionados (sem fechar as existentes)
        sel = self.listbox_clients.curselection()
        self.selected_hwnds = [self.windows[i][0] for i in sel]
        # evitar duplicatas na seleção
        unique_hwnds = []
        seen = set()
        for h in self.selected_hwnds:
            if h not in seen:
                seen.add(h)
                unique_hwnds.append(h)

        for hwnd in unique_hwnds:
            if hwnd in self.streams:
                # já existe: trazer janela para frente
                try:
                    self.streams[hwnd].win.lift()
                    self.streams[hwnd].win.focus_force()
                except Exception:
                    pass
                continue
            try:
                # Validar janela
                if not user32.IsWindow(wintypes.HWND(hwnd)) or user32.IsIconic(wintypes.HWND(hwnd)):
                    title = next((t for h, t in self.windows if h == hwnd), "(desconhecido)")
                    print(f"Janela inválida/minimizada: '{title}' (0x{int(hwnd):X})")
                    continue
                title = next((t for h, t in self.windows if h == hwnd), f"0x{int(hwnd):X}")
                default_size = (self.var_thumb_width.get(), self.var_thumb_height.get())
                opacity = max(0.2, min(1.0, self.var_opacity_pct.get()/100.0))
                win = StreamWindow(self, self.root, hwnd, title, on_close_cb=self._on_stream_closed,
                                   topmost_default=self.var_previews_topmost.get(),
                                   opacity=opacity, default_size=default_size)
                # Aplicar layout salvo
                self.apply_saved_geometry(hwnd, win)
                self.streams[hwnd] = win
            except Exception as e:
                title = next((t for h, t in self.windows if h == hwnd), "(desconhecido)")
                print(f"Erro criando stream para '{title}' (0x{int(hwnd):X}): {e}")



    def on_close(self):
        # Salvar settings
        self.save_settings()
        # Fechar todas as streams
        try:
            for hwnd, win in list(self.streams.items()):
                try:
                    win.on_close()
                except Exception:
                    pass
        finally:
            self.root.destroy()

    def _on_stream_closed(self, hwnd):
        self.streams.pop(hwnd, None)

    # ====== Pages/UI ======
    def _build_pages(self):
        # General
        pg = tk.Frame(self.content)
        self.pages['general'] = pg
        box = tk.LabelFrame(pg, text="General")
        box.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        tk.Checkbutton(box, text="Minimize to System Tray", variable=self.var_minimize_to_tray,
                       command=self.on_settings_change).pack(anchor='w', padx=8, pady=2)
        tk.Checkbutton(box, text="Track client locations", variable=self.var_track_locations,
                       command=self.on_settings_change).pack(anchor='w', padx=8, pady=2)
        tk.Checkbutton(box, text="Hide preview of active client", variable=self.var_hide_active_preview,
                       command=self.on_settings_change).pack(anchor='w', padx=8, pady=2)
        tk.Checkbutton(box, text="Minimize inactive clients", variable=self.var_minimize_inactive,
                       command=self.on_settings_change).pack(anchor='w', padx=8, pady=2)
        tk.Checkbutton(box, text="Previews always on top", variable=self.var_previews_topmost,
                       command=self.apply_global_topmost).pack(anchor='w', padx=8, pady=2)
        tk.Checkbutton(box, text="Hide previews when client is not active", variable=self.var_hide_when_not_active,
                       command=self.on_settings_change).pack(anchor='w', padx=8, pady=2)
        tk.Checkbutton(box, text="Unique layout for each client", variable=self.var_unique_layout,
                       command=self.on_settings_change).pack(anchor='w', padx=8, pady=2)

        # Thumbnail
        pg = tk.Frame(self.content)
        self.pages['thumbnail'] = pg
        box = tk.LabelFrame(pg, text="Thumbnail")
        box.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        # Opacity
        frm_op = tk.Frame(box)
        frm_op.pack(fill=tk.X, padx=8, pady=6)
        tk.Label(frm_op, text="Opacity").pack(side=tk.LEFT)
        scl = tk.Scale(frm_op, from_=20, to=100, orient=tk.HORIZONTAL, showvalue=True,
                       variable=self.var_opacity_pct, command=lambda e: self.apply_thumbnail_to_streams())
        scl.pack(fill=tk.X, expand=True, padx=8)
        # Width/Height
        frm_sz = tk.Frame(box)
        frm_sz.pack(fill=tk.X, padx=8, pady=6)
        tk.Label(frm_sz, text="Thumbnail Width").grid(row=0, column=0, sticky='w')
        tk.Label(frm_sz, text="Thumbnail Height").grid(row=1, column=0, sticky='w')
        sp_w = tk.Spinbox(frm_sz, from_=160, to=1920, increment=8, width=6, textvariable=self.var_thumb_width,
                          command=self.apply_thumbnail_to_streams)
        sp_h = tk.Spinbox(frm_sz, from_=90, to=1080, increment=8, width=6, textvariable=self.var_thumb_height,
                          command=self.apply_thumbnail_to_streams)
        sp_w.grid(row=0, column=1, sticky='w', padx=6)
        sp_h.grid(row=1, column=1, sticky='w', padx=6)

        # Zoom (placeholder)
        pg = tk.Frame(self.content)
        self.pages['zoom'] = pg
        tk.Label(pg, text="Zoom settings (coming soon)").pack(padx=10, pady=10)

        # Overlay (placeholder)
        pg = tk.Frame(self.content)
        self.pages['overlay'] = pg
        tk.Label(pg, text="Overlay settings (coming soon)").pack(padx=10, pady=10)

        # Active Clients
        pg = tk.Frame(self.content)
        self.pages['clients'] = pg
        tk.Label(pg, text="Select windows:").pack(anchor='w', padx=8, pady=(8,2))
        self.listbox_clients = tk.Listbox(pg, selectmode=tk.EXTENDED, width=48, height=14)
        self.listbox_clients.pack(fill=tk.BOTH, expand=True, padx=8)
        btns = tk.Frame(pg)
        btns.pack(fill=tk.X, padx=8, pady=8)
        tk.Button(btns, text="Refresh", command=self.refresh_windows).pack(side=tk.LEFT)
        tk.Button(btns, text="Open streams", command=self.show_selected).pack(side=tk.RIGHT)

        # About
        pg = tk.Frame(self.content)
        self.pages['about'] = pg
        tk.Label(pg, text="client-o-preview 5.2.0").pack(padx=10, pady=10)
        tk.Label(pg, text="This program does NOT modify game interface or broadcast inputs.\nIt only shows live previews.", justify='left').pack(padx=10)

    def show_page(self, name):
        for p in self.content.pack_slaves():
            p.pack_forget()
        page = self.pages.get(name)
        if page:
            page.pack(fill=tk.BOTH, expand=True)

    # ====== Behavior ======
    def apply_global_topmost(self):
        for win in self.streams.values():
            try:
                win.var_topmost.set(self.var_previews_topmost.get())
                win.on_toggle_topmost()
            except Exception:
                pass
        self.on_settings_change()

    def apply_thumbnail_to_streams(self):
        alpha = max(0.2, min(1.0, self.var_opacity_pct.get()/100.0))
        w = self.var_thumb_width.get()
        h = self.var_thumb_height.get()
        for win in self.streams.values():
            try:
                win.set_opacity(alpha)
                win.set_size(w, h)
            except Exception:
                pass
        self.on_settings_change()

    def _check_foreground(self):
        try:
            fg = user32.GetForegroundWindow()
            if self.var_hide_when_not_active.get():
                tracked = set(self.streams.keys())
                if fg not in tracked and fg != self.root.winfo_id():
                    # esconder todos
                    for win in self.streams.values():
                        try:
                            win.win.withdraw()
                        except Exception:
                            pass
                else:
                    for win in self.streams.values():
                        try:
                            win.win.deiconify()
                        except Exception:
                            pass
            if self.var_hide_active_preview.get():
                for hwnd, win in self.streams.items():
                    try:
                        if hwnd == fg:
                            win.win.withdraw()
                        else:
                            win.win.deiconify()
                    except Exception:
                        pass
        except Exception:
            pass
        finally:
            self.root.after(400, self._check_foreground)

    def on_settings_change(self):
        self.save_settings()

    # ====== Persistência ======
    def _compute_settings_path(self):
        try:
            base = os.environ.get('APPDATA') or os.path.expanduser('~')
            cfgdir = os.path.join(base, 'client-o-preview')
            os.makedirs(cfgdir, exist_ok=True)
            return os.path.join(cfgdir, 'settings.json')
        except Exception:
            return os.path.join(os.path.dirname(__file__), 'settings.json')

    def load_settings(self):
        try:
            if os.path.exists(self.settings_path):
                with open(self.settings_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                gen = data.get('general', {})
                self.var_minimize_to_tray.set(gen.get('minimize_to_tray', self.var_minimize_to_tray.get()))
                self.var_track_locations.set(gen.get('track_locations', self.var_track_locations.get()))
                self.var_hide_active_preview.set(gen.get('hide_active_preview', self.var_hide_active_preview.get()))
                self.var_minimize_inactive.set(gen.get('minimize_inactive', self.var_minimize_inactive.get()))
                self.var_previews_topmost.set(gen.get('previews_topmost', self.var_previews_topmost.get()))
                self.var_hide_when_not_active.set(gen.get('hide_when_not_active', self.var_hide_when_not_active.get()))
                self.var_unique_layout.set(gen.get('unique_layout', self.var_unique_layout.get()))
                th = data.get('thumbnail', {})
                self.var_thumb_width.set(th.get('width', self.var_thumb_width.get()))
                self.var_thumb_height.set(th.get('height', self.var_thumb_height.get()))
                self.var_opacity_pct.set(th.get('opacity_pct', self.var_opacity_pct.get()))
                self.layouts = data.get('layouts', {})
        except Exception:
            pass

    def save_settings(self):
        try:
            data = {
                'general': {
                    'minimize_to_tray': self.var_minimize_to_tray.get(),
                    'track_locations': self.var_track_locations.get(),
                    'hide_active_preview': self.var_hide_active_preview.get(),
                    'minimize_inactive': self.var_minimize_inactive.get(),
                    'previews_topmost': self.var_previews_topmost.get(),
                    'hide_when_not_active': self.var_hide_when_not_active.get(),
                    'unique_layout': self.var_unique_layout.get(),
                },
                'thumbnail': {
                    'width': self.var_thumb_width.get(),
                    'height': self.var_thumb_height.get(),
                    'opacity_pct': self.var_opacity_pct.get(),
                },
                'layouts': self.layouts,
            }
            with open(self.settings_path, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2)
        except Exception:
            pass

    def get_pid_for_hwnd(self, hwnd):
        pid = wintypes.DWORD(0)
        try:
            user32.GetWindowThreadProcessId(wintypes.HWND(hwnd), ctypes.byref(pid))
            return int(pid.value)
        except Exception:
            return None

    def layout_key_for_hwnd(self, hwnd):
        if not self.var_track_locations.get():
            return None
        if self.var_unique_layout.get():
            pid = self.get_pid_for_hwnd(hwnd)
            return f"pid:{pid}" if pid else f"hwnd:{int(hwnd)}"
        return "default"

    def apply_saved_geometry(self, hwnd, stream_window):
        key = self.layout_key_for_hwnd(hwnd)
        if not key:
            return
        geom = self.layouts.get(key)
        if geom:
            try:
                stream_window.win.geometry(geom)
            except Exception:
                pass

    def save_layout_for_hwnd(self, hwnd, geometry_str):
        key = self.layout_key_for_hwnd(hwnd)
        if not key:
            return
        self.layouts[key] = geometry_str
        self.save_settings()

    def run(self):
        self.root.mainloop()

if __name__ == "__main__":
    App().run()
