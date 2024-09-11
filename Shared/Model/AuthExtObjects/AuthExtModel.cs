#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Model.AuthExtObjects;
public class AuthExtModel
{
    public bool UseInAuth { get; set; }
    public bool UseInRegister { get; set; }
    public string Json { get; set; }
}
