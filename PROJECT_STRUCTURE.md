# Project Structure

## Root
- `MusicWrap/`: app host/startup project.
- `MusicWrap.Core/`: audio engine and playback services.
- `MusicWrap.Data/`: domain models, persistence, and data access.
- `MusicWrap.UI/`: WPF UI layer.

## MusicWrap.UI (high level)
- `Shell/`: app windows, dialogs, and tray integration.
- `Features/`: feature modules by domain (Library, Playback, Playlist, Providers, Settings, Favorites).
- `Shared/`: reusable controls, models, and services used across features.
- `Converters/`, `Helpers/`, `Selectors/`, `Styles/`: UI infrastructure and cross-cutting presentation utilities.
- `Resources/`: embedded icons and UI assets.
