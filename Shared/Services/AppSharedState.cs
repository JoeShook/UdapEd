#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Services;

public class AppSharedState
{
    private string? _scopeLevelSelected;
    private bool _tieredOAuth;

    public string? ScopeLevelSelected
    {
        get => _scopeLevelSelected;
        set
        {
            var changed = _scopeLevelSelected != value;
            _scopeLevelSelected = value;

            if (changed)
            {
                NotifyStateChanged();
            }
        }
    }

    public bool TieredOAuth
    {
        get => _tieredOAuth;
        set
        {
            var changed = _tieredOAuth != value;
            _tieredOAuth = value;

            if (changed)
            {
                NotifyStateChanged();
            }
        }
    }

    public event Action? OnChange;
    
    private void NotifyStateChanged() => OnChange?.Invoke();
}