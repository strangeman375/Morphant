using System.Runtime.CompilerServices;
using Morphant.Exceptions;

namespace Morphant.Generator;

[CompilerGenerated]
public static class UserMapperMorphantExtensions
{
    public static MapperBuilder<User, UserModel> Template(this MapperBuilder<User, UserModel> builder, Func<User, UserModelMorphantTemplate> templateFunc) =>
        throw new RuntimeInvocationNotSupportedException();

    public static MapperBuilder<User, UserModel> Template(this MapperBuilder<User, UserModel> builder, Func<User, UserModel, UserModelMorphantTemplate> templateFunc) =>
        throw new RuntimeInvocationNotSupportedException();
}
