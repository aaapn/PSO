using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Pos.Application;
using static System.FormattableString;

namespace Pos.Infrastructure;

// Адаптер хранилища: вся ведомость это три CSV, поэтому имеем отдельный CSV.
public sealed class CsvGradeBookStore : IGradeBookStore
{
    private readonly string _directory;

    public CsvGradeBookStore(string directory)
    {
        _directory = directory;
    }

    public void Save(StoredGradeBook book)
    {
        Directory.CreateDirectory(_directory);

        // Пишем во временные файлы.
        try
        {
            using StreamWriter students = Csv.OpenWriter(Path.Combine(_directory, "students.csv.tmp"));
            using StreamWriter criteria = Csv.OpenWriter(Path.Combine(_directory, "criteria.csv.tmp"));
            using StreamWriter evaluations = Csv.OpenWriter(Path.Combine(_directory, "evaluations.csv.tmp"));

            students.Write("id;name\n");
            foreach (StudentDto student in book.Students)
            {
                students.Write(Invariant($"{student.Id};{Csv.Field(student.Name)}\n"));
            }

            criteria.Write("id;name;max_score;weight\n");
            foreach (CriterionDto criterion in book.Criteria)
            {
                criteria.Write(Invariant(
                    $"{criterion.Id};{Csv.Field(criterion.Name)};{criterion.MaxScore};{criterion.Weight}\n"));
            }

            evaluations.Write("id;student_id;criterion_id;score;comment\n");
            foreach (StoredEvaluation evaluation in book.Evaluations)
            {
                evaluations.Write(Invariant(
                    $"{evaluation.Id};{evaluation.StudentId};{evaluation.CriterionId};{evaluation.Score};{Csv.Field(evaluation.Comment)}\n"));
            }

            // Сбрасывает буфер. Ошибка записи видна только после него
            students.Flush();
            criteria.Flush();
            evaluations.Flush();
        }
        catch (Exception error)
        {
            foreach (string name in Csv.GradeBookFiles)
            {
                Csv.RemoveQuietly(Path.Combine(_directory, name + ".tmp"));
            }
            if (Csv.IsFileError(error))
            {
                throw new StorageError("Не удалось записать ведомость в " + _directory + ": " + error.Message, error);
            }
            throw;
        }

        Csv.Publish(_directory);
    }

    public bool Load(out StoredGradeBook book)
    {
        book = new StoredGradeBook();
        string studentsPath = Path.Combine(_directory, "students.csv");
        string criteriaPath = Path.Combine(_directory, "criteria.csv");
        string evaluationsPath = Path.Combine(_directory, "evaluations.csv");
        bool hasStudents = File.Exists(studentsPath);
        bool hasCriteria = File.Exists(criteriaPath);
        bool hasEvaluations = File.Exists(evaluationsPath);
        if (!hasStudents && !hasCriteria && !hasEvaluations)
        {
            return false;
        }
        // Если есть хотя бы один файл,то ведомость сохранялась, и без любого из трёх она неполная.
        if (!hasStudents)
        {
            throw new StorageError("Ведомость неполная: нет файла " + studentsPath);
        }
        if (!hasCriteria)
        {
            throw new StorageError("Ведомость неполная: нет файла " + criteriaPath);
        }
        if (!hasEvaluations)
        {
            throw new StorageError("Ведомость неполная: нет файла " + evaluationsPath);
        }

        StoredGradeBook loaded = new();
        foreach (List<string> parts in Csv.ReadRecords(studentsPath, Csv.StudentsHeader))
        {
            if (parts.Count != 2)
            {
                throw new StorageError("Плохая строка студента: " + string.Join(";", parts));
            }
            loaded.Students.Add(new StudentDto
            {
                Id = Csv.ToInt(parts[0], "id студента"),
                Name = parts[1],
            });
        }
        foreach (List<string> parts in Csv.ReadRecords(criteriaPath, Csv.CriteriaHeader))
        {
            if (parts.Count != 4)
            {
                throw new StorageError("Плохая строка критерия: " + string.Join(";", parts));
            }
            loaded.Criteria.Add(new CriterionDto
            {
                Id = Csv.ToInt(parts[0], "id критерия"),
                Name = parts[1],
                MaxScore = Csv.ToInt(parts[2], "max_score"),
                Weight = Csv.ToInt(parts[3], "weight"),
            });
        }
        foreach (List<string> parts in Csv.ReadRecords(evaluationsPath, Csv.EvaluationsHeader))
        {
            if (parts.Count != 5)
            {
                throw new StorageError("Плохая строка оценки: " + string.Join(";", parts));
            }
            loaded.Evaluations.Add(new StoredEvaluation
            {
                Id = Csv.ToInt(parts[0], "id оценки"),
                StudentId = Csv.ToInt(parts[1], "student_id"),
                CriterionId = Csv.ToInt(parts[2], "criterion_id"),
                Score = Csv.ToInt(parts[3], "score"),
                Comment = parts[4],
            });
        }
        book = loaded;
        return true;
    }

