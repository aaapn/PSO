namespace Pos.Domain;

internal static class TextRules
{
    // Пустое имя в домене бессмыслено.
    public static void RequireName(string value, string field)
    {
        bool blank = value.Trim(' ', '\t').Length == 0;
        if (blank)
        {
            throw new DomainError(field + " не может быть пустым");
        }
    }
}
