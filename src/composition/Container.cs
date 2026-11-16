using Pos.Application;
using Pos.Infrastructure;

namespace Pos.Composition;

// Composition root - это место, где собирается граф объектов
public sealed class Container
{
    private readonly MemoryStudentRepository _students = new();
    private readonly MemoryRubricRepository _rubrics = new();
    private readonly MemoryEvaluationRepository _evaluations = new();
    private readonly CsvGradeBookStore _store;
    private readonly PosFacade _pos;

    public Container()
    {
        _store = new CsvGradeBookStore("data");
        _pos = new PosFacade(_students, _rubrics, _evaluations, _store);
    }

    public PosFacade Pos => _pos;
}
