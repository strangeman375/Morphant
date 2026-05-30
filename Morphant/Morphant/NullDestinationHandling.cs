namespace Morphant;

public enum NullDestinationHandling
{
    Default = 0, // CreateNew

    ReturnNull, // вернуть null

    CreateNew, // создать новый объект

    Throw // бросить исключение
}
