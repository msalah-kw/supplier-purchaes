namespace SupplierPurchases.Data;

/// <summary>
/// تطبيع النصوص العربية لأغراض البحث ومطابقة قواعد الإسناد فقط.
/// لا يُستخدم في مفاتيح قاعدة البيانات حتى لا تتأثر البيانات المحفوظة مسبقًا.
/// </summary>
public static class ArabicText
{
    /// <summary>يوحّد الهمزات والياء والتاء المربوطة، ويزيل التشكيل والتطويل، ويحوّل الأرقام العربية ويضغط المسافات.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var buffer = new char[text.Length];
        var length = 0;
        var pendingSpace = false;

        foreach (var character in text)
        {
            if (IsIgnorable(character))
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = length > 0;
                continue;
            }

            if (pendingSpace)
            {
                buffer[length++] = ' ';
                pendingSpace = false;
            }

            buffer[length++] = Fold(character);
        }

        return length == 0 ? string.Empty : new string(buffer, 0, length);
    }

    /// <summary>يتحقق مما إذا كان النص يحتوي على الكلمة المفتاحية بعد تطبيع الطرفين.</summary>
    public static bool ContainsNormalized(string? text, string normalizedKeyword)
    {
        if (string.IsNullOrEmpty(normalizedKeyword))
        {
            return false;
        }

        return Normalize(text).Contains(normalizedKeyword, StringComparison.Ordinal);
    }

    private static char Fold(char value) => value switch
    {
        'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
        'ى' or 'ئ' => 'ي',
        'ؤ' => 'و',
        'ة' => 'ه',
        >= '\u0660' and <= '\u0669' => (char)('0' + (value - '\u0660')),
        >= '\u06F0' and <= '\u06F9' => (char)('0' + (value - '\u06F0')),
        _ => char.ToUpperInvariant(value)
    };

    private static bool IsIgnorable(char value) =>
        value is '\u0640'                       // التطويل
        or (>= '\u064B' and <= '\u065F')        // التشكيل
        or '\u0670'
        or (>= '\u06D6' and <= '\u06ED')        // علامات إضافية
        or '\u200B' or '\u200C' or '\u200D'     // فواصل غير مرئية
        or '\u200E' or '\u200F';
}
