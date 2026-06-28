namespace Morphant;

public enum ConstructorSelection
{
    Default = 0, // Unambiguous

    Explicit, // явно задаём конструктор

    Parameterless, // только конструктор без параметров

    Single, // единственный конструктор

    Unambiguous, // единственный с параметрами или без параметров

    Greediest, // наибольшее количество параметров, которые можно смаппить

    Largest // наибольшее количество параметров
}
