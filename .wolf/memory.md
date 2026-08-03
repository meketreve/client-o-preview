# Memory

> Chronological action log. Hooks and AI append to this file automatically.
> Old sessions are consolidated by the daemon weekly.

## 2026-08-02 — Region Focus

- Spec veio de conversa no Discord (Epic Suicide × meketreve): mostrar só um pedaço da janela do cliente (painel de drones / capacitor), com pontos pré-definidos (cantos + centro) **e** área ajustável, salva pelo nome do piloto.
- Criados: `RegionPickerWindow.xaml(.cs)`, `RegionOverlayWindow.xaml(.cs)`, `Views/RegionPage.xaml(.cs)`.
- Editados: `csharp/Models/Settings.cs` (`RegionPreset`, `RegionSettings`), `csharp/Services/SettingsService.cs` (bloco `regions`), `StreamWindow.xaml(.cs)` (crop via `rcSource`, letterbox, badge, botão ▣, `FitToRegion`), `MainWindow.xaml(.cs)` (nav + wiring + reaplicação ao abrir stream), `README.md`.
- Fix durante o desenvolvimento: aliases `using` por causa do WinForms habilitado; overlay passou a guardar o retângulo normalizado como fonte da verdade.
- `~/.dotnet/dotnet build` → Build succeeded, 0 warnings.

## 2026-08-02 — Release v0.6.0

- Merge de `feat/region-focus` em `main` (`2338678`), `.wolf/`+`.claude/`+`CLAUDE.md` versionados (token do dashboard e settings.local ignorados).
- Versão deixou de ser texto chumbado no `AboutPage.xaml`: `<Version>0.6.0</Version>` no csproj + leitura do assembly (`03916ec`). Antes mostrava 0.5.0 mesmo depois do commit "v0.5.1".
- Tag `v0.6.0` + release no GitHub com o `.exe` single-file (framework-dependent, precisa do .NET 8 Desktop Runtime).
- `openwolf update` (já estava em 2.0.1, hooks refeitos) + `bin`/`obj`/`publish` adicionados aos excludes do anatomy scan: 181 → 44 arquivos indexados.
