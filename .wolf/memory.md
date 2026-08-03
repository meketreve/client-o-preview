# Memory

> Chronological action log. Hooks and AI append to this file automatically.
> Old sessions are consolidated by the daemon weekly.

## 2026-08-02 — Region Focus

- Spec veio de conversa no Discord (Epic Suicide × meketreve): mostrar só um pedaço da janela do cliente (painel de drones / capacitor), com pontos pré-definidos (cantos + centro) **e** área ajustável, salva pelo nome do piloto.
- Criados: `RegionPickerWindow.xaml(.cs)`, `RegionOverlayWindow.xaml(.cs)`, `Views/RegionPage.xaml(.cs)`.
- Editados: `csharp/Models/Settings.cs` (`RegionPreset`, `RegionSettings`), `csharp/Services/SettingsService.cs` (bloco `regions`), `StreamWindow.xaml(.cs)` (crop via `rcSource`, letterbox, badge, botão ▣, `FitToRegion`), `MainWindow.xaml(.cs)` (nav + wiring + reaplicação ao abrir stream), `README.md`.
- Fix durante o desenvolvimento: aliases `using` por causa do WinForms habilitado; overlay passou a guardar o retângulo normalizado como fonte da verdade.
- `~/.dotnet/dotnet build` → Build succeeded, 0 warnings.
