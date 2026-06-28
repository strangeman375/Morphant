namespace Morphant.Generator;

public partial class UserMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder
            .Map<User, UserModel>()
            .Template(s => new(ByConvention())
            {
                FullName = s.FirstName + " " + s.LastName,
                IsActive = true,
                DisplayName = Ignore(),
                LastLoginAt = Auto(),
                AddressDto = Map(s.Address)
            });
    }
}

/*

        builder
            .Map<User, UserModel>()
            .Template(s => new(ByFactory(() => new UserModel()))
            {
                FullName = s.FirstName + " " + s.LastName,
                IsActive = true,
                DisplayName = Ignore(),
                LastLoginAt = Auto(),
                AddressDto = Map(s.Address)
            });



        builder
            .Map<User, UserModel>()
            .Template(s =>
            {
                var result = s.LastLoginAt is not null
                    ? new UserModelMorphantTemplate(s.Id, s.FirstName, s.LastName)
                    : new UserModelMorphantTemplate(ByFactory(() => new UserModel()));

                return result with
                {
                    FullName = s.FirstName + " " + s.LastName,
                    IsActive = true,
                    DisplayName = Ignore(),
                    LastLoginAt = Auto(),
                    AddressDto = Map(s.Address)
                };
            });


        builder
            .Map<User, UserModel>()
            .Template(s =>
            {
                var result = s.LastLoginAt is not null
                    ? new UserModelMorphantTemplate(s.Id, s.FirstName, s.LastName)
                    : new UserModelMorphantTemplate(ByFactory(() => new UserModel()));

                var displayName = s.Address is null
                    ? s.FirstName
                    : Ignore<string?>();

                return result with
                {
                    FullName = s.FirstName + " " + s.LastName,
                    IsActive = true,
                    DisplayName = displayName,
                    LastLoginAt = Auto(),
                    AddressDto = Map(s.Address)
                };
            });
*/