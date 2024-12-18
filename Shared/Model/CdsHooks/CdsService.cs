#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Model.CdsHooks;

public class CdsServiceViewModel
{
    public string Url { get; set; }
    public CdsService CdsService {get; set; }
}

[Serializable]
public class CdsService : Udap.CdsHooks.Model.CdsService
{
    public CdsService(){}

    public CdsService(Udap.CdsHooks.Model.CdsService baseService)
    {
        foreach (var property in baseService.GetType().GetProperties())
        {
            var value = property.GetValue(baseService);
            this.GetType().GetProperty(property.Name)?.SetValue(this, value);
        }
    }

    public bool Enabled { get; set; }
}
