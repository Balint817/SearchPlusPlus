using Assets.Scripts.Database;
using Assets.Scripts.PeroTools.Nice.Components;
using Harmony;
using JetBrains.Annotations;
using MelonLoader;
using PeroPeroGames;
using SearchPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

public class SingleSetValue<T>
{
    private T _value;
    private bool _hasValue;
    public T Value
    {
        get
        {
            return _value;
        }
        set
        {
            if (_hasValue)
            {
                throw new InvalidOperationException("value may only be set once");
            }
            _hasValue = true;
            _value = value;
        }
    }

    public bool HasValue
    {
        get
        {
            return _hasValue;
        }
    }

    public static implicit operator T(SingleSetValue<T> instance)
    {
        return instance.Value;
    }
}
public class SearchResponse
{
    public static readonly SearchResponse PassedTest = new SearchResponse(0) { Position = null };
    public static readonly SearchResponse FailedTest = new SearchResponse(1) { Position = null };

    public readonly string Message;
    public readonly string Suggestion;
    public readonly int Code;

    public readonly SingleSetValue<int?> _positionValue = new SingleSetValue<int?>();
    public int? Position
    {
        get
        {
            return _positionValue;
        }
        set
        {
            _positionValue.Value = value;
        }
    }

    public SearchResponse(int code)
    {
        Code = code;
    }
    public SearchResponse(string message, int code)
    {
        Message = message;
        Code = code;
    }
    public SearchResponse(string message, int code, int? position)
    {
        Message = message;
        Code = code;
        Position = position;
    }
    public SearchResponse(string message, string suggestion, int code)
    {
        Message = message;
        Code = code;
        Suggestion = suggestion;
    }
    public SearchResponse(string message, string suggestion, int code, int? position)
    {
        Message = message;
        Code = code;
        Suggestion = suggestion;
        Position = position;
    }

}

public delegate SearchResponse SearchEvaluator(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null);
public delegate string SearchTransformer(MusicInfo musicInfo, string key, string value, string valueOverride = null);
public static class SearchParser
{

    public static SearchResponse GetSearchError(List<List<SearchTerm>> searchTerms)
    {
        if (searchTerms.Count == 1 && searchTerms[0].Count == 1 && searchTerms[0][0].ErrorInfo != null)
        {
            return searchTerms[0][0].ErrorInfo;
        }
        return null;
    }
    public static SearchResponse EvaluateSearch(List<List<SearchTerm>> searchTerms, MusicInfo musicInfo, PeroString peroString, string valueOverride = null, bool isTop=false)
    {
        var err = GetSearchError(searchTerms);
        if (err != null)
        {
            return err;
        }
        int groupIdx = 0;
        foreach (var termGroup in searchTerms)
        {
            var groupResult = false;
            foreach (var term in termGroup)
            {
                var negate = term.Key.StartsWith("-");
                var key = negate ? term.Key.Substring(1) : term.Key;
                if (!IsKeyRegistered(key))
                {
                    return new SearchResponse($"search error: received unknown key \"{key}\"", -1, groupIdx);
                }
                if (!isTop && !ModMain.RecursionEnabled && key == "def")
                {
                    return new SearchResponse("search error: the \"def\" tag is not allowed in this context", -1, groupIdx);
                }
                var termResult = GetByKey(key)(musicInfo, peroString, term.Value, valueOverride);

                if (termResult.Code == (negate ? 1 : 0))
                {
                    groupResult = true;
                    if (!ModMain.ForceErrorCheck)
                    {
                        break;
                    }
                }
                if (termResult.Code <= -1)
                {
                    if (!termResult._positionValue.HasValue)
                    {
                        termResult.Position = groupIdx;
                    }
                    return termResult;
                };
                groupIdx++;
            }
            if (!groupResult)
            {
                return SearchResponse.FailedTest;
            }
        }
        return SearchResponse.PassedTest;
    }

    private static Dictionary<string, SearchEvaluator> registeredKeys = new Dictionary<string, SearchEvaluator>();

