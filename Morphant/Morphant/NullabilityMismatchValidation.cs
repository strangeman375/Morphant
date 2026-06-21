namespace Morphant;

// что делать, если nullable source мембер маппится на not-nullable destination member
public enum NullabilityMismatchValidation
{
    Default = 0, // Error

    None, // нет валидации

    Warning, // варнинг

    Error // ошибка компиляции (treat as required)
}
