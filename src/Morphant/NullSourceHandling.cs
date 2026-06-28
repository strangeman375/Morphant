namespace Morphant;

public enum NullSourceHandling
{
    Default = 0, // ReturnNull

    ReturnNull, // вернуть null

    ReturnDestination, // вернуть destination, если он есть, иначе null

    Throw // бросить исключение
}