    public static bool IsKeyRegistered(string key)
    {
        return registeredKeys.ContainsKey(key);
    }

    public static SearchEvaluator GetByKey(string key)
    {
        return registeredKeys[key];
    }

    public static bool GetByKey(string key, out SearchEvaluator function)
    {
        if (!registeredKeys.ContainsKey(key))
        {
            function = null;
            return false;
        }
        function = registeredKeys[key];
        return true;
    }

    public static string IllegalChars = "\\\": |-";

    private static List<string> Aliases = new List<string>();
    public static void RegisterKey(string key, SearchEvaluator function, bool forceDuplicate = false)
    {
        if (!IsKeyValid(key))
        {
            throw new ArgumentException($"The following key contains illegal characters: \"{key}\"");
        }
        key = key.ToLower();
        if (IsKeyRegistered(key))
        {
            if (Aliases.Contains(key))
            {
                var t = registeredKeys[key];
                registeredKeys.Remove(key);
                try
                {
                    RegisterKey(key, function);
                    Aliases.Remove(key);
                    MelonLogger.Msg(ConsoleColor.DarkRed, $"The alias '{key}' has been overriden by a tag from a different mod!");
                }
                catch (Exception)
                {
                    registeredKeys[key] = t;
                }
            }
            throw new Exception($"The key \"{key}\" has already been registered by the assembly {registeredKeys[key].GetMethodInfo().Module.Assembly.FullName} ({registeredKeys[key].GetMethodInfo().Name})");
        }
        if (!forceDuplicate && registeredKeys.Values.Contains(function))
        {
            throw new ArgumentException($"The function \"{registeredKeys[key].GetMethodInfo().Name}\" has already been registered and duplication wasn't enabled");
        }
        registeredKeys[key] = function;
    }

    internal static void ClearAliases()
    {
        while (Aliases.Count > 0)
        {
            registeredKeys.Remove(Aliases[0]);
            Aliases.RemoveAt(0);
        }

    }
    internal static void RegisterAlias(string key, SearchEvaluator function)
    {
        RegisterKey(key, function);
        Aliases.Add(key);
    }

    public static bool IsKeyValid(string key)
    {
        return !key.Any(x => IllegalChars.Contains(x));
    }
    
