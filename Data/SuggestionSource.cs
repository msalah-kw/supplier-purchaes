namespace SupplierPurchases.Data;

/// <summary>
/// مصدر اقتراحات سريع يحتفظ بنسخة مُطبَّعة من الأسماء مرة واحدة،
/// فتتم التصفية أثناء الكتابة بدون إعادة معالجة النصوص في كل ضغطة زر.
/// </summary>
public sealed class SuggestionSource
{
    private string[] _names = [];
    private string[] _normalized = [];

    public int Count => _names.Length;

    public IReadOnlyList<string> Names => _names;

    public void SetItems(IEnumerable<string> names)
    {
        _names = names as string[] ?? names.ToArray();
        _normalized = new string[_names.Length];
        for (var i = 0; i < _names.Length; i++)
        {
            _normalized[i] = ArabicText.Normalize(_names[i]);
        }
    }

    public bool Contains(string name)
    {
        var normalized = ArabicText.Normalize(name);
        return Array.IndexOf(_normalized, normalized) >= 0;
    }

    /// <summary>يصفّي الأسماء بكل الكلمات المكتوبة، ويقدّم المطابقات التي تبدأ بالكلمة الأولى.</summary>
    public List<string> Filter(string? query, int limit = 300)
    {
        var results = new List<string>(Math.Min(limit, _names.Length));

        if (string.IsNullOrWhiteSpace(query))
        {
            for (var i = 0; i < _names.Length && results.Count < limit; i++)
            {
                results.Add(_names[i]);
            }

            return results;
        }

        var keywords = ArabicText.Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (keywords.Length == 0)
        {
            return results;
        }

        List<string>? partialMatches = null;

        for (var i = 0; i < _names.Length; i++)
        {
            var candidate = _normalized[i];
            var matchesAll = true;

            foreach (var keyword in keywords)
            {
                if (!candidate.Contains(keyword, StringComparison.Ordinal))
                {
                    matchesAll = false;
                    break;
                }
            }

            if (!matchesAll)
            {
                continue;
            }

            if (candidate.StartsWith(keywords[0], StringComparison.Ordinal))
            {
                if (results.Count < limit)
                {
                    results.Add(_names[i]);
                }
            }
            else
            {
                (partialMatches ??= []).Add(_names[i]);
            }
        }

        if (partialMatches is not null)
        {
            for (var i = 0; i < partialMatches.Count && results.Count < limit; i++)
            {
                results.Add(partialMatches[i]);
            }
        }

        return results;
    }
}
