using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace ClientOPreview.Localization;

// Runtime string table. XAML binds through the indexer ({loc:Tr Key}), so switching the
// language raises Item[] and every bound control re-reads its text without a restart.
public sealed class Loc : INotifyPropertyChanged
{
    public const string English = "en";
    public const string PortugueseBr = "pt-BR";

    public static Loc Instance { get; } = new();

    private static string _lang = English;

    private Loc() { }

    public static string CurrentLanguage => _lang;

    /// <summary>Raised after the language changed, for code-built strings that XAML cannot bind.</summary>
    public static event EventHandler? LanguageChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>WPF's Binding.IndexerName, inlined so this file needs no WPF reference.</summary>
    private const string IndexerName = "Item[]";

    public string this[string key] => Get(key);

    public static string Get(string key)
    {
        var table = _lang == PortugueseBr ? Pt : En;
        if (table.TryGetValue(key, out var value)) return value;
        if (En.TryGetValue(key, out var fallback)) return fallback;
        return key;
    }

    public static string Format(string key, params object[] args) => string.Format(Get(key), args);

    public static void SetLanguage(string? code)
    {
        var normalized = Normalize(code);
        if (normalized == _lang) return;
        _lang = normalized;
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(IndexerName));
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return English;
        return code.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? PortugueseBr : English;
    }

    /// <summary>Used the first time the app runs, before settings.json has a language.</summary>
    public static string SystemDefault() => Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    private static readonly Dictionary<string, string> En = new()
    {
        // Navigation
        ["NavThumbnail"] = "Thumbnail",
        ["NavHotkeys"] = "Hotkeys",
        ["NavZoom"] = "Zoom",
        ["NavRegion"] = "Region Focus",
        ["NavClients"] = "Active Clients",
        ["NavLanguage"] = "Language",
        ["NavAbout"] = "About",

        // Thumbnail page
        ["ThumbTitle"] = "Thumbnail",
        ["ThumbPreviewsSection"] = "Previews",
        ["ThumbTopmost"] = "Previews always on top",
        ["ThumbTopmostOnlyFocused"] = "Only on top while a client is in focus",
        ["ThumbTopmostOnlyFocusedHint"] = "When no client (and no window of this app) is in focus, the previews drop behind other windows.",
        ["ThumbOpacity"] = "Opacity",
        ["ThumbTitleSize"] = "Title Size",
        ["ThumbActiveColor"] = "Active Color",
        ["ThumbWidth"] = "Width",
        ["ThumbHeight"] = "Height",
        ["ThumbApply"] = "Apply",
        ["ThumbGeneralSection"] = "General",
        ["ThumbMinimizeToTray"] = "Minimize to System Tray",
        ["ThumbTrackLocations"] = "Track client locations",
        ["ThumbUniqueLayout"] = "Save individual window positions",
        ["ThumbSnapToGrid"] = "Snap to grid during move",
        ["ThumbGridSize"] = "Grid Size:",

        // Hotkeys page
        ["HotkeysTitle"] = "Hotkeys",
        ["HotkeysEnable"] = "Enable global hotkeys",
        ["HotkeysRegisterFailed"] = "Windows refused these shortcuts — the system or another app already owns them: {0}. Pick another combination.",
        ["HotkeysCycleTitle"] = "Cycle Hotkey",
        ["HotkeysCycleDesc"] = "Press this hotkey to cycle through open thumbnails",
        ["HotkeysModifiers"] = "Modifiers:",
        ["HotkeysKey"] = "Key:",
        ["HotkeysKeyHint"] = "(Click and press a key)",
        ["HotkeysKeyTooltip"] = "Click and press a key to set",
        ["HotkeysDirectTitle"] = "Direct Hotkeys",
        ["HotkeysDirectDesc"] = "Press these hotkeys to activate a specific thumbnail by index",
        ["HotkeysMappings"] = "Hotkey mappings:",
        ["HotkeysOpenThumbnails"] = "Open Thumbnails",
        ["HotkeysNoThumbnails"] = "No thumbnails open",
        ["HotkeysItemLabel"] = "Hotkey {0}:",
        ["HotkeysNone"] = "(None)",

        // Zoom page
        ["ZoomTitle"] = "Zoom Settings",
        ["ZoomResizeOnHover"] = "Resize window on hover",
        ["ZoomInternal"] = "Internal zoom (don't resize window)",
        ["ZoomMagnification"] = "Magnification Factor:",
        ["ZoomCenterX"] = "Centering X (Focus):",
        ["ZoomCenterY"] = "Centering Y (Focus):",
        ["ZoomHint"] = "Adjust centering to focus on specific parts of the window (e.g. top-left for maps).",

        // Region page
        ["RegionTitle"] = "Region Focus",
        ["RegionDesc"] = "Show only one part of a client (drone panel, capacitor, minimap) instead of the whole window.",
        ["RegionOpenPreviews"] = "Open previews:",
        ["RegionRefresh"] = "Refresh list",
        ["RegionDefine"] = "Edit this region…",
        ["RegionClear"] = "Clear region",
        ["RegionSavedPresets"] = "Saved presets:",
        ["RegionDeletePreset"] = "Delete preset",
        ["RegionNewPreset"] = "New preset…",
        ["RegionNewPresetHint"] = "Draw the area once, using the selected preview as the example screen. The preset is then saved and you just pick it for every other client.",
        ["RegionApplyStep"] = "2. Apply a saved preset to the selected preview:",
        ["RegionCreateStep"] = "1. Create the region once:",
        ["RegionFooter"] = "Presets are saved by name (e.g. \"drones\") and reused on any preview. The ▣ button on a preview title bar opens the same picker.",
        ["RegionNone"] = "— none (full window) —",
        ["RegionSelectFirst"] = "Select an open preview first.",
        ["RegionNoExample"] = "Select an open preview to use as the example screen.",
        ["RegionNameTaken"] = "A preset named \"{0}\" already exists. Pick another name — saved presets are never overwritten.",

        // Clients page
        ["ClientsSelectWindows"] = "Select windows:",
        ["ClientsRefresh"] = "Refresh",
        ["ClientsOpenStreams"] = "Open streams",
        ["ClientsCloseSelected"] = "Close selected",
        ["ClientsCloseAll"] = "Close all",

        // Language page
        ["LanguageTitle"] = "Language",
        ["LanguageDesc"] = "Choose the interface language. The change is applied immediately, no restart needed.",

        // About page
        ["AboutTitle"] = "About",
        ["AboutDisclaimer"] = "This program does NOT modify game interface or broadcast inputs. It only shows live previews.",

        // Stream window
        ["StreamRegionTooltip"] = "Focus a region of this window",

        // Region picker
        ["PickerTitle"] = "Region Focus",
        ["PickerPresetName"] = "Preset name",
        ["PickerNameTip"] = "Tip: use the pilot / character name so you can reuse it later.",
        ["PickerQuickAnchors"] = "Quick anchors",
        ["PickerAnchorSize"] = "Anchor size",
        ["PickerFullWindow"] = "Full window",
        ["PickerResult"] = "Result",
        ["PickerLockAspect"] = "Keep crop proportions",
        ["PickerFitWindow"] = "Resize preview to the crop",
        ["PickerSave"] = "Save",
        ["PickerCancel"] = "Cancel",
        ["PickerHint"] = "Drag on the preview to draw the area, drag inside it to move, use the handles to resize.",
        ["PickerNameRequired"] = "Give the preset a name (e.g. the pilot name).",
        ["PickerReadoutOf"] = "of",

        // Tray
        ["TrayOpen"] = "Open",
        ["TrayExit"] = "Exit",
        ["TrayMinimized"] = "Minimized to tray.",

        // Errors
        ["ErrorUnhandled"] = "Unhandled Exception",
        ["ErrorStartup"] = "Startup Error",
    };

    private static readonly Dictionary<string, string> Pt = new()
    {
        // Navegação
        ["NavThumbnail"] = "Miniatura",
        ["NavHotkeys"] = "Atalhos",
        ["NavZoom"] = "Zoom",
        ["NavRegion"] = "Foco de Região",
        ["NavClients"] = "Clientes Ativos",
        ["NavLanguage"] = "Idioma",
        ["NavAbout"] = "Sobre",

        // Página de miniatura
        ["ThumbTitle"] = "Miniatura",
        ["ThumbPreviewsSection"] = "Previews",
        ["ThumbTopmost"] = "Previews sempre no topo",
        ["ThumbTopmostOnlyFocused"] = "Só no topo enquanto um cliente estiver em foco",
        ["ThumbTopmostOnlyFocusedHint"] = "Quando nenhum cliente (nem uma janela deste app) está em foco, as previews ficam atrás das outras janelas.",
        ["ThumbOpacity"] = "Opacidade",
        ["ThumbTitleSize"] = "Tam. título",
        ["ThumbActiveColor"] = "Cor ativa",
        ["ThumbWidth"] = "Largura",
        ["ThumbHeight"] = "Altura",
        ["ThumbApply"] = "Aplicar",
        ["ThumbGeneralSection"] = "Geral",
        ["ThumbMinimizeToTray"] = "Minimizar para a bandeja do sistema",
        ["ThumbTrackLocations"] = "Lembrar a posição das previews",
        ["ThumbUniqueLayout"] = "Salvar a posição de cada janela separadamente",
        ["ThumbSnapToGrid"] = "Alinhar à grade ao mover",
        ["ThumbGridSize"] = "Tamanho da grade:",

        // Página de atalhos
        ["HotkeysTitle"] = "Atalhos",
        ["HotkeysEnable"] = "Ativar atalhos globais",
        ["HotkeysRegisterFailed"] = "O Windows recusou estes atalhos — o sistema ou outro app já usa: {0}. Escolha outra combinação.",
        ["HotkeysCycleTitle"] = "Atalho de ciclo",
        ["HotkeysCycleDesc"] = "Pressione este atalho para alternar entre as previews abertas",
        ["HotkeysModifiers"] = "Modificadores:",
        ["HotkeysKey"] = "Tecla:",
        ["HotkeysKeyHint"] = "(Clique e pressione uma tecla)",
        ["HotkeysKeyTooltip"] = "Clique e pressione uma tecla para definir",
        ["HotkeysDirectTitle"] = "Atalhos diretos",
        ["HotkeysDirectDesc"] = "Pressione estes atalhos para ativar uma preview específica pelo índice",
        ["HotkeysMappings"] = "Mapeamento dos atalhos:",
        ["HotkeysOpenThumbnails"] = "Previews abertas",
        ["HotkeysNoThumbnails"] = "Nenhuma preview aberta",
        ["HotkeysItemLabel"] = "Atalho {0}:",
        ["HotkeysNone"] = "(Nenhum)",

        // Página de zoom
        ["ZoomTitle"] = "Configurações de zoom",
        ["ZoomResizeOnHover"] = "Redimensionar a janela ao passar o mouse",
        ["ZoomInternal"] = "Zoom interno (não redimensiona a janela)",
        ["ZoomMagnification"] = "Fator de ampliação:",
        ["ZoomCenterX"] = "Centralização X (foco):",
        ["ZoomCenterY"] = "Centralização Y (foco):",
        ["ZoomHint"] = "Ajuste a centralização para focar em partes específicas da janela (ex.: canto superior esquerdo para mapas).",

        // Página de região
        ["RegionTitle"] = "Foco de Região",
        ["RegionDesc"] = "Mostre só uma parte do cliente (painel de drones, capacitor, minimapa) em vez da janela inteira.",
        ["RegionOpenPreviews"] = "Previews abertas:",
        ["RegionRefresh"] = "Atualizar lista",
        ["RegionDefine"] = "Editar esta região…",
        ["RegionClear"] = "Limpar região",
        ["RegionSavedPresets"] = "Presets salvos:",
        ["RegionDeletePreset"] = "Apagar preset",
        ["RegionNewPreset"] = "Novo preset…",
        ["RegionNewPresetHint"] = "Desenhe a área uma vez, usando a preview selecionada como tela de exemplo. O preset fica salvo e depois é só escolher ele em cada outro cliente.",
        ["RegionApplyStep"] = "2. Aplique um preset salvo na preview selecionada:",
        ["RegionCreateStep"] = "1. Crie a região uma vez:",
        ["RegionFooter"] = "Os presets são salvos por nome (ex.: \"drones\") e reaproveitados em qualquer preview. O botão ▣ na barra de título da preview abre o mesmo seletor.",
        ["RegionNone"] = "— nenhuma (janela inteira) —",
        ["RegionSelectFirst"] = "Selecione uma preview aberta primeiro.",
        ["RegionNoExample"] = "Selecione uma preview aberta para usar como tela de exemplo.",
        ["RegionNameTaken"] = "Já existe um preset chamado \"{0}\". Escolha outro nome — presets salvos nunca são sobrescritos.",

        // Página de clientes
        ["ClientsSelectWindows"] = "Selecione as janelas:",
        ["ClientsRefresh"] = "Atualizar",
        ["ClientsOpenStreams"] = "Abrir previews",
        ["ClientsCloseSelected"] = "Fechar selecionadas",
        ["ClientsCloseAll"] = "Fechar todas",

        // Página de idioma
        ["LanguageTitle"] = "Idioma",
        ["LanguageDesc"] = "Escolha o idioma da interface. A mudança é aplicada na hora, sem reiniciar.",

        // Página sobre
        ["AboutTitle"] = "Sobre",
        ["AboutDisclaimer"] = "Este programa NÃO modifica a interface do jogo nem replica comandos. Ele apenas mostra previews ao vivo.",

        // Janela de preview
        ["StreamRegionTooltip"] = "Focar uma região desta janela",

        // Seletor de região
        ["PickerTitle"] = "Foco de Região",
        ["PickerPresetName"] = "Nome do preset",
        ["PickerNameTip"] = "Dica: use o nome do piloto / personagem para reaproveitar depois.",
        ["PickerQuickAnchors"] = "Âncoras rápidas",
        ["PickerAnchorSize"] = "Tamanho da âncora",
        ["PickerFullWindow"] = "Janela inteira",
        ["PickerResult"] = "Resultado",
        ["PickerLockAspect"] = "Manter as proporções do recorte",
        ["PickerFitWindow"] = "Ajustar a preview ao recorte",
        ["PickerSave"] = "Salvar",
        ["PickerCancel"] = "Cancelar",
        ["PickerHint"] = "Arraste sobre a preview para desenhar a área, arraste dentro dela para mover, use as alças para redimensionar.",
        ["PickerNameRequired"] = "Dê um nome ao preset (ex.: o nome do piloto).",
        ["PickerReadoutOf"] = "de",

        // Bandeja
        ["TrayOpen"] = "Abrir",
        ["TrayExit"] = "Sair",
        ["TrayMinimized"] = "Minimizado para a bandeja.",

        // Erros
        ["ErrorUnhandled"] = "Exceção não tratada",
        ["ErrorStartup"] = "Erro na inicialização",
    };
}
