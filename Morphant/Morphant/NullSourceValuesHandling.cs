namespace Morphant;

// что делать с null значениями в source members
public enum NullSourceValuesHandling // только для not-nullable / nullable / auto/ manual ?
{
    Default = 0, // None

    None, // нет доп-логики, просто прокидываем

    Ignore, // игнорируем, т.е. не прокидываем в destination members

    Throw // бросаем исключение
}
