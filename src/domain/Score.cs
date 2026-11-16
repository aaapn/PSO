namespace Pos.Domain;

// Реализуются баллы.
// Сами по себе уже проверены: 0 <= value <= max.
public sealed record Score
{
    private Score(int value, int maxScore)
    {
        Value = value;
        MaxScore = maxScore;
    }

    public int Value { get; }
    public int MaxScore { get; }

    public static Score Of(int value, int maxScore)
    {
        if (maxScore <= 0)
        {
            throw new DomainError("Максимум критерия должен быть больше нуля");
        }
        if (value < 0 || value > maxScore)
        {
            throw new DomainError($"Балл {value} вне диапазона 0..{maxScore}");
        }
        return new Score(value, maxScore);
    }
}
