using MongoDB.Bson;

namespace IntTech_Controller_Backend.Models;

public class DeviceResponseDto
{
    public ObjectId Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public ObjectId LocationId { get; set; }
    public string Status { get; set; } = "offline";
    public bool IsPlaying { get; set; }
    public string? CurrentLumoGameId { get; set; }
    public ActivePlaylistState? ActivePlaylist { get; set; }
    public DateTime LastChecked { get; set; }
}
