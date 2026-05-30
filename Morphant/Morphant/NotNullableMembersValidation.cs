namespace Morphant;

// что делать, если nullable source мембер маппится на not-nullable destination member
public enum NotNullableMembersValidation
{
    Default = 0, // Error

    None, // нет валидации

    Warn, // варнинг

    Error // ошибка компиляции (treat as required)
}
