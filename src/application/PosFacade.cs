using System.Collections.Generic;
using Pos.Domain;

namespace Pos.Application;

public enum TotalPolicy
{
    OnlyGraded,
    MissingAsZero,
}

// Структурный паттерн «Фасад». консоль ходит только сюда.
public sealed class PosFacade
{
    private readonly IStudentRepository _students;
    private readonly IRubricRepository _rubrics;
    private readonly IEvaluationRepository _evaluations;
    private readonly IGradeBookStore _store;
    private EvaluationFactory _factory = new();
    private readonly OnlyGradedStrategy _onlyGraded = new();
    private readonly MissingAsZeroStrategy _missingAsZero = new();

    public PosFacade(
        IStudentRepository students,
        IRubricRepository rubrics,
        IEvaluationRepository evaluations,
        IGradeBookStore store)
    {
        _students = students;
        _rubrics = rubrics;
        _evaluations = evaluations;
        _store = store;
    }

    public StudentDto AddStudent(string name)
    {
        return ToDto(_students.Add(name));
    }

    public List<StudentDto> Students()
    {
        List<StudentDto> result = new();
        foreach (Student student in _students.All())
        {
            result.Add(ToDto(student));
        }
        return result;
    }

    public CriterionDto AddCriterion(string name, int maxScore, int weight)
    {
        CriterionId id = _rubrics.Rubric.Add(name, maxScore, weight);
        return ToDto(_rubrics.Rubric.Find(id)!);
    }

    public List<CriterionDto> Criteria()
    {
        List<CriterionDto> result = new();
        foreach (Criterion criterion in _rubrics.Rubric.All())
        {
            result.Add(ToDto(criterion));
        }
        return result;
    }

    public EvaluationDto Grade(int studentId, int criterionId, int score, string comment)
    {
        Student? student = _students.Find(new StudentId(studentId));
        if (student == null)
        {
            throw new DomainError("Студент не найден");
        }
        if (_evaluations.FindByPair(new StudentId(studentId), new CriterionId(criterionId)) != null)
        {
            throw new DomainError("Оценка по этому критерию уже стоит. Исправьте её или отмените.");
        }
        Evaluation created = _factory.Create(
            student, _rubrics.Rubric, new CriterionId(criterionId), score, comment);
        _evaluations.Add(created);
        return ToDto(created);
    }

    public EvaluationDto CorrectGrade(int evaluationId, int score, string comment)
    {
        Evaluation? found = _evaluations.Find(new EvaluationId(evaluationId));
        if (found == null)
        {
            throw new DomainError("Оценка не найдена");
        }
        Criterion? criterion = _rubrics.Rubric.Find(found.CriterionId);
        if (criterion == null)
        {
            throw new DomainError("Критерия нет в рубрикаторе");
        }
        Evaluation updated = found.Copy();
        updated.Correct(criterion, score, comment);
        _evaluations.Update(updated);
        return ToDto(updated);
    }

    public void CancelGrade(int evaluationId)
    {
        if (!_evaluations.Remove(new EvaluationId(evaluationId)))
        {
            throw new DomainError("Оценка не найдена");
        }
    }

    public List<EvaluationDto> Grades()
    {
        List<EvaluationDto> result = new();
        foreach (Evaluation evaluation in _evaluations.All())
        {
            result.Add(ToDto(evaluation));
        }
        return result;
    }

    public List<TotalDto> Totals(TotalPolicy policy)
    {
        ITotalStrategy rule = Strategy(policy);
        TotalCalculator calculator = new(rule);
        List<TotalLine> lines = calculator.Calculate(_students.All(), _rubrics.Rubric, _evaluations.All());
        List<TotalDto> result = new();
        foreach (TotalLine line in lines)
        {
            result.Add(new TotalDto
            {
                StudentId = line.StudentId.Value,
                StudentName = line.StudentName,
                Value = line.Value,
                Strategy = rule.Name,
            });
        }
        return result;
    }

    public string ExportTotals(TotalPolicy policy)
    {
        string fileName = policy == TotalPolicy.OnlyGraded
            ? "totals_only_graded.csv"
            : "totals_missing_zero.csv";
        return _store.ExportTotals(Totals(policy), fileName);
    }

    public void Save()
    {
        _store.Save(Snapshot());
    }

