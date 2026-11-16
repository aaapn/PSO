using System.Collections.Generic;
using Pos.Domain;

namespace Pos.Infrastructure;

// Адаптеры порта репозитория: данные живут в памяти процесса.

public sealed class MemoryStudentRepository : IStudentRepository
{
    private List<Student> _students = new();
    private int _nextId = 1;

    public Student Add(string name)
    {
        Student student = new(new StudentId(_nextId), name);
        _students.Add(student);
        ++_nextId;
        return student;
    }

    public void ReplaceAll(IEnumerable<Student> students)
    {
        // Сначала всё проверяем, потом меняем. Так ошибка не оставит половину списка
        List<Student> fresh = new(students);
        int nextId = 1;
        for (int i = 0; i < fresh.Count; ++i)
        {
            for (int j = i + 1; j < fresh.Count; ++j)
            {
                if (fresh[i].Id == fresh[j].Id)
                {
                    throw new DomainError("Студент с таким id уже есть");
                }
            }
            if (fresh[i].Id.Value >= nextId)
            {
                nextId = fresh[i].Id.Value + 1;
            }
        }
        _students = fresh;
        _nextId = nextId;
    }

    public IReadOnlyList<Student> All()
    {
        return new List<Student>(_students);
    }

    public Student? Find(StudentId id)
    {
        foreach (Student student in _students)
        {
            if (student.Id == id)
            {
                return student;
            }
        }
        return null;
    }
}

public sealed class MemoryRubricRepository : IRubricRepository
{
    private Rubric _rubric = new();

    public Rubric Rubric => _rubric;

    public void Replace(Rubric rubric)
    {
        _rubric = rubric;
    }
}

// Оценка — это изменяемый класс, поэтому внутрь кладём копии и наружу отдаём копии.
public class MemoryEvaluationRepository : IEvaluationRepository
{
    private List<Evaluation> _items = new();

    public void Add(Evaluation evaluation)
    {
        if (Find(evaluation.Id) != null)
        {
            throw new DomainError("Оценка с таким id уже есть");
        }
        if (FindByPair(evaluation.StudentId, evaluation.CriterionId) != null)
        {
            throw new DomainError("Оценка по этому критерию уже стоит");
        }
        _items.Add(evaluation.Copy());
    }

    public void Update(Evaluation evaluation)
    {
        for (int i = 0; i < _items.Count; ++i)
        {
            Evaluation current = _items[i];
            if (current.Id == evaluation.Id)
            {
                // Пара «студент + критерий» не меняется, т к можно получить две оценки одной пары.
                if (current.StudentId != evaluation.StudentId ||
                    current.CriterionId != evaluation.CriterionId)
                {
                    throw new DomainError("У оценки нельзя менять студента или критерий");
                }
                _items[i] = evaluation.Copy();
                return;
            }
        }
        throw new DomainError("Оценка не найдена");
    }

    public bool Remove(EvaluationId id)
    {
        for (int i = 0; i < _items.Count; ++i)
        {
            if (_items[i].Id == id)
            {
                _items.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    //тест подменяет этот метод, чтобы проверить откат загрузки.
    public virtual void ReplaceAll(IEnumerable<Evaluation> evaluations)
    {
        List<Evaluation> fresh = new();
        foreach (Evaluation evaluation in evaluations)
        {
            fresh.Add(evaluation.Copy());
        }
        for (int i = 0; i < fresh.Count; ++i)
        {
            for (int j = i + 1; j < fresh.Count; ++j)
            {
                if (fresh[i].Id == fresh[j].Id)
                {
                    throw new DomainError("Оценка с таким id уже есть");
                }
                if (fresh[i].StudentId == fresh[j].StudentId &&
                    fresh[i].CriterionId == fresh[j].CriterionId)
                {
                    throw new DomainError("Оценка по этому критерию уже стоит");
                }
            }
        }
        _items = fresh;
    }

    public IReadOnlyList<Evaluation> All()
    {
        List<Evaluation> result = new();
        foreach (Evaluation evaluation in _items)
        {
            result.Add(evaluation.Copy());
        }
        return result;
    }

    public Evaluation? Find(EvaluationId id)
    {
        foreach (Evaluation evaluation in _items)
        {
            if (evaluation.Id == id)
            {
                return evaluation.Copy();
            }
        }
        return null;
    }

    public Evaluation? FindByPair(StudentId studentId, CriterionId criterionId)
    {
        foreach (Evaluation evaluation in _items)
        {
            if (evaluation.StudentId == studentId && evaluation.CriterionId == criterionId)
            {
                return evaluation.Copy();
            }
        }
        return null;
    }
}
