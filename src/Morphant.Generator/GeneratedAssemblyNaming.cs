using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class GeneratedAssemblyNaming
{
    public static string BuildScope(Compilation compilation)
    {
        var identity = compilation.Assembly.Identity;
        var result = new StringBuilder("A_");

        foreach (var character in identity.Name)
        {
            if (character is >= 'A' and <= 'Z' or
                >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                result.Append(character);
            }
            else if (character == '_')
            {
                result.Append("__");
            }
            else
            {
                result.Append('_');
                result.Append(((int)character).ToString(
                    "X4", CultureInfo.InvariantCulture));
            }
        }

        if (!identity.PublicKeyToken.IsDefaultOrEmpty)
        {
            result.Append("_K");

            foreach (var value in identity.PublicKeyToken)
            {
                result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }
        }

        return result.ToString();
    }
}
