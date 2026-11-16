using System.Collections.Generic;

namespace Pos.Domain;

// Строка итога. Это результат расчёта, а не отдельное рандомное число.
public sealed class TotalLine
{
    public StudentId StudentId { get; init; }
    public string StudentName { get; init; } = "";
    public double Value { get; init; }
}

// Поведенческий паттерн Стратегия: правило итога можно подменить.
public interface ITotalStrategy
{
    string Name { get; }
    double ScoreOutOfTen(Rubric rubric, IReadOnlyList<Evaluation> evaluations);
}

// Пустые критерии в знаменатель не входят.
public sealed class OnlyGradedStrategy : ITotalStrategy
{
    public string Name => "только по выставленным критериям";

    public double ScoreOutOfTen(Rubric rubric, IReadOnlyList<Evaluation> evaluations)
    {
        TotalRules.RejectDuplicateCriteria(evaluations);
        double weightSum = 0;
        double acc = 0;
        foreach (Evaluation evaluation in evaluations)
        {
            Criterion criterion = TotalRules.RequireCriterion(rubric, evaluation.CriterionId);
            acc += TotalRules.WeightedPart(evaluation, criterion);
            weightSum += criterion.Weight;
        }
        if (weightSum == 0)
        {
            return 0;
        }
        return acc / weightSum * 10.0;
    }
}

// Невыставленный критерий даёт 0 и всё равно входит в сумму весов.
public sealed class MissingAsZeroStrategy : ITotalStrategy
{
    public string Name => "пустой критерий считается нулём";

    public double ScoreOutOfTen(Rubric rubric, IReadOnlyList<Evaluation> evaluations)
    {
        TotalRules.RejectDuplicateCriteria(evaluations);
        double weightSum = 0;
        foreach (Criterion criterion in rubric.All())
        {
            weightSum += criterion.Weight;
        }
        if (weightSum == 0)
        {
            return 0;
        }
        double acc = 0;
        foreach (Evaluation evaluation in evaluations)
        {
            Criterion criterion = TotalRules.RequireCriterion(rubric, evaluation.CriterionId);
            acc += TotalRules.WeightedPart(evaluation, criterion);
        }
        return acc / weightSum * 10.0;
    }
}

// Domain Service: итог не принадлежит одной оценке и не хранится в сущности.
public sealed class TotalCalculator
{
    private readonly ITotalStrategy _strategy;

    public TotalCalculator(ITotalStrategy strategy)
    {
        _strategy = strategy;
    }

    public List<TotalLine> Calculate(
        IReadOnlyList<Student> students,
        Rubric rubric,
        IReadOnlyList<Evaluation> evaluations)
    {
        // Оценка чужого студента не должна молча выпасть из расчёта
        foreach (Evaluation evaluation in evaluations)
        {
            bool known = false;
            foreach (Student student in students)
            {
                if (student.Id == evaluation.StudentId)
                {
                    known = true;
                }
            }
            if (!known)
            {
                throw new DomainError("Оценка ссылается на студента, которого нет в списке");
            }
        }

        List<TotalLine> lines = new();
        foreach (Student student in students)
        {
            List<Evaluation> own = new();
            foreach (Evaluation evaluation in evaluations)
            {
                if (evaluation.StudentId == student.Id)
                {
                    own.Add(evaluation);
                }
            }
            lines.Add(new TotalLine
            {
                StudentId = student.Id,
                StudentName = student.Name,
                Value = _strategy.ScoreOutOfTen(rubric, own),
            });
        }
        return lines;
    }
}

// Общие шаги обеих стратегий. 
// Файл виден только в этом файле.
file static class TotalRules
{
    public static void RejectDuplicateCriteria(IReadOnlyList<Evaluation> evaluations)
    {
        for (int i = 0; i < evaluations.Count; ++i)
        {
            for (int j = i + 1; j < evaluations.Count; ++j)
            {
                if (evaluations[i].CriterionId == evaluations[j].CriterionId)
                {
                    throw new DomainError("Две оценки по одному критерию у студента");
                }
            }
        }
    }

    public static Criterion RequireCriterion(Rubric rubric, CriterionId id)
    {
        Criterion? found = rubric.Find(id);
        if (found == null)
        {
            throw new DomainError("Оценка ссылается на критерий вне рубрикатора");
        }
        return found;
    }

    public static double WeightedPart(Evaluation evaluation, Criterion criterion)
    {
        return (double)evaluation.Score.Value / criterion.MaxScore * criterion.Weight;
    }
}
