namespace Morphant;

public enum NullDestinationHandling
{
    Default = 0, // CreateNew

    CreateNew, // создать новый объект

    Throw // бросить исключение
}
