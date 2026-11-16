using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Pos.Application;
using Pos.Domain;

namespace Pos.Infrastructure;

// Адаптер консоли, он про домен и файлы не знает, только про фасад.
public sealed class ConsoleUi
{
    private readonly PosFacade _pos;

    public ConsoleUi(PosFacade pos)
    {
        _pos = pos;
    }

    public void Run()
    {
        Console.WriteLine("ПОС - преподавательская оценка студентов");
        Console.WriteLine("Ведомость пишется в каталог " + _pos.StoragePath());
        while (true)
        {
            Console.Write("\n1  Добавить студента\n"
                          + "2  Список студентов\n"
                          + "3  Добавить критерий\n"
                          + "4  Список критериев\n"
                          + "5  Выставить оценку\n"
                          + "6  Список оценок\n"
                          + "7  Исправить оценку\n"
                          + "8  Отменить оценку\n"
                          + "9  Показать итог\n"
                          + "10 Экспорт итога в CSV\n"
                          + "11 Сохранить ведомость\n"
                          + "12 Загрузить ведомость\n"
                          + "13 Демо-сценарий\n"
                          + "0  Выход\n");
            int choice = ReadInt("> ");
            try
            {
                if (choice == 0)
                {
                    return;
                }
                else if (choice == 1)
                {
                    string name = ReadLine("Имя: ");
                    StudentDto student = _pos.AddStudent(name);
                    Console.WriteLine("Добавлен студент " + student.Id + " " + student.Name);
                }
                else if (choice == 2)
                {
                    PrintStudents();
                }
                else if (choice == 3)
                {
                    string name = ReadLine("Название: ");
                    int maxScore = ReadInt("Максимум балла: ");
                    int weight = ReadInt("Вес: ");
                    CriterionDto criterion = _pos.AddCriterion(name, maxScore, weight);
                    Console.WriteLine("Добавлен критерий " + criterion.Id + " " + criterion.Name);
                }
                else if (choice == 4)
                {
                    PrintCriteria();
                }
                else if (choice == 5)
                {
                    PrintStudents();
                    PrintCriteria();
                    int studentId = ReadInt("Id студента: ");
                    int criterionId = ReadInt("Id критерия: ");
                    int score = ReadInt("Балл: ");
                    string comment = ReadLine("Комментарий (можно пустой): ");
                    EvaluationDto grade = _pos.Grade(studentId, criterionId, score, comment);
                    Console.WriteLine("Оценка " + grade.Id + " поставлена.");
                }
                else if (choice == 6)
                {
                    PrintGrades();
                }
                else if (choice == 7)
                {
                    PrintGrades();
                    int id = ReadInt("Id оценки: ");
                    int score = ReadInt("Новый балл: ");
                    string comment = ReadLine("Новый комментарий: ");
                    _pos.CorrectGrade(id, score, comment);
                    Console.WriteLine("Оценка исправлена.");
                }
                else if (choice == 8)
                {
                    PrintGrades();
                    int id = ReadInt("Id оценки: ");
                    _pos.CancelGrade(id);
                    Console.WriteLine("Оценка отменена.");
                }
                else if (choice == 9)
                {
                    int which = ReadInt("1 — только выставленные, 2 — пустой как ноль: ");
                    if (which == 1)
                    {
                        PrintTotals(TotalPolicy.OnlyGraded);
                    }
                    else if (which == 2)
                    {
                        PrintTotals(TotalPolicy.MissingAsZero);
                    }
                    else
                    {
                        Console.WriteLine("Нужно 1 или 2.");
                    }
                }
                else if (choice == 10)
                {
                    int which = ReadInt("1 — только выставленные, 2 — пустой как ноль: ");
                    if (which == 1)
                    {
                        Console.WriteLine("Записано: " + _pos.ExportTotals(TotalPolicy.OnlyGraded));
                    }
                    else if (which == 2)
                    {
                        Console.WriteLine("Записано: " + _pos.ExportTotals(TotalPolicy.MissingAsZero));
                    }
                    else
                    {
                        Console.WriteLine("Нужно 1 или 2.");
                    }
                }
                else if (choice == 11)
                {
                    _pos.Save();
                    Console.WriteLine("Ведомость сохранена в " + _pos.StoragePath());
                }
                else if (choice == 12)
                {
                    _pos.Load();
                    Console.WriteLine("Ведомость загружена из " + _pos.StoragePath());
                }
                else if (choice == 13)
                {
                    Demo();
                }
                else
                {
                    Console.WriteLine("Нет такого пункта.");
                }
            }
            catch (DomainError error)
            {
                Console.WriteLine("Нельзя: " + error.Message);
            }
            catch (StorageError error)
            {
                // Файл не прочитался или не записался: сообщаем и остаёмся в меню
                Console.WriteLine("Ошибка: " + error.Message);
            }
        }
    }

