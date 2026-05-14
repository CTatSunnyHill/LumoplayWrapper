# Game Access Model

## Rule (per-category gating, strict)

A non-admin user can see a game if and only if, for every category in which the user has at least one allowed tag:

- The game has at least one tag in that category, **AND**
- That tag is in the user's `AllowedTagIds`.

Categories the user has **no** allowed tags in do not restrict visibility.

Admins (`Role == "Admin"`) bypass all gating entirely.

### Examples

| User allowed tags | Game tags | Visible? | Reason |
|---|---|---|---|
| Floor (Location), Easy (Difficulty) | Floor, Easy | ✅ | Matches both categories |
| Floor (Location), Easy (Difficulty) | Floor, Hard | ❌ | Difficulty mismatch |
| Floor (Location), Easy (Difficulty) | Wall, Easy | ❌ | Location mismatch |
| Floor (Location), Easy (Difficulty) | (no tags) | ❌ | Strict: game missing required categories |
| (no allowed tags) | (any) | ✅ | No category gating applies |
| Easy (Difficulty only) | Floor | ❌ | Game has no Difficulty tag — strict failure |

---

## Surfaces where filtering is applied

| Endpoint | Behavior |
|---|---|
| `GET /api/Games` | List filtered to visible games only |
| `GET /api/Games/{gameId}` | 404 if game is forbidden (existence not leaked) |
| `GET /api/Devices` | Device stays visible; `currentGame` stripped if forbidden |
| `GET /api/Devices/{ip}` | Device visible if location allowed; `currentGame` stripped if game forbidden |
| `GET /api/Playlists` | Playlist visible; forbidden games stripped from `Games` list |
| `GET /api/Playlists/{id}` | Playlist visible if user can see it; forbidden games stripped from `Games` list |
| `POST /api/Playlists/{id}/add-game-to-playlist/{gameId}` | 403 if game forbidden |
| `POST /api/Playback/play-game/{ip}/game/{gameId}` | 403 if game forbidden |
| `POST /api/Playback/play-playlist/{ip}/{playlistId}` | 403 if first game in playlist is forbidden |
| `POST /api/Playback/playlist/next-game/{ip}` | 403 if next game is forbidden |
| `POST /api/Playback/playlist/previous-game/{ip}` | 403 if previous game is forbidden |

---

## Session invalidation (SessionVersion)

Every user document stores an integer `SessionVersion`. This value is embedded as a JWT claim on login.

**What triggers a version bump:**
- `PUT /api/Users/{id}` — any field change (role, locations, tags)
- `DELETE /api/Tag/{id}` — auto-strips the tag from all users who held it and bumps their version

**How it's enforced:**
`SessionVersionMiddleware` runs on every authenticated request. It compares the JWT's `SessionVersion` claim to the current DB value. If they differ, the request is rejected with **401**. The Flutter client's `ApiClient` detects the 401 and fires a logout callback, returning the user to the login screen.

---

## Cascading behaviors

- Editing a user (any field) bumps their `SessionVersion` → forces re-auth on next request
- Deleting a tag auto-strips it from all users' `AllowedTagIds` and bumps affected users' `SessionVersion`
- Deleting a tag is blocked if any game still references it
