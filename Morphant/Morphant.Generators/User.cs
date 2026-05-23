namespace Morphant.Generators;

public class Address
{
    public string StreetName { get; set; } = null!;

    public int HouseNumber { get; set; }
}

public class AddressModel
{
    public string StreetName { get; set; } = null!;

    public int HouseNumber { get; set; }
}

public class User
{
    public int Id { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string? Email { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string? InternalNote { get; set; }

    public Address? Address { get; set; }
}

public class UserModel
{
    public UserModel()
    {
    }

    public UserModel(int id, string firstName, string lastName)
    {
        Id = id;
        FullName = firstName + " " + lastName;
    }

    public UserModel(Guid id, string? displayName, bool isActive = true)
    {
        Id = id.GetHashCode();
        DisplayName = displayName;
    }

    public int Id { get; set; }

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool IsActive { get; set; }

    public string? DisplayName { get; set; } = null!;

    public AddressModel? AddressDto { get; set; }
}

public sealed record UserModelMorphantTemplateConstructorMembers
{
    public ConstructorMember<int> idInt;
    public ConstructorMember<string> firstName;
    public ConstructorMember<string> lastName;
    public ConstructorMember<Guid> idGuid;
    public ConstructorMember<string?> displayName;
    public ConstructorMember<bool> isActive;
}

public sealed record UserModelMorphantTemplate
{
    public UserModelMorphantTemplate(ConstructorMarker marker, UserModelMorphantTemplateConstructorMembers? members = null)
    {
    }

    public UserModelMorphantTemplate()
    {
    }

    public UserModelMorphantTemplate(ConstructorMember<int> id, ConstructorMember<string> firstName, ConstructorMember<string> lastName)
    {
    }

    /// <param name="isActive">Default value: true</param>
    public UserModelMorphantTemplate(ConstructorMember<Guid> id, ConstructorMember<string?> displayName, ConstructorMember<bool>? isActive = null)
    {
    }

    public Member<int> Id { get; set; }

    public Member<string> FullName { get; set; }

    public Member<string?> Email { get; set; }

    public Member<DateTime> CreatedAt { get; set; }

    public Member<DateTime?> LastLoginAt { get; set; }

    public Member<bool> IsActive { get; set; }

    public Member<string?> DisplayName { get; set; }

    public Member<AddressModel?> AddressDto { get; set; }
}
