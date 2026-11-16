using System;
using System.Collections.Generic;

namespace Pos.Application;

// Порт приложения: сохранить ведомость и выгрузить итог.
// Формат файла находится снаружи.
// Любая ошибка чтения или записи приходит как StorageError.
public interface IGradeBookStore
{
    void Save(StoredGradeBook book);
    // false — сохранённой ведомости ещё нет.
    bool Load(out StoredGradeBook book);
    string ExportTotals(IReadOnlyList<TotalDto> rows, string fileName);
    string Location();
}

// Происходит ошибка хранилища, если  файла нет, он битый или не записался.
public class StorageError : Exception
{
    public StorageError(string message) : base(message)
    {
    }

    public StorageError(string message, Exception inner) : base(message, inner)
    {
    }
}
