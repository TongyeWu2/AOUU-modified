using System;
using System.Linq;

namespace AOUU.Services;

public static class OcrTextTriggerMatcher
{
    public static bool IsMatch(string recognizedText, string targetText)
    {
        var normalizedRecognizedText = Normalize(recognizedText);
        var normalizedTargetText = Normalize(targetText);

        if (string.IsNullOrWhiteSpace(normalizedRecognizedText) ||
            string.IsNullOrWhiteSpace(normalizedTargetText))
        {
            return false;
        }

        if (normalizedRecognizedText.Contains(normalizedTargetText, StringComparison.Ordinal))
        {
            return true;
        }

        var allowedDistance = normalizedTargetText.Length <= 4 ? 1 : 2;
        for (var length = Math.Max(1, normalizedTargetText.Length - allowedDistance);
             length <= normalizedTargetText.Length + allowedDistance;
             length++)
        {
            if (length > normalizedRecognizedText.Length)
            {
                continue;
            }

            for (var index = 0; index <= normalizedRecognizedText.Length - length; index++)
            {
                var candidate = normalizedRecognizedText.Substring(index, length);
                if (GetEditDistance(candidate, normalizedTargetText) <= allowedDistance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static string Normalize(string text)
    {
        return new string(text
            .ToUpperInvariant()
            .Select(NormalizeCharacter)
            .Where(character => character is >= 'A' and <= 'Z')
            .ToArray());
    }

    private static char NormalizeCharacter(char character)
    {
        return character switch
        {
            '0' => 'O',
            '1' => 'I',
            'L' => 'I',
            _ => character
        };
    }

    private static int GetEditDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;

            for (var column = 1; column <= right.Length; column++)
            {
                var cost = left[row - 1] == right[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
