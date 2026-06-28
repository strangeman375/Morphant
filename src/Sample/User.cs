using Morphant.Markers;
using Morphant.Members;

namespace Morphant.Generator;

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


