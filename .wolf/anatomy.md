# anatomy.md

> Auto-maintained by OpenWolf. Last scanned: 2026-08-03T02:52:03.221Z
> Files: 44 tracked | Anatomy hits: 0 | Misses: 0

## ./

- `.gitattributes` — Git attributes (~18 tok)
- `.gitignore` — Git ignore rules (~155 tok)
- `App.xaml` (~72 tok)
- `App.xaml.cs` — Class: App (~224 tok)
- `build.bat` (~195 tok)
- `CLAUDE.md` — OpenWolf (~57 tok)
- `ClientOPreview.csproj` (~134 tok)
- `MainWindow.xaml` (~333 tok)
- `MainWindow.xaml.cs` — MainWindow: ForceClose (~6667 tok)
- `README.md` — Project documentation (~1190 tok)
- `RegionOverlayWindow.xaml` — transparent selection layer over the DWM thumbnail (~302 tok)
- `RegionOverlayWindow.xaml.cs` — RegionOverlayWindow: SetNormalizedSelection, drag/resize handles (~2299 tok)
- `RegionPickerWindow.xaml` — region picker UI: live preview, quick anchors, result box (~1204 tok)
- `RegionPickerWindow.xaml.cs` — RegionPickerWindow: RegionSaved, RefreshPreview, SourceRect, SyncOverlay (~2451 tok)
- `StreamWindow.xaml` (~488 tok)
- `StreamWindow.xaml.cs` — StreamWindow: SetOpacity, SetTitleFontSize, SetHighlightColor, SetActiveState + 5 more (~2993 tok)
- `TrayHelper.cs` — TrayHelper: Ensure, MinimizeToTray (~406 tok)
- `WARP.md` — WARP.md (~257 tok)

## .claude/

- `settings.json` (~514 tok)
- `settings.local.json` (~28 tok)

## .claude/commands/

- `reframe.md` — Mode: migrate [framework] (~551 tok)
- `security-audit.md` — Layer 1 — Dependencies (~510 tok)

## .claude/rules/

- `openwolf.md` (~328 tok)

## Models/

- `WindowItem.cs` — Class: WindowItem (~63 tok)

## Native/

- `NativeMethods.cs` — Class: NativeMethods (~1222 tok)

## Services/

- `WindowEnumerator.cs` — Class: WindowEnumerator (~361 tok)

## Views/

- `AboutPage.xaml` — Declares or (~139 tok)
- `AboutPage.xaml.cs` — Class: AboutPage (~233 tok)
- `ClientsPage.xaml` (~306 tok)
- `ClientsPage.xaml.cs` — ClientsPage: SetWindows (~323 tok)
- `GeneralPage.xaml` (~469 tok)
- `GeneralPage.xaml.cs` — GeneralPage: LoadFrom (~479 tok)
- `HotkeysPage.xaml` (~1633 tok)
- `HotkeysPage.xaml.cs` — ThumbnailOption: LoadFrom, UpdateOpenThumbnails (~2348 tok)
- `OverlayPage.xaml` (~94 tok)
- `OverlayPage.xaml.cs` — Class: OverlayPage (~52 tok)
- `RegionPage.xaml` — Region Focus page: stream list, preset combo, presets list (~623 tok)
- `RegionPage.xaml.cs` — RegionPage: SetStreams, SetPresets + StreamEntry/PresetEntry (~1035 tok)
- `ThumbnailPage.xaml` (~762 tok)
- `ThumbnailPage.xaml.cs` — ThumbnailPage: ThumbnailArgs, LoadFrom (~752 tok)
- `ZoomPage.xaml` (~630 tok)
- `ZoomPage.xaml.cs` — ZoomPage: LoadFrom (~331 tok)

## csharp/Models/

- `Settings.cs` — SettingsData: Clone, UpsertPreset, RemovePreset (~1045 tok)

## csharp/Services/

- `SettingsService.cs` — SettingsService: GetSettings, SaveSettings (~3477 tok)
