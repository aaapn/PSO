namespace Pos.Domain;

// Показывает по какому критерию был поставлен балл
public sealed class Evaluation
{
    // internal: вне домена конструктор не виден, оценку создаёт только EvaluationFactory.
    internal Evaluation(
        EvaluationId id,
        StudentId studentId,
        CriterionId criterionId,
        Score score,
        string comment)
    {
        Id = id;
        StudentId = studentId;
        CriterionId = criterionId;
        Score = score;
        Comment = comment;
    }

    public EvaluationId Id { get; }
    public StudentId StudentId { get; }
    public CriterionId CriterionId { get; }
    public Score Score { get; private set; }
    public string Comment { get; private set; }

    // Исправление балла. здесь диапазон проверяется заново
    public void Correct(Criterion criterion, int rawScore, string comment)
    {
        if (criterion.Id != CriterionId)
        {
            throw new DomainError("Оценка относится к другому критерию");
        }
        Score = Score.Of(rawScore, criterion.MaxScore);
        Comment = comment;
    }

    // Копия той же оценки. Класс — это ссылочный тип, поэтому репозиторий хранит
    // и отдаёт копии: исправить оценку можно только через update.
    public Evaluation Copy() => new(Id, StudentId, CriterionId, Score, Comment);
}
