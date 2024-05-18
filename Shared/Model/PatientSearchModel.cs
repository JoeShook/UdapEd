#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using UdapEd.Shared.Model.Smart;

namespace UdapEd.Shared.Model;

public class PatientSearchModel
{
    public bool GetResource { get; set; }

    public string? Family { get; set; }
    public string? Given { get; set; }
    public string? Name { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Id { get; set; }
    public string? Identifier { get; set; }

    public int RowsPerPage { get; set; }

    // public Uri? NextLink { get; set; }
    public PageDirection PageDirection { get; set; }
    public int Page { get; set; }

    public string? Bundle { get; set; }

    public LaunchContext? LaunchContext { get; set; }
}

public class PatientMatchModel
{
    public string? Family { get; set; }
    public string? Given { get; set; }
    public AdministrativeGender? Gender { get; set; }
    public DateTime? BirthDate { get; set; }

    public string? Identifier { get; set; }

    public List<Address>? AddressList { get; set; }

    public List<ContactSystem>? ContactSystemList { get; set; }
}

public class Address
{
    public int Id { get; set; }
    public string? Line1 { get; set; }

    public string? City { get; set; }

    public string? State {get; set; }

    public string? PostalCode { get; set; }
}

public class ContactSystem
{
    public int Id { get; set; }
    public ContactPoint.ContactPointSystem ContactPointSystem { get; set; }
    public ContactPoint.ContactPointUse ContactPointUse { get; set; }
    public string? Value { get; set; }
}