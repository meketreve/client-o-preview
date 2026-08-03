# anatomy.md

> Auto-maintained by OpenWolf. Last scanned: 2026-08-03T03:38:02.210Z
> Files: 45 tracked | Anatomy hits: 0 | Misses: 0

## ./

- `.gitattributes` — Git attributes (~18 tok)
- `.gitignore` — Git ignore rules (~203 tok)
- `App.xaml` (~72 tok)
- `App.xaml.cs` — Class: App (~237 tok)
- `build.bat` (~195 tok)
- `CLAUDE.md` — OpenWolf (~57 tok)
- `ClientOPreview.csproj` (~134 tok)
- `MainWindow.xaml` (~369 tok)
- `MainWindow.xaml.cs` — MainWindow: ForceClose (~7589 tok)
- `README.md` — Project documentation (~1454 tok)
- `RegionOverlayWindow.xaml` — transparent selection layer over the DWM thumbnail (~302 tok)
- `RegionOverlayWindow.xaml.cs` — RegionOverlayWindow: SetNormalizedSelection, drag/resize handles (~2299 tok)
- `RegionPickerWindow.xaml` (~1221 tok)
- `RegionPickerWindow.xaml.cs` — Returns true when the typed name belongs to another, already saved preset. (~2639 tok)
- `StreamWindow.xaml` (~504 tok)
- `StreamWindow.xaml.cs` — StreamWindow: SetOpacity, SetTitleFontSize, SetHighlightColor, SetActiveState + 5 more (~2993 tok)
- `TrayHelper.cs` — TrayHelper: Ensure, MinimizeToTray (~475 tok)
- `WARP.md` — WARP.md (~257 tok)

## .claude/

- `settings.json` (~514 tok)
- `settings.local.json` (~28 tok)

## .claude/commands/

- `reframe.md` — Mode: migrate [framework] (~551 tok)
- `security-audit.md` — Layer 1 — Dependencies (~510 tok)

## .claude/rules/

- `openwolf.md` (~328 tok)

## Localization/

- `Loc.cs` — Raised after the language changed, for code-built strings that XAML cannot bind. (~3986 tok)

## Models/

- `WindowItem.cs` — Class: WindowItem (~63 tok)

## Native/

- `NativeMethods.cs` — Class: NativeMethods (~1222 tok)

## Services/

- `WindowEnumerator.cs` — Class: WindowEnumerator (~361 tok)

## Views/

- `AboutPage.xaml` (~143 tok)
- `AboutPage.xaml.cs` — Class: AboutPage (~233 tok)
- `ClientsPage.xaml` (~362 tok)
- `ClientsPage.xaml.cs` — ClientsPage: SetWindows (~323 tok)
- `HotkeysPage.xaml` (~1640 tok)
- `HotkeysPage.xaml.cs` — ThumbnailOption: LoadFrom, UpdateOpenThumbnails (~2461 tok)
- `LanguagePage.xaml` (~276 tok)
- `LanguagePage.xaml.cs` — LanguagePage: LoadFrom (~244 tok)
- `OverlayPage.xaml` (~110 tok)
- `OverlayPage.xaml.cs` — Class: OverlayPage (~52 tok)
- `RegionPage.xaml` (~751 tok)
- `RegionPage.xaml.cs` — StreamEntry: SetStreams, SetPresets (~1331 tok)
- `ThumbnailPage.xaml` (~1306 tok)
- `ThumbnailPage.xaml.cs` — ThumbnailPage: ThumbnailArgs, LoadFrom (~1105 tok)
- `ZoomPage.xaml` (~628 tok)
- `ZoomPage.xaml.cs` — ZoomPage: LoadFrom (~331 tok)

## csharp/Models/

- `Settings.cs` — SettingsData: Clone, UpsertPreset, RemovePreset (~1145 tok)

## csharp/Services/

- `SettingsService.cs` — Class: SettingsService (~3626 tok)