    public int Demo()
    {
        Console.WriteLine("\n=== Сквозной сценарий ПОС ===");
        try
        {
            StudentDto ivan = _pos.AddStudent("Иванов Иван");
            StudentDto maria = _pos.AddStudent("Петрова Мария");
            Console.WriteLine("Студенты заведены:");
            PrintStudents();

            CriterionDto lectures = _pos.AddCriterion("Посещаемость", 10, 2);
            CriterionDto seminars = _pos.AddCriterion("Семинары", 10, 3);
            CriterionDto homework = _pos.AddCriterion("Домашние задания", 10, 5);
            Console.WriteLine("Критерии заведены:");
            PrintCriteria();

            _pos.Grade(ivan.Id, lectures.Id, 10, "ходил");
            _pos.Grade(ivan.Id, seminars.Id, 8, "");
            _pos.Grade(ivan.Id, homework.Id, 9, "сдано");
            _pos.Grade(maria.Id, lectures.Id, 6, "");
            _pos.Grade(maria.Id, seminars.Id, 7, "опоздание");
            Console.WriteLine("Оценки выставлены. У Марии нет оценки за ДЗ — так стратегии расходятся.");
            PrintGrades();

            Console.WriteLine("\nПробуем поставить 12 из 10:");
            try
            {
                _pos.Grade(maria.Id, homework.Id, 12, "слишком много");
                Console.WriteLine("Ошибка: балл 12 прошёл, так быть не должно.");
                return 1;
            }
            catch (DomainError error)
            {
                Console.WriteLine("  отклонён: " + error.Message);
            }

            Console.WriteLine("Пробуем оценить критерий, которого нет:");
            try
            {
                _pos.Grade(ivan.Id, 99, 5, "");
                Console.WriteLine("Ошибка: чужой критерий прошёл.");
                return 1;
            }
            catch (DomainError error)
            {
                Console.WriteLine("  отклонён: " + error.Message);
            }

            Console.WriteLine();
            PrintTotals(TotalPolicy.OnlyGraded);
            PrintTotals(TotalPolicy.MissingAsZero);

            _pos.Save();
            string gradedFile = _pos.ExportTotals(TotalPolicy.OnlyGraded);
            string zeroFile = _pos.ExportTotals(TotalPolicy.MissingAsZero);
            Console.WriteLine("Ведомость сохранена в " + _pos.StoragePath());
            Console.WriteLine("Итог выгружен: " + gradedFile);
            Console.WriteLine("Итог выгружен: " + zeroFile);
            Console.WriteLine("=== Конец сценария ===");
            return 0;
        }
        catch (Exception error) when (error is DomainError || error is StorageError)
        {
            Console.WriteLine("Сценарий сломался: " + error.Message);
            return 1;
        }
    }

    private void PrintStudents()
    {
        List<StudentDto> students = _pos.Students();
        if (students.Count == 0)
        {
            Console.WriteLine("Студентов пока нет.");
            return;
        }
        Console.WriteLine("Студенты:");
        foreach (StudentDto student in students)
        {
            Console.WriteLine("  " + student.Id + "  " + student.Name);
        }
    }

    private void PrintCriteria()
    {
        List<CriterionDto> criteria = _pos.Criteria();
        if (criteria.Count == 0)
        {
            Console.WriteLine("Критериев пока нет.");
            return;
        }
        Console.WriteLine("Критерии:");
        foreach (CriterionDto criterion in criteria)
        {
            Console.WriteLine("  " + criterion.Id + "  " + criterion.Name
                              + "  max=" + criterion.MaxScore
                              + "  вес=" + criterion.Weight);
        }
    }

    private void PrintGrades()
    {
        List<EvaluationDto> grades = _pos.Grades();
        if (grades.Count == 0)
        {
            Console.WriteLine("Оценок пока нет.");
            return;
        }
        Console.WriteLine("Оценки:");
        foreach (EvaluationDto grade in grades)
        {
            string line = "  " + grade.Id + "  " + grade.StudentName
                          + " / " + grade.CriterionName
                          + "  " + grade.Score + "/" + grade.MaxScore;
            if (grade.Comment.Length != 0)
            {
                line += "  (" + grade.Comment + ")";
            }
            Console.WriteLine(line);
        }
    }

    private void PrintTotals(TotalPolicy policy)
    {
        List<TotalDto> rows = _pos.Totals(policy);
        if (rows.Count == 0)
        {
            Console.WriteLine("Студентов нет, считать нечего.");
            return;
        }
        Console.WriteLine("Итог (" + rows[0].Strategy + "):");
        foreach (TotalDto row in rows)
        {
            Console.WriteLine("  " + row.StudentName + "  "
                              + row.Value.ToString("F2", CultureInfo.InvariantCulture) + " / 10");
        }
    }

    private static string ReadLine(string prompt)
    {
        Console.Write(prompt);
        string? line = Console.ReadLine();
        if (line == null)
        {
            throw new EndOfStreamException("Ввод закончился");
        }
        return line;
    }

    private static int ReadInt(string prompt)
    {
        while (true)
        {
            string line = ReadLine(prompt);
            if (int.TryParse(
                    line,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite,
                    CultureInfo.InvariantCulture,
                    out int value))
            {
                return value;
            }
            Console.WriteLine("Нужно целое число.");
        }
    }
}
