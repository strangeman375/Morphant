namespace Morphant;

public enum MemberMatching
{
    Default = 0, // Auto

    Auto, // обычный режим = явно заданные свойства + заресолвенные по неймингу

    Explicit // только явно заданные свойства
}
