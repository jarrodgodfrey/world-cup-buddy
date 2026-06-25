using System.Text.Json;
using WorldCupBuddy.Models;

namespace WorldCupBuddy.Services;

/// <summary>
/// Holds the current user's profile for the lifetime of their Blazor circuit and
/// notifies pages (Edge Finder, Simulator) when it changes so they can react.
/// Scoped — one instance per connected user.
/// </summary>
public class ProfileState
{
    public UserProfile? Current { get; private set; }

    public event Action? OnChange;

    public bool HasProfile => Current is not null;

    public void Set(UserProfile profile)
    {
        Current = profile;
        OnChange?.Invoke();
    }

    public void Clear()
    {
        Current = null;
        OnChange?.Invoke();
    }

    /// <summary>Browser localStorage key used to persist the profile across reloads.</summary>
    public const string StorageKey = "wcb_profile";

    public string ToJson() => Current is null ? "" : JsonSerializer.Serialize(Current);

    /// <summary>Rehydrate from a JSON string (e.g. localStorage). Returns true if a profile was loaded.</summary>
    public bool LoadFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var p = JsonSerializer.Deserialize<UserProfile>(json);
            if (p is not null)
            {
                Current = p;
                OnChange?.Invoke();
                return true;
            }
        }
        catch { /* corrupt/old payload — ignore */ }
        return false;
    }
}
