using System.Collections.Generic;

namespace Pos.Domain;

// Список критериев — агрегат. Критерий учитывается только если лежит здесь.
public sealed class Rubric
{
    private readonly List<Criterion> _items = new();
    private int _nextId = 1;

    public CriterionId Add(string name, int maxScore, int weight)
    {
        if (FindByName(name) != null)
        {
            throw new DomainError("Критерий с таким названием уже есть");
        }
        CriterionId id = new(_nextId);
        Criterion created = new(id, name, maxScore, weight);
        _items.Add(created);
        ++_nextId;
        return id;
    }

    public void AddExisting(Criterion criterion)
    {
        if (Find(criterion.Id) != null)
        {
            throw new DomainError("Критерий с таким id уже есть");
        }
        if (FindByName(criterion.Name) != null)
        {
            throw new DomainError("Критерий с таким названием уже есть");
        }
        if (criterion.Id.Value >= _nextId)
        {
            _nextId = criterion.Id.Value + 1;
        }
        _items.Add(criterion);
    }

    public Criterion? Find(CriterionId id)
    {
        foreach (Criterion criterion in _items)
        {
            if (criterion.Id == id)
            {
                return criterion;
            }
        }
        return null;
    }

    public Criterion? FindByName(string name)
    {
        foreach (Criterion criterion in _items)
        {
            if (criterion.Name == name)
            {
                return criterion;
            }
        }
        return null;
    }

    public IReadOnlyList<Criterion> All() => _items.AsReadOnly();
}
