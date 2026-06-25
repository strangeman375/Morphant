using System.Runtime.CompilerServices;
using Morphant.Markers;
using Morphant.Members;

namespace Morphant.Generators;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

[CompilerGenerated]
public sealed record UserModelMorphantTemplateConstructorMembers
{
    public ConstructorMember<int> idInt;
    public ConstructorMember<string> firstName;
    public ConstructorMember<string> lastName;
    public ConstructorMember<Guid> idGuid;
    public ConstructorMember<string?> displayName;
    public ConstructorMember<bool> isActive;
}

[CompilerGenerated]
public sealed record UserModelMorphantTemplate
{
    public UserModelMorphantTemplate(
        ByConventionMarker marker,
        UserModelMorphantTemplateConstructorMembers? members = null)
    {
    }

    public UserModelMorphantTemplate(ByFactoryMarker<UserModel> marker)
    {
    }

    public UserModelMorphantTemplate()
    {
    }

    public UserModelMorphantTemplate(
        ConstructorMember<int> id,
        ConstructorMember<string> firstName,
        ConstructorMember<string> lastName)
    {
    }

    public UserModelMorphantTemplate(
        ConstructorMember<Guid> id,
        ConstructorMember<string?> displayName,
        ConstructorMember<bool>? isActive = null)
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
