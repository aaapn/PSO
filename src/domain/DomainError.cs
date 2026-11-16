using System;

namespace Pos.Domain;

// Ошибка нарушения правила предметной области
public class DomainError : Exception
{
    public DomainError(string message) : base(message)
    {
    }
}
