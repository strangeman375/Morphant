namespace Morphant;

public enum MembersSelection
{
    Default = 0, // Auto

    Auto, // обычный режим = явно заданные свойства + заресолвенные по неймингу

    Explicit, // только явно заданные свойства

    Required // явно заданные свойства + заресолвенные по неймингу обязательные свойства
}
