using System.Text;
using System.Text.Json;

namespace Kieran.Quizmaster.Infrastructure.Ai.Quizzes;

/// <summary>
/// Incremental JSON parser tuned for our specific shape:
///   { "questions": [ {...}, {...}, ... ] }
///
/// Tracks `{}` depth while ignoring braces inside strings, so we can yield
/// each fully-formed question object the moment its closing `}` arrives in
/// the model's token stream. Robust to:
///   - leading prose / markdown fences (we wait for the first `[`)
///   - inner `[]` arrays (e.g. options) — they don't end the outer array
///     because we only flip out of "in array" mode when a `]` appears at
///     depth 0
///   - escaped quotes inside string values
/// </summary>
internal sealed class StreamingQuestionParser
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly StringBuilder _buffer = new();
    private int  _scanPos;
    private int  _depth;
    private int  _questionStart = -1;
    private bool _inArray;
    private bool _inString;
    private bool _escape;

    public int EmittedCount { get; private set; }

    /// <summary>
    /// Append a streamed chunk and yield any questions that became fully
    /// parseable as a result. Malformed objects (rare) are skipped silently.
    /// </summary>
    public IEnumerable<RawQuestion> Append(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) yield break;

        _buffer.Append(chunk);

        for (; _scanPos < _buffer.Length; _scanPos++)
        {
            char c = _buffer[_scanPos];

            if (_escape)
            {
                _escape = false;
                continue;
            }
            if (_inString)
            {
                if (c == '\\') _escape = true;
                else if (c == '"') _inString = false;
                continue;
            }
            if (c == '"')
            {
                _inString = true;
                continue;
            }

            if (!_inArray)
            {
                if (c == '[') _inArray = true;
                continue;
            }

            switch (c)
            {
                case '{':
                    if (_depth == 0) _questionStart = _scanPos;
                    _depth++;
                    break;

                case '}':
                    _depth--;
                    if (_depth == 0 && _questionStart >= 0)
                    {
                        var json = _buffer.ToString(_questionStart, _scanPos - _questionStart + 1);
                        _questionStart = -1;
                        RawQuestion? raw = null;
                        try { raw = JsonSerializer.Deserialize<RawQuestion>(json, JsonOpts); }
                        catch { /* skip malformed object */ }
                        if (raw is not null)
                        {
                            EmittedCount++;
                            yield return raw;
                        }
                    }
                    break;

                case ']' when _depth == 0:
                    _inArray = false;
                    break;
            }
        }
    }
}
