using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.ProDataGrid.Models;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public sealed class CustomDrawingEditingRow : ObservableObject, IDataGridCellDrawOperationItemCache
{
    private SlotEntry[]? _entries;
    private int _id;
    private string _title = string.Empty;
    private string _notes = string.Empty;
    private string _category = string.Empty;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public bool TryGetCellDrawCacheEntry(int cacheSlot, int cacheKey, out object value)
    {
        if (_entries is not null && cacheSlot >= 0 && cacheSlot < _entries.Length)
        {
            SlotEntry entry = _entries[cacheSlot];
            if (entry.HasValue && entry.CacheKey == cacheKey && entry.Value is not null)
            {
                value = entry.Value;
                return true;
            }
        }

        value = null!;
        return false;
    }

    public void SetCellDrawCacheEntry(int cacheSlot, int cacheKey, object value)
    {
        if (cacheSlot < 0) return;

        EnsureCapacity(cacheSlot + 1)[cacheSlot] = new SlotEntry
        {
            HasValue = true,
            CacheKey = cacheKey,
            Value = value
        };
    }

    private SlotEntry[] EnsureCapacity(int minLength)
    {
        if (_entries is null)
        {
            _entries = new SlotEntry[minLength];
            return _entries;
        }

        if (_entries.Length >= minLength) return _entries;

        int newLength = _entries.Length;
        while (newLength < minLength) newLength *= 2;

        var expanded = new SlotEntry[newLength];
        _entries.CopyTo(expanded, 0);
        _entries = expanded;
        return _entries;
    }

    private struct SlotEntry
    {
        public bool HasValue;
        public int CacheKey;
        public object? Value;
    }
}
