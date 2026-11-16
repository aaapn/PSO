using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Pos.Application;
using Pos.Domain;
using Pos.Infrastructure;

namespace Pos.Tests;

internal static class Tests
{
    private static int _failed;

    private static void Check(
        bool cond,
        [CallerArgumentExpression(nameof(cond))] string expr = "",
        [CallerLineNumber] int line = 0)
    {
        if (!cond)
        {
            Console.Error.WriteLine("FAIL " + line + ": " + expr);
            ++_failed;
        }
    }

    // Ждём исключение типа E или наследника.
    private static void ExpectThrows<E>(Action fn, int line) where E : Exception
    {
        try
        {
            fn();
            Console.Error.WriteLine("FAIL " + line + ": ждали " + typeof(E).Name);
            ++_failed;
        }
        catch (E)
        {
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + line + ": другое исключение: " + error.Message);
            ++_failed;
        }
    }

    private static void ExpectError(Action fn, [CallerLineNumber] int line = 0)
    {
        ExpectThrows<DomainError>(fn, line);
    }

    // Ошибки файлов — StorageError.
    private static void ExpectStorage(Action fn, [CallerLineNumber] int line = 0)
    {
        ExpectThrows<StorageError>(fn, line);
    }

    private static bool Near(double left, double right)
    {
        return Math.Abs(left - right) < 1e-6;
    }

    // Отдельный каталог на каждый тест. Он удаляется также когда тест упал исключением.
    private sealed class TempDir : IDisposable
    {
        public TempDir(string name)
        {
            Path = System.IO.Path.Combine("testdata_pos", name);
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (Exception)
            {
            }
        }
    }

    private static void WriteFile(string path, string text)
    {
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    // Фасад с репозиториями в памяти и CSV в каталоге dir как в Container.
    private sealed class TestPos
    {
        public TestPos(string dir)
        {
            Store = new CsvGradeBookStore(dir);
            Pos = new PosFacade(Students, Rubrics, Evaluations, Store);
        }

        public MemoryStudentRepository Students { get; } = new();
        public MemoryRubricRepository Rubrics { get; } = new();
        public MemoryEvaluationRepository Evaluations { get; } = new();
        public CsvGradeBookStore Store { get; }
        public PosFacade Pos { get; }
    }

    // Репозиторий, который можно заставить отказать при подмене.
    private sealed class FailingEvaluationRepository : MemoryEvaluationRepository
    {
        public bool Fail { get; set; }

        public override void ReplaceAll(IEnumerable<Evaluation> evaluations)
        {
            if (Fail)
            {
                throw new DomainError("репозиторий отказал");
            }
            base.ReplaceAll(evaluations);
        }
    }

    private const string StudentsCsv = "id;name\n1;Иванов Иван\n2;Петрова Мария\n";
    private const string CriteriaCsv = "id;name;max_score;weight\n1;Посещаемость;10;2\n2;Семинары;10;3\n";
    private const string EvaluationsCsv = "id;student_id;criterion_id;score;comment\n1;1;1;10;ходил\n";

    private static void WriteGradeBook(TempDir dir, string students, string criteria, string evaluations)
    {
        WriteFile(dir.File("students.csv"), students);
        WriteFile(dir.File("criteria.csv"), criteria);
        WriteFile(dir.File("evaluations.csv"), evaluations);
    }

    private static void TestScoreAndCriterion()
    {
        ExpectError(() => Score.Of(12, 10));
        ExpectError(() => Score.Of(-1, 10));
        Score ok = Score.Of(0, 10);
        Check(ok.Value == 0);
        Check(Score.Of(10, 10).Value == 10);

        ExpectError(() => new Criterion(new CriterionId(1), "Пустой максимум", 0, 1));
        ExpectError(() => new Criterion(new CriterionId(1), "Нулевой вес", 10, 0));
        ExpectError(() => new Student(new StudentId(1), "   "));
        ExpectError(() => new Student(new StudentId(1), ""));
        // За ';' следит CSV-адаптера, а домен такое имя принимает
        Check(new Student(new StudentId(1), "Иванов;Иван").Name == "Иванов;Иван");
        ExpectError(() => new Student(new StudentId(0), "Иван"));
    }

    private static void TestRubricAndFactory()
    {
        Rubric rubric = new();
        CriterionId lectures = rubric.Add("Посещаемость", 10, 2);
        ExpectError(() => rubric.Add("Посещаемость", 10, 1));
        Check(rubric.All().Count == 1);

        Student ivan = new(new StudentId(1), "Иванов Иван");
        EvaluationFactory factory = new();
        ExpectError(() => factory.Create(ivan, rubric, new CriterionId(99), 5, ""));

        Evaluation evaluation = factory.Create(ivan, rubric, lectures, 8, "ходил");
        Check(evaluation.Score.Value == 8);
        Check(evaluation.StudentId == ivan.Id);

        Criterion criterion = rubric.Find(lectures)!;
        ExpectError(() => evaluation.Correct(criterion, 12, ""));
        evaluation.Correct(criterion, 4, "исправлено");
        Check(evaluation.Score.Value == 4);
        Check(evaluation.Comment == "исправлено");
    }

    private static void TestTotals()
    {
        Rubric rubric = new();
        CriterionId lectures = rubric.Add("Посещаемость", 10, 2);
        CriterionId seminars = rubric.Add("Семинары", 10, 3);
        CriterionId homework = rubric.Add("Домашние задания", 10, 5);

        Student ivan = new(new StudentId(1), "Иванов Иван");
        Student maria = new(new StudentId(2), "Петрова Мария");
        EvaluationFactory factory = new();

        List<Evaluation> all = new()
        {
            factory.Create(ivan, rubric, lectures, 10, ""),
            factory.Create(ivan, rubric, seminars, 8, ""),
            factory.Create(ivan, rubric, homework, 9, ""),
            factory.Create(maria, rubric, lectures, 6, ""),
            factory.Create(maria, rubric, seminars, 7, ""),
        };

        OnlyGradedStrategy onlyGraded = new();
        MissingAsZeroStrategy missingAsZero = new();
        TotalCalculator gradedCalc = new(onlyGraded);
        TotalCalculator zeroCalc = new(missingAsZero);

        List<Student> students = new() { ivan, maria };
        List<TotalLine> graded = gradedCalc.Calculate(students, rubric, all);
        List<TotalLine> zero = zeroCalc.Calculate(students, rubric, all);

        // 10/10*2 + 8/10*3 + 9/10*5) / 10 * 10 = 8.9
        Check(Near(graded[0].Value, 8.9));
        Check(Near(zero[0].Value, 8.9));

        Check(Near(graded[1].Value, 6.6));
        Check(Near(zero[1].Value, 3.3));

        Evaluation again = factory.Create(ivan, rubric, lectures, 1, "");
        List<Evaluation> duplicated = new() { all[0], again };
        ExpectError(() => onlyGraded.ScoreOutOfTen(rubric, duplicated));

        // Если студента нет в списке, но у него есть оценка, то это ошибка.
        Student stranger = new(new StudentId(3), "Сидоров");
        List<Evaluation> withStranger = new(all) { factory.Create(stranger, rubric, lectures, 5, "") };
        ExpectError(() => gradedCalc.Calculate(students, rubric, withStranger));
    }

    private static void TestFacadeAndCsv()
    {
        using TempDir temp = new("facade");
        string dir = temp.Path;

        MemoryStudentRepository students = new();
        MemoryRubricRepository rubrics = new();
        MemoryEvaluationRepository evaluations = new();
        CsvGradeBookStore store = new(dir);
        PosFacade pos = new(students, rubrics, evaluations, store);

        StudentDto ivan = pos.AddStudent("Иванов Иван");
        StudentDto maria = pos.AddStudent("Петрова Мария");
        CriterionDto lectures = pos.AddCriterion("Посещаемость", 10, 2);
        CriterionDto seminars = pos.AddCriterion("Семинары", 10, 3);
        CriterionDto homework = pos.AddCriterion("Домашние задания", 10, 5);

        pos.Grade(ivan.Id, lectures.Id, 10, "ходил");
        pos.Grade(ivan.Id, seminars.Id, 8, "");
        pos.Grade(ivan.Id, homework.Id, 9, "");
        pos.Grade(maria.Id, lectures.Id, 6, "");
        EvaluationDto mariaSeminar = pos.Grade(maria.Id, seminars.Id, 7, "");

        ExpectError(() => pos.Grade(maria.Id, homework.Id, 12, ""));
        ExpectError(() => pos.Grade(ivan.Id, 99, 5, ""));
        ExpectError(() => pos.Grade(ivan.Id, lectures.Id, 5, "вторая"));

        List<TotalDto> before = pos.Totals(TotalPolicy.MissingAsZero);
        Check(Near(before[1].Value, 3.3));

        pos.Grade(maria.Id, homework.Id, 10, "досдала");
        List<TotalDto> full = pos.Totals(TotalPolicy.OnlyGraded);
        Check(Near(full[1].Value, 8.3));

        pos.CorrectGrade(mariaSeminar.Id, 10, "было опоздание");
        // 6/10*2 + 10/10*3 + 10/10*5 = 1.2 + 3 + 5 = 9.2
        Check(Near(pos.Totals(TotalPolicy.OnlyGraded)[1].Value, 9.2));

        int homeworkGradeId = 0;
        foreach (EvaluationDto grade in pos.Grades())
        {
            if (grade.StudentId == maria.Id && grade.CriterionId == homework.Id)
            {
                homeworkGradeId = grade.Id;
            }
        }
        pos.CancelGrade(homeworkGradeId);
        Check(Near(pos.Totals(TotalPolicy.MissingAsZero)[1].Value, 4.2));
        Check(Near(pos.Totals(TotalPolicy.OnlyGraded)[1].Value, 8.4));

        pos.Save();
        string exported = pos.ExportTotals(TotalPolicy.OnlyGraded);
        Check(File.Exists(exported));

        MemoryStudentRepository students2 = new();
        MemoryRubricRepository rubrics2 = new();
        MemoryEvaluationRepository evaluations2 = new();
        CsvGradeBookStore store2 = new(dir);
        PosFacade loaded = new(students2, rubrics2, evaluations2, store2);
        loaded.Load();
        Check(loaded.Students().Count == 2);
        Check(loaded.Criteria().Count == 3);
        Check(loaded.Grades().Count == 5);
        Check(Near(loaded.Totals(TotalPolicy.OnlyGraded)[0].Value, 8.9));
        Check(Near(loaded.Totals(TotalPolicy.MissingAsZero)[1].Value, 4.2));

        // Записали и прочитали — данные те же, поле в поле
        List<EvaluationDto> saved = pos.Grades();
        List<EvaluationDto> restored = loaded.Grades();
        Check(saved.Count == restored.Count);
        for (int i = 0; i < saved.Count && i < restored.Count; ++i)
        {
            Check(saved[i].Id == restored[i].Id);
            Check(saved[i].StudentId == restored[i].StudentId);
            Check(saved[i].CriterionId == restored[i].CriterionId);
            Check(saved[i].Score == restored[i].Score);
            Check(saved[i].Comment == restored[i].Comment);
        }
    }

    private static void TestMissingCsvFiles()
    {
        using TempDir dir = new("missing");
        CsvGradeBookStore store = new(dir.Path);
        StoredGradeBook book;

        // Если нет ни одного файла, то ведомость  ещё не сохраняли.
        Check(!store.Load(out book));
        TestPos empty = new(dir.Path);
        ExpectError(() => empty.Pos.Load());

        // Если нет evaluations.csv, то нельзя  загрузить ведомость без оценок
        WriteFile(dir.File("students.csv"), StudentsCsv);
        WriteFile(dir.File("criteria.csv"), CriteriaCsv);
        ExpectStorage(() => store.Load(out book));
        try
        {
            store.Load(out book);
        }
        catch (StorageError error)
        {
            Check(error.Message.Contains("evaluations.csv"));
        }

        // evaluations.csv
        File.Delete(dir.File("students.csv"));
        File.Delete(dir.File("criteria.csv"));
        WriteFile(dir.File("evaluations.csv"), EvaluationsCsv);
        ExpectStorage(() => store.Load(out book));

        // Это нормальная пустая ведомость с заголовком.
        WriteGradeBook(dir, StudentsCsv, CriteriaCsv, "id;student_id;criterion_id;score;comment\n");
        Check(store.Load(out book));
        Check(book.Students.Count == 2);
        Check(book.Criteria.Count == 2);
        Check(book.Evaluations.Count == 0);

        // Допускаетс неполный набор файлов.
        TestPos current = new(dir.Path);
        current.Pos.AddStudent("Сидоров");
        File.Delete(dir.File("evaluations.csv"));
        ExpectStorage(() => current.Pos.Load());
        Check(current.Pos.Students().Count == 1);
    }

    private static void TestBrokenCsv()
    {
        using TempDir dir = new("broken");
        TestPos current = new(dir.Path);
        StudentDto kept = current.Pos.AddStudent("Сидоров");
        CriterionDto keptCriterion = current.Pos.AddCriterion("Экзамен", 10, 1);
        current.Pos.Grade(kept.Id, keptCriterion.Id, 7, "");

        // Чужой заголовок не наша ведомость
        WriteGradeBook(dir, "id;fio\n1;Иванов\n", CriteriaCsv, EvaluationsCsv);
        ExpectStorage(() => current.Pos.Load());
        // Пустой файл без заголовка
        WriteGradeBook(dir, "", CriteriaCsv, EvaluationsCsv);
        ExpectStorage(() => current.Pos.Load());
        // Битая строка: не хватает поля
        WriteGradeBook(dir, StudentsCsv, "id;name;max_score;weight\n1;Посещаемость;10\n", EvaluationsCsv);
        ExpectStorage(() => current.Pos.Load());
        // Не число
        WriteGradeBook(dir, StudentsCsv, CriteriaCsv, "id;student_id;criterion_id;score;comment\n1;1;1;десять;\n");
        ExpectStorage(() => current.Pos.Load());
        // Незакрытая кавычка
        WriteGradeBook(dir, "id;name\n1;\"Иванов\n", CriteriaCsv, EvaluationsCsv);
        ExpectStorage(() => current.Pos.Load());
        // Ссылка на студента, которого нет
        WriteGradeBook(dir, StudentsCsv, CriteriaCsv, "id;student_id;criterion_id;score;comment\n1;99;1;5;\n");
        ExpectError(() => current.Pos.Load());
        // Ссылка на критерий, которого нет
        WriteGradeBook(dir, StudentsCsv, CriteriaCsv, "id;student_id;criterion_id;score;comment\n1;1;99;5;\n");
        ExpectError(() => current.Pos.Load());
        // Повторный id студента
        WriteGradeBook(dir, "id;name\n1;Иванов\n1;Петрова\n", CriteriaCsv, EvaluationsCsv);
        ExpectError(() => current.Pos.Load());
        // Повторный id оценки
        WriteGradeBook(dir, StudentsCsv, CriteriaCsv, "id;student_id;criterion_id;score;comment\n1;1;1;5;\n1;1;2;5;\n");
        ExpectError(() => current.Pos.Load());
        // Балл вне диапазона
        WriteGradeBook(dir, StudentsCsv, CriteriaCsv, "id;student_id;criterion_id;score;comment\n1;1;1;12;\n");
        ExpectError(() => current.Pos.Load());

        // После всех проблем в памяти имеем ту же ведомость
        Check(current.Pos.Students().Count == 1);
        Check(current.Pos.Students()[0].Name == "Сидоров");
        Check(current.Pos.Criteria().Count == 1);
        Check(current.Pos.Grades().Count == 1);
        Check(Near(current.Pos.Totals(TotalPolicy.OnlyGraded)[0].Value, 7.0));
    }

    private static void TestLoadRollback()
    {
        using TempDir dir = new("rollback");
        TestPos current = new(dir.Path);
        StudentDto ivan = current.Pos.AddStudent("Иванов Иван");
        CriterionDto lectures = current.Pos.AddCriterion("Посещаемость", 10, 2);
        CriterionDto seminars = current.Pos.AddCriterion("Семинары", 10, 3);
        current.Pos.Grade(ivan.Id, lectures.Id, 8, "");
        double totalBefore = current.Pos.Totals(TotalPolicy.OnlyGraded)[0].Value;

        // Студенты и критерии в файле корректны, а id оценки нет => ломается на последнем шаге сборки.
        WriteGradeBook(dir,
            "id;name\n7;Петрова Мария\n",
            "id;name;max_score;weight\n5;ДЗ;10;1\n",
            "id;student_id;criterion_id;score;comment\n0;7;5;5;\n");
        ExpectError(() => current.Pos.Load());

        Check(current.Pos.Students().Count == 1);
        Check(current.Pos.Students()[0].Name == "Иванов Иван");
        Check(current.Pos.Criteria().Count == 2);
        Check(current.Pos.Grades().Count == 1);
        Check(Near(current.Pos.Totals(TotalPolicy.OnlyGraded)[0].Value, totalBefore));
        // Следующие id те же, что были до неудачной загрузки.
        StudentDto next = current.Pos.AddStudent("Новый");
        Check(next.Id == 2);
        Check(current.Pos.AddCriterion("Новый критерий", 10, 1).Id == 3);
        Check(current.Pos.Grade(ivan.Id, seminars.Id, 9, "").Id == 2);

        // Удачная загрузка заменяет ведомость целиком, а повторная не плодит дубли.
        WriteGradeBook(dir, StudentsCsv, CriteriaCsv, EvaluationsCsv);
        current.Pos.Load();
        Check(current.Pos.Students().Count == 2);
        Check(current.Pos.Criteria().Count == 2);
        Check(current.Pos.Grades().Count == 1);
        current.Pos.Load();
        Check(current.Pos.Students().Count == 2);
        Check(current.Pos.Grades().Count == 1);
        // Счётчики продолжают файл
        Check(current.Pos.AddStudent("Третий").Id == 3);
        Check(current.Pos.Grade(2, 1, 6, "").Id == 2);
    }

    private static void TestRollbackWhenRepositoryFails()
    {
        using TempDir dir = new("repo_fail");
        WriteGradeBook(dir, StudentsCsv, CriteriaCsv, EvaluationsCsv);

        MemoryStudentRepository students = new();
        MemoryRubricRepository rubrics = new();
        FailingEvaluationRepository evaluations = new();
        CsvGradeBookStore store = new(dir.Path);
        PosFacade pos = new(students, rubrics, evaluations, store);

        StudentDto ivan = pos.AddStudent("Сидоров");
        CriterionDto exam = pos.AddCriterion("Экзамен", 10, 1);
        pos.Grade(ivan.Id, exam.Id, 4, "");

        // Студенты и список критериев уже подменены, а оценки — нет. Фасад обязан вернуть всё назад.
        evaluations.Fail = true;
        ExpectError(() => pos.Load());
        Check(pos.Students().Count == 1);
        Check(pos.Students()[0].Name == "Сидоров");
        Check(pos.Criteria().Count == 1);
        Check(pos.Criteria()[0].Name == "Экзамен");
        Check(pos.Grades().Count == 1);
        Check(pos.AddStudent("Новый").Id == 2);
    }

    private static void TestUpdatePairInvariant()
    {
        Rubric rubric = new();
        CriterionId lectures = rubric.Add("Посещаемость", 10, 2);
        CriterionId seminars = rubric.Add("Семинары", 10, 3);
        Student ivan = new(new StudentId(1), "Иванов Иван");
        EvaluationFactory factory = new();
        MemoryEvaluationRepository repository = new();

        Evaluation first = factory.Create(ivan, rubric, lectures, 8, "");
        repository.Add(first);
        repository.Add(factory.Create(ivan, rubric, seminars, 6, ""));

        // Балл и комментарий у той же пары менять можно
        first.Correct(rubric.Find(lectures)!, 9, "исправлено");
        repository.Update(first);
        Check(repository.Find(first.Id)!.Score.Value == 9);

        // Та же оценка, но с занятой парой «Иванов + Семинары»
        Evaluation moved = factory.Restore(ivan, rubric, first.Id, seminars, 2, "");
        ExpectError(() => repository.Update(moved));
        // С другим студентом
        Student maria = new(new StudentId(2), "Петрова Мария");
        Evaluation otherStudent = factory.Restore(maria, rubric, first.Id, lectures, 2, "");
        ExpectError(() => repository.Update(otherStudent));

        Evaluation after = repository.Find(first.Id)!;
        Check(after.CriterionId == lectures);
        Check(after.StudentId == ivan.Id);
        Check(after.Score.Value == 9);
        Check(after.Comment == "исправлено");
        Check(repository.All().Count == 2);

        // Оценка - это ссылочный тип, поэтому исправленная снаружи копия
        // не меняет репозиторий в обход Update
        Evaluation outside = repository.Find(first.Id)!;
        outside.Correct(rubric.Find(lectures)!, 1, "мимо update");
        Check(repository.Find(first.Id)!.Score.Value == 9);
        repository.All()[0].Correct(rubric.Find(lectures)!, 2, "мимо update");
        Check(repository.Find(first.Id)!.Score.Value == 9);

        // Неизвестная оценка
        Evaluation unknown = factory.Restore(ivan, rubric, new EvaluationId(50), lectures, 1, "");
        ExpectError(() => repository.Update(unknown));
    }

    private static void TestCsvSpecialText()
    {
        using TempDir dir = new("special");
        TestPos source = new(dir.Path);
        const string name = "Иванов; \"Ваня\" Иван";
        const string criterionName = "Семинары; доклад";
        const string comment = "ответ; \"почти\" верный\nвторая строка\r\nтретья";

        StudentDto ivan = source.Pos.AddStudent(name);
        CriterionDto seminar = source.Pos.AddCriterion(criterionName, 10, 1);
        source.Pos.Grade(ivan.Id, seminar.Id, 7, comment);
        source.Pos.Save();
        source.Pos.ExportTotals(TotalPolicy.OnlyGraded);

        TestPos loaded = new(dir.Path);
        loaded.Pos.Load();
        Check(loaded.Pos.Students().Count == 1);
        Check(loaded.Pos.Students()[0].Name == name);
        Check(loaded.Pos.Criteria()[0].Name == criterionName);
        Check(loaded.Pos.Grades().Count == 1);
        Check(loaded.Pos.Grades()[0].Comment == comment);

        // Пустое имя, поэтому нельзя
        ExpectError(() => loaded.Pos.AddStudent("  "));
    }

    private static void TestSaveFailure()
    {
        using TempDir dir = new("save_fail");
        TestPos current = new(dir.Path);
        StudentDto ivan = current.Pos.AddStudent("Иванов Иван");
        CriterionDto exam = current.Pos.AddCriterion("Экзамен", 10, 1);
        current.Pos.Grade(ivan.Id, exam.Id, 5, "");
        current.Pos.Save();

        current.Pos.AddStudent("Петрова Мария");

        // Временный файл не создать, т к на его месте каталог
        Directory.CreateDirectory(Path.Combine(dir.File("evaluations.csv.tmp"), "занято"));
        ExpectStorage(() => current.Pos.Save());
        Directory.Delete(dir.File("evaluations.csv.tmp"), true);
        {
            TestPos reread = new(dir.Path);
            reread.Pos.Load();
            Check(reread.Pos.Students().Count == 1);
            Check(reread.Pos.Grades().Count == 1);
        }
        Check(!File.Exists(dir.File("students.csv.tmp")));

        // Резервную копию не сделать, т к часть файлов уже переехала в .bak, их надо вернуть
        Directory.CreateDirectory(Path.Combine(dir.File("criteria.csv.bak"), "занято"));
        ExpectStorage(() => current.Pos.Save());
        Directory.Delete(dir.File("criteria.csv.bak"), true);
        {
            TestPos reread = new(dir.Path);
            reread.Pos.Load();
            Check(reread.Pos.Students().Count == 1);
            Check(reread.Pos.Grades().Count == 1);
        }

        // Когда ничего не мешает, сохраняется новое состояние
        current.Pos.Save();
        {
            TestPos reread = new(dir.Path);
            reread.Pos.Load();
            Check(reread.Pos.Students().Count == 2);
        }
        Check(!File.Exists(dir.File("students.csv.bak")));

        // Экспорт не делает вид, что всё записал
        Directory.CreateDirectory(Path.Combine(dir.File("totals_only_graded.csv"), "занято"));
        ExpectStorage(() => current.Pos.ExportTotals(TotalPolicy.OnlyGraded));
    }

    private static void RemoveTestData()
    {
        if (Directory.Exists("testdata_pos"))
        {
            Directory.Delete("testdata_pos", true);
        }
    }

    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            TestScoreAndCriterion();
            TestRubricAndFactory();
            TestTotals();
            TestFacadeAndCsv();
            TestMissingCsvFiles();
            TestBrokenCsv();
            TestLoadRollback();
            TestRollbackWhenRepositoryFails();
            TestUpdatePairInvariant();
            TestCsvSpecialText();
            TestSaveFailure();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Тест упал исключением: " + error.Message);
            RemoveTestData();
            return 1;
        }
        RemoveTestData();
        if (_failed != 0)
        {
            Console.Error.WriteLine("Провалено проверок: " + _failed);
            return 1;
        }
        Console.WriteLine("Все тесты прошли");
        return 0;
    }
}