    public string ExportTotals(IReadOnlyList<TotalDto> rows, string fileName)
    {
        if (fileName.Contains('/') || fileName.Contains('\\'))
        {
            throw new StorageError("Некорректное имя файла итога");
        }
        string path = Path.Combine(_directory, fileName);
        try
        {
            Directory.CreateDirectory(_directory);
            using StreamWriter output = Csv.OpenWriter(path);
            output.Write("student_id;name;total;strategy\n");
            foreach (TotalDto row in rows)
            {
                output.Write(Invariant(
                    $"{row.StudentId};{Csv.Field(row.StudentName)};{row.Value:F2};{Csv.Field(row.Strategy)}\n"));
            }
            output.Flush();
        }
        catch (Exception error) when (Csv.IsFileError(error))
        {
            throw new StorageError("Не удалось записать " + path + ": " + error.Message, error);
        }
        return path;
    }

    public string Location()
    {
        return _directory;
    }
}

// Разбор и запись CSV. Файл виден только в этом файле, как анонимный namespace в C++.
file static class Csv
{
    public static readonly List<string> StudentsHeader = new() { "id", "name" };
    public static readonly List<string> CriteriaHeader = new() { "id", "name", "max_score", "weight" };
    public static readonly List<string> EvaluationsHeader = new() { "id", "student_id", "criterion_id", "score", "comment" };
    public static readonly List<string> GradeBookFiles = new() { "students.csv", "criteria.csv", "evaluations.csv" };

    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    // Поле с ';' или переводом строки берём в кавычки, кавычку внутри удваиваем.
    public static string Field(string value)
    {
        if (value.IndexOfAny(new[] { ';', '"', '\r', '\n' }) < 0)
        {
            return value;
        }
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static int ToInt(string text, string what)
    {
        if (!int.TryParse(
                text,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite,
                CultureInfo.InvariantCulture,
                out int value))
        {
            throw new StorageError("Не число в поле «" + what + "»: " + text);
        }
        return value;
    }

    public static StreamWriter OpenWriter(string path)
    {
        return new StreamWriter(path, false, Utf8);
    }

    public static bool IsFileError(Exception error)
    {
        return error is IOException || error is UnauthorizedAccessException;
    }

    // Читает записи CSV. Запись не всегда равна строке: поле в кавычках
    // может содержать ';' и перевод строки. Заголовок обязан совпасть точно.
    public static List<List<string>> ReadRecords(string path, List<string> header)
    {
        if (!File.Exists(path))
        {
            throw new StorageError("Нет файла " + path);
        }
        string text;
        try
        {
            text = File.ReadAllText(path, Utf8);
        }
        catch (Exception error) when (IsFileError(error))
        {
            throw new StorageError("Не удалось прочитать " + path, error);
        }

        List<List<string>> records = new();
        List<string> fields = new();
        StringBuilder current = new();
        bool quoted = false;
        bool hasData = false;
        for (int i = 0; i < text.Length; ++i)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch != '"')
                {
                    current.Append(ch);
                }
                else if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    current.Append('"');
                    ++i;
                }
                else
                {
                    quoted = false;
                }
            }
            else if (ch == '"')
            {
                quoted = true;
                hasData = true;
            }
            else if (ch == ';')
            {
                fields.Add(current.ToString());
                current.Clear();
                hasData = true;
            }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    ++i;
                }
                // Пустые строки надо пропустить
                if (hasData)
                {
                    fields.Add(current.ToString());
                    records.Add(fields);
                }
                fields = new List<string>();
                current.Clear();
                hasData = false;
            }
            else
            {
                current.Append(ch);
                hasData = true;
            }
        }
        if (quoted)
        {
            throw new StorageError("Незакрытая кавычка в " + path);
        }
        if (hasData)
        {
            fields.Add(current.ToString());
            records.Add(fields);
        }

        if (records.Count == 0 || !records[0].SequenceEqual(header))
        {
            throw new StorageError("Неверный заголовок в " + path + ", ждали: " + string.Join(";", header));
        }
        records.RemoveAt(0);
        return records;
    }

    public static void RemoveQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
        }
    }

    // Готовые .tmp становятся настоящими файлами. Старые файлы на это время уходят в .bak,
    // и если какое-то переименование не прошло, всё возвращается назад.
    public static void Publish(string directory)
    {
        List<string> backedUp = new();
        List<string> placed = new();

        void Rollback()
        {
            foreach (string name in placed)
            {
                RemoveQuietly(Path.Combine(directory, name));
            }
            foreach (string name in backedUp)
            {
                try
                {
                    File.Move(Path.Combine(directory, name + ".bak"), Path.Combine(directory, name), true);
                }
                catch (Exception)
                {
                }
            }
            foreach (string name in GradeBookFiles)
            {
                RemoveQuietly(Path.Combine(directory, name + ".tmp"));
            }
        }

        foreach (string name in GradeBookFiles)
        {
            string target = Path.Combine(directory, name);
            if (File.Exists(target))
            {
                try
                {
                    File.Move(target, target + ".bak", true);
                }
                catch (Exception error) when (IsFileError(error))
                {
                    Rollback();
                    throw new StorageError("Не удалось сделать резервную копию " + target, error);
                }
                backedUp.Add(name);
            }
        }
        foreach (string name in GradeBookFiles)
        {
            string target = Path.Combine(directory, name);
            try
            {
                File.Move(target + ".tmp", target, true);
            }
            catch (Exception error) when (IsFileError(error))
            {
                Rollback();
                throw new StorageError("Не удалось записать " + target, error);
            }
            placed.Add(name);
        }
        foreach (string name in backedUp)
        {
            RemoveQuietly(Path.Combine(directory, name + ".bak"));
        }
    }
}
