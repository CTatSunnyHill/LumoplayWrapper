# LUMOplay_Remote_Controller

## Route Refactor History

In May 2026, the monolithic `LumoRemoteController` was split into four resource-named
controllers. The old `/api/LumoRemote/*` routes are no longer served.

| New Controller | Base Route | Replaces |
|---|---|---|
| `DevicesController` | `/api/Devices` | `/api/LumoRemote/devices*` |
| `GamesController` | `/api/Games` | `/api/LumoRemote/games*` |
| `PlaylistsController` | `/api/Playlists` | `/api/LumoRemote/playlists*` |
| `PlaybackController` | `/api/Playback` | `/api/LumoRemote/play-game*`, `stop-game*`, `now-playing*`, `play-playlist*`, `playlist/next-game*`, `playlist/previous-game*` |