using MongoDB.Bson;

namespace IntTech_Controller_Backend.Models;

/**
 * A <see cref="Device"/> as returned to clients. Mirrors the stored document
 * minus the security key, which must never leave the server.
 */
public class DeviceResponseDto
{
    /** Mongo document identifier. */
    public ObjectId Id { get; set; }
    /** Human-readable device name shown in the UI. */
    public string Name { get; set; } = string.Empty;
    /** Host or IP address of the LUMOplay service on the local network. */
    public string IpAddress { get; set; } = string.Empty;
    /** Id of the location this device sits in. */
    public ObjectId LocationId { get; set; }
    /** Reachability as of <see cref="LastChecked"/>: "online" or "offline". */
    public string Status { get; set; } = "offline";
    /** Whether a game was playing at the last poll. */
    public bool IsPlaying { get; set; }
    /** Vendor id of the game on screen, or null when nothing is playing. */
    public string? CurrentLumoGameId { get; set; }
    /** Playlist currently running on this device, or null when none is active. */
    public ActivePlaylistState? ActivePlaylist { get; set; }
    /** UTC timestamp of the last successful poll of this device. */
    public DateTime LastChecked { get; set; }
}
