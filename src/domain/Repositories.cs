using System.Collections.Generic;

namespace Pos.Domain;

// Порты хранения. Домен не знает, память это или файл.

public interface IStudentRepository
{
    Student Add(string name);
    // Заменяет весь список разом. При ошибке старый список остаётся как был.
    void ReplaceAll(IEnumerable<Student> students);
    IReadOnlyList<Student> All();
    Student? Find(StudentId id);
}

public interface IRubricRepository
{
    // менять рубрикатор можно только через его же методы (Add, AddExisting),
    // а они держат инварианты.
    Rubric Rubric { get; }
    void Replace(Rubric rubric);
}

public interface IEvaluationRepository
{
    // Вторая оценка той же пары «студент + критерий» запрещена.
    void Add(Evaluation evaluation);
    // Меняются только балл и комментарий. Студента и критерий у оценки поменять нельзя.
    void Update(Evaluation evaluation);
    bool Remove(EvaluationId id);
    // Заменяет все оценки разом. При ошибке старые оценки остаются как были.
    void ReplaceAll(IEnumerable<Evaluation> evaluations);
    IReadOnlyList<Evaluation> All();
    Evaluation? Find(EvaluationId id);
    Evaluation? FindByPair(StudentId studentId, CriterionId criterionId);
}
