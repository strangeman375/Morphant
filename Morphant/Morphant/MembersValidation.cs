namespace Morphant;

public enum MembersValidation
{
    Default = 0, // None

    None, // обычный режим = варнинги только по required destination свойствам

    Source, // все свойства source должны участвовать, варнинги по всем unmapped source свойствам + required destination свойствам

    Destination, // все свойства destination должны участвовать, варнинги по всем unmapped destination свойствам

    Strict // все свойства source и destination должны участвовать, варнинги по всем unmapped свойствам source и destination
}
