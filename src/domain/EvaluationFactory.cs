namespace Pos.Domain;

// Порождающий паттерн: единственная точка, где появляется оценка.
// Нельзя обойти проверку балла и проверку, что есть подходящий критерий
public sealed class EvaluationFactory
{
    private int _nextId = 1;

    public Evaluation Create(
        Student student,
        Rubric rubric,
        CriterionId criterionId,
        int rawScore,
        string comment)
    {
        Evaluation created = Build(new EvaluationId(_nextId), student, rubric, criterionId, rawScore, comment);
        ++_nextId;
        return created;
    }

    // Та же проверка при загрузке уже сохранённой оценки.
    public Evaluation Restore(
        Student student,
        Rubric rubric,
        EvaluationId id,
        CriterionId criterionId,
        int rawScore,
        string comment)
    {
        Evaluation created = Build(id, student, rubric, criterionId, rawScore, comment);
        if (id.Value >= _nextId)
        {
            _nextId = id.Value + 1;
        }
        return created;
    }

    private static Evaluation Build(
        EvaluationId id,
        Student student,
        Rubric rubric,
        CriterionId criterionId,
        int rawScore,
        string comment)
    {
        if (id.Value <= 0)
        {
            throw new DomainError("Некорректный id оценки");
        }
        Criterion? criterion = rubric.Find(criterionId);
        if (criterion == null)
        {
            throw new DomainError("Критерия нет в рубрикаторе");
        }
        Score score = Score.Of(rawScore, criterion.MaxScore);
        return new Evaluation(id, student.Id, criterionId, score, comment);
    }
}
