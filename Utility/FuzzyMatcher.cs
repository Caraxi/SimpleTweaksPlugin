using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SimpleTweaksPlugin.Utility;

public readonly struct FuzzyMatcher {
    private const bool IsBorderMatching = true;
    private static readonly (int, int)[] EmptySegArray = Array.Empty<(int, int)>();

    private readonly string needleString = string.Empty;
    private readonly int needleFinalPosition = -1;
    private readonly (int Start, int End)[] needleSegments = EmptySegArray;
    private readonly MatchMode mode = MatchMode.Simple;

    public FuzzyMatcher(string term, MatchMode matchMode) {
        needleString = term;
        needleFinalPosition = needleString.Length - 1;
        mode = matchMode;

        needleSegments = matchMode switch {
            MatchMode.FuzzyParts => FindNeedleSegments(needleString),
            MatchMode.Fuzzy or MatchMode.Simple => EmptySegArray,
            _ => throw new ArgumentOutOfRangeException(nameof(matchMode), matchMode, "Invalid match mode"),
        };
    }

    private static (int Start, int End)[] FindNeedleSegments(ReadOnlySpan<char> span) {
        var segments = new List<(int, int)>();
        var wordStart = -1;

        for (var i = 0; i < span.Length; i++) {
            if (span[i] is not ' ' and not '\u3000') {
                if (wordStart < 0)
                    wordStart = i;
            }
            else if (wordStart >= 0) {
                segments.Add((wordStart, i - 1));
                wordStart = -1;
            }
        }

        if (wordStart >= 0)
            segments.Add((wordStart, span.Length - 1));

        return segments.ToArray();
    }

    public int Matches(string value) {
        if (needleFinalPosition < 0)
            return 0;

        if (mode == MatchMode.Simple)
            return value.Contains(needleString, StringComparison.InvariantCultureIgnoreCase) ? 1 : 0;

        if (mode == MatchMode.Fuzzy)
            return GetRawScore(value, 0, needleFinalPosition);

        if (mode == MatchMode.FuzzyParts) {
            if (needleSegments.Length < 2)
                return GetRawScore(value, 0, needleFinalPosition);

            var total = 0;
            for (var i = 0; i < needleSegments.Length; i++) {
                var (start, end) = needleSegments[i];
                var cur = GetRawScore(value, start, end);
                if (cur == 0)
                    return 0;

                total += cur;
            }

            return total;
        }

        return 8;
    }

    public int MatchesAny(params string[] values) {
        var max = 0;
        for (var i = 0; i < values.Length; i++) {
            var cur = Matches(values[i]);
            if (cur > max)
                max = cur;
        }

        return max;
    }

    private int GetRawScore(ReadOnlySpan<char> haystack, int needleStart, int needleEnd) {
        var (startPos, gaps, consecutive, borderMatches, endPos) = FindForward(haystack, needleStart, needleEnd);
        if (startPos < 0)
            return 0;

        var needleSize = needleEnd - needleStart + 1;

        var score = CalculateRawScore(needleSize, startPos, gaps, consecutive, borderMatches);

        (startPos, gaps, consecutive, borderMatches) = FindReverse(haystack, endPos, needleStart, needleEnd);
        var revScore = CalculateRawScore(needleSize, startPos, gaps, consecutive, borderMatches);

        return int.Max(score, revScore);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateRawScore(int needleSize, int startPos, int gaps, int consecutive, int borderMatches) {
        var score = 100
                    + needleSize * 3
                    + borderMatches * 3
                    + consecutive * 5
                    - startPos
                    - gaps * 2;
        if (startPos == 0)
            score += 5;
        return score < 1 ? 1 : score;
    }

    private (int StartPos, int Gaps, int Consecutive, int BorderMatches, int HaystackIndex) FindForward(
        ReadOnlySpan<char> haystack, int needleStart, int needleEnd) {
        var needleIndex = needleStart;
        var lastMatchIndex = -10;

        var startPos = 0;
        var gaps = 0;
        var consecutive = 0;
        var borderMatches = 0;

        for (var haystackIndex = 0; haystackIndex < haystack.Length; haystackIndex++) {
            if (haystack[haystackIndex] == needleString[needleIndex]) {
                if (IsBorderMatching) {
                    if (haystackIndex > 0) {
                        if (!char.IsLetterOrDigit(haystack[haystackIndex - 1]))
                            borderMatches++;
                    }
                }

                needleIndex++;

                if (haystackIndex == lastMatchIndex + 1)
                    consecutive++;

                if (needleIndex > needleEnd)
                    return (startPos, gaps, consecutive, borderMatches, haystackIndex);

                lastMatchIndex = haystackIndex;
            }
            else {
                if (needleIndex > needleStart)
                    gaps++;
                else
                    startPos++;
            }
        }

        return (-1, 0, 0, 0, 0);
    }

    private (int StartPos, int Gaps, int Consecutive, int BorderMatches) FindReverse(
        ReadOnlySpan<char> haystack, int haystackLastMatchIndex, int needleStart, int needleEnd) {
        var needleIndex = needleEnd;
        var revLastMatchIndex = haystack.Length + 10;

        var gaps = 0;
        var consecutive = 0;
        var borderMatches = 0;

        for (var haystackIndex = haystackLastMatchIndex; haystackIndex >= 0; haystackIndex--) {
            if (haystack[haystackIndex] == needleString[needleIndex]) {
                if (IsBorderMatching) {
                    if (haystackIndex > 0) {
                        if (!char.IsLetterOrDigit(haystack[haystackIndex - 1]))
                            borderMatches++;
                    }
                }

                needleIndex--;

                if (haystackIndex == revLastMatchIndex - 1)
                    consecutive++;

                if (needleIndex < needleStart)
                    return (haystackIndex, gaps, consecutive, borderMatches);

                revLastMatchIndex = haystackIndex;
            }
            else
                gaps++;
        }

        return (-1, 0, 0, 0);
    }
}

public enum MatchMode {
    Simple,
    Fuzzy,
    FuzzyParts,
}
