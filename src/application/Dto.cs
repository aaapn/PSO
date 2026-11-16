using System.Collections.Generic;

namespace Pos.Application;

// Плоские структуры для консоли и CSV. это не домен.

public sealed class StudentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class CriterionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int MaxScore { get; set; }
    public int Weight { get; set; }
}

public sealed class EvaluationDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public int CriterionId { get; set; }
    public string CriterionName { get; set; } = "";
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public string Comment { get; set; } = "";
}

public sealed class TotalDto
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public double Value { get; set; }
    public string Strategy { get; set; } = "";
}

public sealed class StoredEvaluation
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CriterionId { get; set; }
    public int Score { get; set; }
    public string Comment { get; set; } = "";
}

public sealed class StoredGradeBook
{
    public List<StudentDto> Students { get; set; } = new();
    public List<CriterionDto> Criteria { get; set; } = new();
    public List<StoredEvaluation> Evaluations { get; set; } = new();
}
