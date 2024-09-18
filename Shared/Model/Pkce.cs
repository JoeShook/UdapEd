#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Model;

public class Pkce
{
    public string? CodeChallenge { get; set; }

    public string? CodeChallengeMethod { get; set; }

    public string? CodeVerifier { get; set; }
    public bool EnablePkce { get; set; }
}