    public void Load()
    {
        if (!_store.Load(out StoredGradeBook data))
        {
            throw new DomainError("Сохранённой ведомости нет");
        }

        for (int i = 0; i < data.Students.Count; ++i)
        {
            if (data.Students[i].Id <= 0)
            {
                throw new DomainError("В файле некорректный id студента");
            }
            for (int j = i + 1; j < data.Students.Count; ++j)
            {
                if (data.Students[i].Id == data.Students[j].Id)
                {
                    throw new DomainError("В файле два студента с одним id");
                }
            }
        }
        for (int i = 0; i < data.Criteria.Count; ++i)
        {
            if (data.Criteria[i].Id <= 0)
            {
                throw new DomainError("В файле некорректный id критерия");
            }
            for (int j = i + 1; j < data.Criteria.Count; ++j)
            {
                if (data.Criteria[i].Id == data.Criteria[j].Id)
                {
                    throw new DomainError("В файле два критерия с одним id");
                }
                if (data.Criteria[i].Name == data.Criteria[j].Name)
                {
                    throw new DomainError("В файле два критерия с одним названием");
                }
            }
        }
        for (int i = 0; i < data.Evaluations.Count; ++i)
        {
            StoredEvaluation evaluation = data.Evaluations[i];
            bool studentOk = false;
            foreach (StudentDto student in data.Students)
            {
                if (student.Id == evaluation.StudentId)
                {
                    studentOk = true;
                }
            }
            if (!studentOk)
            {
                throw new DomainError("В файле оценка студента, которого нет в списке");
            }
            CriterionDto? criterion = null;
            foreach (CriterionDto item in data.Criteria)
            {
                if (item.Id == evaluation.CriterionId)
                {
                    criterion = item;
                }
            }
            if (criterion == null)
            {
                throw new DomainError("В файле оценка по критерию, которого нет в рубрикаторе");
            }
            Score.Of(evaluation.Score, criterion.MaxScore);
            for (int j = i + 1; j < data.Evaluations.Count; ++j)
            {
                StoredEvaluation other = data.Evaluations[j];
                if (evaluation.Id == other.Id)
                {
                    throw new DomainError("В файле две оценки с одним id");
                }
                if (evaluation.StudentId == other.StudentId &&
                    evaluation.CriterionId == other.CriterionId)
                {
                    throw new DomainError("В файле две оценки одной пары студент-критерий");
                }
            }
        }

        // Новую ведомость собираем целиком отдельно от текущей. Пока она не собрана, текущую не трогаем
        List<Student> freshStudents = new();
        foreach (StudentDto student in data.Students)
        {
            freshStudents.Add(new Student(new StudentId(student.Id), student.Name));
        }
        Rubric freshRubric = new();
        foreach (CriterionDto criterion in data.Criteria)
        {
            freshRubric.AddExisting(new Criterion(
                new CriterionId(criterion.Id), criterion.Name, criterion.MaxScore, criterion.Weight));
        }
        EvaluationFactory freshFactory = new();
        List<Evaluation> freshEvaluations = new();
        foreach (StoredEvaluation evaluation in data.Evaluations)
        {
            Student student = LoadRules.RequireStudentIn(data.Students, evaluation.StudentId);
            Evaluation restored = freshFactory.Restore(
                student,
                freshRubric,
                new EvaluationId(evaluation.Id),
                new CriterionId(evaluation.CriterionId),
                evaluation.Score,
                evaluation.Comment);
            freshEvaluations.Add(restored);
        }

        // Подменяем всё разом. Если какой-то репозиторий всё же откажет, возвращаем старое состояние
        IReadOnlyList<Student> oldStudents = _students.All();
        Rubric oldRubric = _rubrics.Rubric;
        IReadOnlyList<Evaluation> oldEvaluations = _evaluations.All();
        try
        {
            _students.ReplaceAll(freshStudents);
            _rubrics.Replace(freshRubric);
            _evaluations.ReplaceAll(freshEvaluations);
        }
        catch
        {
            _students.ReplaceAll(oldStudents);
            _rubrics.Replace(oldRubric);
            _evaluations.ReplaceAll(oldEvaluations);
            throw;
        }
        // Счётчик id оценок меняем только после успешной подмены
        _factory = freshFactory;
    }

    public string StoragePath()
    {
        return _store.Location();
    }

    private static StudentDto ToDto(Student student)
    {
        return new StudentDto
        {
            Id = student.Id.Value,
            Name = student.Name,
        };
    }

    private static CriterionDto ToDto(Criterion criterion)
    {
        return new CriterionDto
        {
            Id = criterion.Id.Value,
            Name = criterion.Name,
            MaxScore = criterion.MaxScore,
            Weight = criterion.Weight,
        };
    }

    private EvaluationDto ToDto(Evaluation evaluation)
    {
        Student? student = _students.Find(evaluation.StudentId);
        Criterion? criterion = _rubrics.Rubric.Find(evaluation.CriterionId);
        return new EvaluationDto
        {
            Id = evaluation.Id.Value,
            StudentId = evaluation.StudentId.Value,
            CriterionId = evaluation.CriterionId.Value,
            Score = evaluation.Score.Value,
            MaxScore = evaluation.Score.MaxScore,
            Comment = evaluation.Comment,
            StudentName = student != null ? student.Name : "?",
            CriterionName = criterion != null ? criterion.Name : "?",
        };
    }

    private ITotalStrategy Strategy(TotalPolicy policy)
    {
        if (policy == TotalPolicy.OnlyGraded)
        {
            return _onlyGraded;
        }
        return _missingAsZero;
    }

    private StoredGradeBook Snapshot()
    {
        StoredGradeBook book = new()
        {
            Students = Students(),
            Criteria = Criteria(),
        };
        foreach (Evaluation evaluation in _evaluations.All())
        {
            book.Evaluations.Add(new StoredEvaluation
            {
                Id = evaluation.Id.Value,
                StudentId = evaluation.StudentId.Value,
                CriterionId = evaluation.CriterionId.Value,
                Score = evaluation.Score.Value,
                Comment = evaluation.Comment,
            });
        }
        return book;
    }
}

file static class LoadRules
{
    public static Student RequireStudentIn(List<StudentDto> students, int id)
    {
        foreach (StudentDto student in students)
        {
            if (student.Id == id)
            {
                return new Student(new StudentId(student.Id), student.Name);
            }
        }
        throw new DomainError("В файле оценка студента, которого нет в списке");
    }
}
