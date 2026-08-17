using System.Collections.ObjectModel;
using System.Text.Json;
using PageArc.Models;

namespace PageArc.Services;

public sealed class CategoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _categoriesFile;

    public CategoryService(string? categoriesFile = null)
    {
        _categoriesFile = string.IsNullOrWhiteSpace(categoriesFile) ? AppPaths.CategoriesFile : Path.GetFullPath(categoriesFile);
    }

    public ObservableCollection<CategoryEntry> Categories { get; } = [];

    public void Load(IEnumerable<BookEntry> books)
    {
        EnsureStorage();
        Categories.Clear();
        try
        {
            if (File.Exists(_categoriesFile))
            {
                var items = JsonSerializer.Deserialize<List<CategoryEntry>>(File.ReadAllText(_categoriesFile)) ?? [];
                foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.Name))) Categories.Add(item);
            }
        }
        catch
        {
            Categories.Clear();
        }

        var changed = false;
        foreach (var name in books.Select(x => x.Collection).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.CurrentCultureIgnoreCase))
        {
            if (Categories.Any(x => string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase))) continue;
            Categories.Add(new CategoryEntry { Name = name! });
            changed = true;
        }
        if (changed) Save();
    }

    public CategoryEntry Add(string name)
    {
        var normalized = name.Trim();
        if (normalized.Length == 0) throw new ArgumentException("Category name cannot be empty.", nameof(name));
        var existing = Categories.FirstOrDefault(x => string.Equals(x.Name, normalized, StringComparison.CurrentCultureIgnoreCase));
        if (existing is not null) return existing;

        var category = new CategoryEntry { Name = normalized };
        Categories.Add(category);
        Save();
        return category;
    }

    public void Save()
    {
        lock (_gate)
        {
            EnsureStorage();
            var temp = _categoriesFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Categories.ToList(), JsonOptions));
            File.Move(temp, _categoriesFile, true);
        }
    }

    private void EnsureStorage()
    {
        if (string.Equals(_categoriesFile, AppPaths.CategoriesFile, StringComparison.OrdinalIgnoreCase))
            AppPaths.Ensure();
        var directory = Path.GetDirectoryName(_categoriesFile);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
    }
}
