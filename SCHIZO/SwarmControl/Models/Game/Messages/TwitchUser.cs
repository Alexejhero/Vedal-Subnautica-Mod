namespace SwarmControl.Models.Game.Messages;

#nullable enable
/// <summary>
/// Twitch user info.
/// </summary>
/// <param name="Id">Channel ID.</param>
/// <param name="Username">Login name.</param>
/// <param name="DisplayName">What's displayed in chat.</param>
public record TwitchUser(int Id, string Username, string DisplayName);