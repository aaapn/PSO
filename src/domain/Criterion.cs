namespace Pos.Domain;

// Критерии. за что ставим балл, какой максимум и какой вес в итоге
public sealed class Criterion
{
    public Criterion(CriterionId id, string name, int maxScore, int weight)
    {
        if (id.Value <= 0)
        {
            throw new DomainError("Некорректный id критерия");
        }
        TextRules.RequireName(name, "Название критерия");
        if (maxScore <= 0)
        {
            throw new DomainError("Максимум критерия должен быть больше нуля");
        }
        if (weight <= 0)
        {
            throw new DomainError("Вес критерия должен быть больше нуля");
        }
        Id = id;
        Name = name;
        MaxScore = maxScore;
        Weight = weight;
    }

    public CriterionId Id { get; }
    public string Name { get; }
    public int MaxScore { get; }
    public int Weight { get; }
}
