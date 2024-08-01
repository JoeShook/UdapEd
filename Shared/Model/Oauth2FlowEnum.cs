#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Model;

public enum Oauth2FlowEnum { client_credentials, authorization_code }


public static class Auth2FlowChoice
{
    private static readonly IDictionary<Oauth2FlowEnum, GrantTypeWithScopeFilter> _choices = new Dictionary<Oauth2FlowEnum, GrantTypeWithScopeFilter>
    {
        {
            Oauth2FlowEnum.client_credentials,
            new GrantTypeWithScopeFilter
            {
                Name = Oauth2FlowEnum.client_credentials.ToString(), 
                GrantType = "client_credentials"
            }
        },
        {
            Oauth2FlowEnum.authorization_code,
            new GrantTypeWithScopeFilter
            {
                Name = "authorization_code",
                GrantType = "authorization_code"
            }
        }
    };

    public static IDictionary<Oauth2FlowEnum, GrantTypeWithScopeFilter> Choices => _choices;

    public static string GetGrantType(this Oauth2FlowEnum flow)
    {
        return _choices[flow].GrantType;
    }

}


public class GrantTypeWithScopeFilter
{
    public string Name { get; set; }
    public string GrantType { get; set; } = string.Empty;
}