    public static List<List<SearchTerm>> ParseSearchText(string containsText)
    {
        if (containsText == null)
        {
            return new List<List<SearchTerm>>() {
                new List<SearchTerm>() {
                    new SearchTerm(
                        new SearchResponse($"syntax error, the given search was null", -2)) } };
        }
        var result = new List<List<SearchTerm>>() { new List<SearchTerm>() };
        var idx = 0;
        bool isString = false;
        bool orTagEnd = false;
        bool stringEnd = false;
        bool escape = false;
        bool isValue = false;
        string key = null;
        string value = null;

        for (; idx < containsText.Length; idx++)
        {
            var c = containsText[idx];
            switch (c)
            {
                case '-':
                    if (stringEnd)
                    {
                        return new List<List<SearchTerm>>() {
                            new List<SearchTerm>() {
                                new SearchTerm(
                                    new SearchResponse($"syntax error at position {idx + 1}, expected '|' or ' ' after string terminator, got '{c}'", $"did you mean to escape ('\\\"') the previous quotation mark?", -2, idx)) } };
                    }
                    if (isString)
                    {
                        if (escape)
                        {
                            escape = false;
                        }
                        value += c;
                        break;
                    }
                    if (isValue)
                    {
                        value += c;
                        break;
                    }
                    if (key != null)
                    {
                        return new List<List<SearchTerm>>() {
                            new List<SearchTerm>() {
                                new SearchTerm(
                                    new SearchResponse($"syntax error at position {idx + 1}, '{c}' may only appear directly at the start of a key or inside the value", -2, idx)) } };
                    }
                    key += c;
                    break;
                case '|':
                    if (isString)
                    {
                        if (escape)
                        {
                            escape = false;
                        }
                        value += c;
                        break;
                    }
                    stringEnd = false;
                    orTagEnd = true;
                    key = key.ToLower();
                    //if (!IsKeyRegistered(key))
                    //{
                    //    return new List<List<SearchTerm>>() {
                    //        new List<SearchTerm>() {
                    //            new SearchTerm(
                    //                new SearchResponse($"received unknown key \"{key}\"", -2)) } };
                    //}
                    if (isValue)
                    {
                        if (value == null)
                        {
                            return new List<List<SearchTerm>>() {
                                new List<SearchTerm>() {
                                    new SearchTerm(
                                        new SearchResponse($"syntax error at position {idx + 1}, expected value after ':'", -2, idx)) } };
                        }
                        result.Last().Add(new SearchTerm(key, value));
                        key = null;
                        value = null;
                        isValue = false;
                        break;
                    }
                    if (key == null)
                    {
                        return new List<List<SearchTerm>>() {
                            new List<SearchTerm>() {
                                new SearchTerm(
                                    new SearchResponse($"syntax error at position {idx + 1}, expected a key before '|'", -2, idx)) } };
                    }
                    result.Last().Add(new SearchTerm(key, value));
                    key = null;
                    value = null;
                    break;
                case ' ':
                    if (orTagEnd)
                    {
                        return new List<List<SearchTerm>>() {
                            new List<SearchTerm>() {
                                new SearchTerm(
                                    new SearchResponse($"syntax error at position {idx + 1}, expected a key after '|'", -2, idx)) } };
                    }
                    if (isString)
                    {
                        if (escape)
                        {
                            escape = false;
                        }
                        value += c;
                        break;
                    }
                    orTagEnd = false;
                    stringEnd = false;
                    key = key.ToLower();
                    //if (!IsKeyRegistered(key))
                    //{
                    //    return new List<List<SearchTerm>>() {
                    //        new List<SearchTerm>() {
                    //            new SearchTerm(
                    //                new SearchResponse($"received unknown key \"{key}\"", -2)) } };
                    //}
                    if (isValue)
                    {
                        if (value == null)
                        {
                            return new List<List<SearchTerm>>() {
                                new List<SearchTerm>() {
                                    new SearchTerm(
                                        new SearchResponse($"syntax error at position {idx + 1}, expected a value after ':'", -2, idx)) } };
                        }
                        isValue = false;
                    }
                    result.Last().Add(new SearchTerm(key, value));
                    result.Add(new List<SearchTerm>());
                    key = null;
                    value = null;
                    break;
                case ':':
                    if (orTagEnd)
                    {
                        return new List<List<SearchTerm>>() {
                            new List<SearchTerm>() {
                                new SearchTerm(
                                    new SearchResponse($"syntax error at position {idx + 1}, expected a key after '|'", -2, idx)) } };
                    }
                    if (stringEnd)
                    {
                        return new List<List<SearchTerm>>() {
                            new List<SearchTerm>() {
                                new SearchTerm(
                                    new SearchResponse($"syntax error at position {idx + 1}, expected '|' or ' ' after string terminator, got '{c}'", $"did you mean to escape ('\\\"') the previous quotation mark?", -2, idx)) } };
                    }
                    if (isString)
                    {
                        if (escape)
                        {
                            escape = false;
                        }
                        value += c;
                        break;
                    }
                    if (!isValue)
                    {
                        if (key == null)
                        {
                            return new List<List<SearchTerm>>() {
                                new List<SearchTerm>() {
                                    new SearchTerm(
                                        new SearchResponse($"syntax error at position {idx + 1}, expected a key after '|' or ' ', got '{c}'", -2, idx)) } };
                        }
                        isValue = true;
                        break;
                    }
                    return new List<List<SearchTerm>>() {
                        new List<SearchTerm>() {
                            new SearchTerm(
                                new SearchResponse($"syntax error at position {idx + 1}, expected '|' or ' ' after value, got '{c}'", $"did you mean to use a string?", -2, idx)) } };
                case '\\':
                    if (orTagEnd)
                    {
                        return new List<List<SearchTerm>>() {
                            new List<SearchTerm>() {
                                new SearchTerm(
                                    new SearchResponse($"syntax error at position {idx + 1}, expected a key after '|'", -2, idx)) } };
                    }
                    if (stringEnd)
                    {
                        return new List<List<SearchTerm>>() {
                            new List<SearchTerm>() {
                                new SearchTerm(
                                    new SearchResponse($"syntax error at position {idx + 1}, expected '|' or ' ' after string terminator, got '{c}'", $"did you mean to escape ('\\\"') the previous quotation mark?", -2, idx)) } };
                    }
                    if (isString)
                    {
                        if (escape)
                        {
                            value += c;
                            escape = false;
                            break;
                        }
                        escape = true;
                        break;
                    }
                    if (isValue)
                    {
                        value += c;
                        break;
                    }
                    return new List<List<SearchTerm>>() {
                        new List<SearchTerm>() {
                            new SearchTerm(
                                new SearchResponse($"syntax error at position {idx + 1}, key cannot contain '{c}'", -2, idx)) } };
                case '"':
                    if (orTagEnd)
                    {
                        return new List<List<SearchTerm>>() {
                            new List<SearchTerm>() {
                                new SearchTerm(
                                    new SearchResponse($"syntax error at position {idx + 1}, expected a key after '|'", -2, idx)) } };
                    }
                    if (stringEnd)
                    {
                        return new List<List<SearchTerm>>() {
                            new List<SearchTerm>() {
                                new SearchTerm(
                                    new SearchResponse($"syntax error at position {idx + 1}, expected '|' or ' ' after string terminator, got '{c}'", $"did you mean to escape ('\\\"') the previous quotation mark?", -2, idx)) } };
                    }
                    if (isString)
                    {
                        if (escape)
                        {
                            value += c;
                            escape = false;
                            break;
                        }
                        if (value == null)
                        {
                            value = "";
                        }
                        isString = false;
                        stringEnd = true;
                        break;
                    }
                    if (isValue)
                    {
                        if (value != null)
                        {
                            return new List<List<SearchTerm>>() {
                                new List<SearchTerm>() {
                                    new SearchTerm(
                                        new SearchResponse($"syntax error at position {idx + 1}, literal text cannot contain '{c}'.", $"did you mean to use a string and escape ('\\\"') the quotation mark?", -2, idx)) } };
                        }
                        isString = true;
                        break;
                    }
                    return new List<List<SearchTerm>>() {
                        new List<SearchTerm>() {
                            new SearchTerm(
                                new SearchResponse($"syntax error at position {idx + 1}, key cannot contain '{c}'", -2, idx)) } };
                default:
                    orTagEnd = false;
                    if (stringEnd)
                    {
                        return new List<List<SearchTerm>>() {
                            new List<SearchTerm>() {
                                new SearchTerm(
                                    new SearchResponse($"syntax error at position {idx + 1}, expected '|' or ' ' after string terminator, got '{c}'", "did you forget to escape ('\\\"') a quotation mark?", -2, idx)) } };
                    }
                    if (isValue)
                    {
                        value += c;
                        break;
                    }
                    key += c;
                    break;
            }
        }
        if (isString)
        {
            return new List<List<SearchTerm>>() {
                new List<SearchTerm>() {
                    new SearchTerm(
                        new SearchResponse($"syntax error at position {idx + 1} (end of prompt), unterminated string", "did you forget to escape ('\\\"') a quotation mark?", -2, idx)) } };
        }
        if (key != null)
        {
            result.Last().Add(new SearchTerm(key, value));
        }

        return result;
    }
}
public class ErrorInfo
{

}
public class SearchTerm
{
    public string Key;
    public string Value;
    internal SearchResponse ErrorInfo;

    public SearchTerm(string key)
    {
        Key = key;
    }
    public SearchTerm(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public SearchTerm(SearchResponse error)
    {
        ErrorInfo = error;
    }
}