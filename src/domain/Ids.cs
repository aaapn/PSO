namespace Pos.Domain;

// Идентификаторы — это объекты-значения. они равны, если равно число. своей жизни вне id нет.

public readonly record struct StudentId(int Value);

public readonly record struct CriterionId(int Value);

public readonly record struct EvaluationId(int Value);
