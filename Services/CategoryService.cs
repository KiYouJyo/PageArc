using System.Collections.ObjectModel;
using System.Text.Json;
using PageArc.Models;

namespace PageArc.Services;

public sealed class CategoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();

    public ObservableCollection<CategoryEntry> Categories { get; } = [];

    public void Load(IEnumerable<BookEntry> books)
    {
        AppPaths.Ensure();
        Categories.Clear();
        try
        {
            if (File.Exists(AppPaths.CategoriesFile))
            {
                var items = JsonSerializer.Deserialize<List<CategoryEntry>>(File.ReadAllText(AppPaths.CategoriesFile)) ?? [];
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
            AppPaths.Ensure();
            var temp = AppPaths.CategoriesFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Categories.ToList(), JsonOptions));
            File.Move(temp, AppPaths.CategoriesFile, true);
        }
    }
}
