namespace Pos.Domain;

// Студент — это сущность. Людей с одинаковыми именами различаем по id.
public sealed class Student
{
    public Student(StudentId id, string name)
    {
        if (id.Value <= 0)
        {
            throw new DomainError("Некорректный id студента");
        }
        TextRules.RequireName(name, "Имя студента");
        Id = id;
        Name = name;
    }

    public StudentId Id { get; }
    public string Name { get; }

    public override bool Equals(object? other) => other is Student student && Id == student.Id;
    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Student? left, Student? right) => Equals(left, right);
    public static bool operator !=(Student? left, Student? right) => !Equals(left, right);
}
