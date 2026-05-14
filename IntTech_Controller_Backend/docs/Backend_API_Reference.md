# Backend API Reference

Base path: `/api`

All endpoints require a valid JWT Bearer token unless marked **(anonymous)**.

---

## Authentication

### POST /api/Auth/login (anonymous)

Returns a JWT on successful credential verification.

**Body:**
```json
{ "username": "string", "password": "string" }
```

**Response:**
```json
{ "token": "<jwt>" }
```

JWT claims included: `NameIdentifier` (user ID), `Name` (username), `Role`, `SessionVersion`, `AllowedLocationsIds` (JSON array), `AllowedTagIds` (JSON array).

---

## Users (Admin only)

### GET /api/Users

Returns all user accounts (passwords excluded).

### POST /api/Users

Creates a new user account.

**Body:**
```json
{
  "username": "string",
  "password": "string",
  "role": "Admin" | "User",
  "allowedLocationsIds": ["<location_id>", ...],
  "allowedTagIds": ["<tag_id>", ...]
}
```

### DELETE /api/Users/{id}

Deletes a user and cascades deletion to all playlists they owned. Cannot delete the master admin (`username == "admin"`).

### GET /api/Users/{userId}/playlist-impact

Returns counts of personal and default playlists that would be deleted along with the user. Used to populate the delete confirmation dialog.

### PUT /api/Users/{id}

Partial update of a user's role, location access, and tag access. Bumps `SessionVersion` on any successful change, forcing re-authentication.

**Body (all fields optional):**
```json
{
  "role": "Admin" | "User",
  "allowedLocationsIds": ["<location_id>", ...],
  "allowedTagIds": ["<tag_id>", ...]
}
```

**Behavior:**
- Cannot demote the master admin (`username == "admin"`).
- Empty arrays (`[]`) clear the respective allow-list.
- If no fields change, `SessionVersion` is not bumped.
- Returns `{ "message": "User updated", "sessionVersion": <new_version> }`.

---

## Games

### GET /api/Games

Returns all games visible to the caller. Non-admins see only games that pass the per-category tag filter (see [Game_Access_Model.md](Game_Access_Model.md)).

Optional query param: `?platform=lumoplay|vr|switch`

### GET /api/Games/{gameId}

Returns a single game. Returns **404** if the game does not exist **or** if the caller is not allowed to see it (existence is not leaked).

### POST /api/Games *(Admin)*

Creates a new game entry.

### PUT /api/Games/{gameId} *(Admin)*

Updates game metadata (name, description, image filename, one-pager filename).

### DELETE /api/Games/{gameId} *(Admin)*

Removes a game from the library.

### POST /api/Games/{gameId}/image *(Admin)*

Uploads and associates a cover image (multipart/form-data).

### DELETE /api/Games/{gameId}/image *(Admin)*

Removes a game's cover image.

### POST /api/Games/{gameId}/one-pager *(Admin)*

Uploads and associates a one-pager image.

### DELETE /api/Games/{gameId}/one-pager *(Admin)*

Removes a game's one-pager image.

### GET /api/Games/{gameId}/tags

Returns resolved tag details for a single game, grouped by category.

### POST /api/Games/{gameId}/tags *(Admin)*

Replaces all tag assignments for a game in one call. Body: `{ "tagIds": ["<tag_id>", ...] }`.

---

## Devices

### GET /api/Devices

Returns all devices visible to the caller (filtered by allowed locations for non-admins). `currentGame` is stripped from the response if the game is forbidden for the caller.

### GET /api/Devices/{ipAddress}

Returns a single device by IP. Returns **404** if the device doesn't exist or if the caller lacks location access. `currentGame` is stripped if the game is forbidden.

### POST /api/Devices *(Admin)*

Registers a new device.

### DELETE /api/Devices *(Admin)*

Removes a device by IP address (`?ipAddress=<ip>`).

---

## Playlists

### GET /api/Playlists

Returns the caller's own playlists plus all default playlists, with forbidden games stripped from each playlist's `Games` list.

### GET /api/Playlists/{id}

Returns a single playlist visible to the caller, with forbidden games stripped from the `Games` list. Returns **404** if not visible (existence not leaked).

### POST /api/Playlists

Creates a new personal playlist for the caller.

### PUT /api/Playlists/{id}

Full update (name + game list). Owner-only.

### DELETE /api/Playlists/{id}

Deletes a playlist. Owner-only.

### POST /api/Playlists/{id}/publish *(Admin, owner-only)*

Toggles `IsDefault` (published/unpublished).

### POST /api/Playlists/{id}/clone

Copies a visible playlist into the caller's personal library.

### POST /api/Playlists/{playlistId}/add-game-to-playlist/{gameId}

Adds a game to a playlist the caller owns. Returns **403** if the game is forbidden for the caller.

### POST /api/Playlists/{playlistId}/remove-game-from-playlist/{gameId}

Removes a game from a playlist the caller owns.

### PUT /api/Playlists/{playlistId}/update-order

Reorders games in a playlist the caller owns.

---

## Playback

### POST /api/Playback/play-game/{ipAddress}/game/{gameId}

Launches a game on a device. Returns **403** if the game is forbidden for the caller. Returns **404** if the device is outside the caller's allowed locations.

### POST /api/Playback/stop-game/{ipAddress}

Stops playback on a device.

### GET /api/Playback/now-playing/{ipAddress}

Returns the current playback state from the device.

### POST /api/Playback/play-playlist/{ipAddress}/{playlistId}

Starts playlist playback from the first game. Returns **403** if the first game is forbidden.

### POST /api/Playback/playlist/next-game/{ipAddress}

Advances to the next game in the active playlist. Returns **403** if the next game is forbidden.

### POST /api/Playback/playlist/previous-game/{ipAddress}

Goes back to the previous game in the active playlist. Returns **403** if the previous game is forbidden.
