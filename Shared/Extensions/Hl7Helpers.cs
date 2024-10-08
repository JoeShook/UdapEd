﻿#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Reflection;
using Hl7.Fhir.Model;

namespace UdapEd.Shared.Extensions;
public class Hl7Helpers
{
    public static List<Coding> GetAllCodingsFromType(Type type)
    {
        var codingList = new List<Coding>();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static);

        foreach (var property in properties)
        {
            if (property.PropertyType == typeof(Coding))
            {
                var coding = (Coding)property.GetValue(null);
                if (coding != null)
                {
                    codingList.Add(coding);
                }
            }
        }

        return codingList;
    }
}
