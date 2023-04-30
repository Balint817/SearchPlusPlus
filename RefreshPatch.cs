using System.Collections.Generic;
using MelonLoader;
using System;
using System.Linq;
using Assets.Scripts.UI.Panels.PnlMusicTag;
using Assets.Scripts.Database;
using Assets.Scripts.PeroTools.Nice.Interface;
using Assets.Scripts.PeroTools.Nice.Datas;
using CustomAlbums;

namespace SearchPlusPlus
{
    [HarmonyLib.HarmonyPatch(typeof(PnlMusicSearchItem), "RefreshData")]
    internal static class RefreshPatch
    {
        internal static List<Dictionary<string, object>> highScores;

        internal static List<string> fullCombos;

        internal static void Postfix()
        {
            if (SearchPatch.searchError != null)
            {
                SearchPatch.isAdvancedSearch = false;
                MelonLogger.Msg(ConsoleColor.Red, SearchPatch.searchError);
            }
        }
        internal static void Prefix()
        {
            SearchPatch.searchError = null;
            string text = GlobalDataBase.s_DbMusicTag.m_FindKeyword;
            //highScores = DataHelper.highest;

            //IData:
            //"uid" string
            //"evaluate" int
            //"score" int
            //"combo" int
            //"clear" int
            //"accuracyStr" string
            //"accuracy" float;

            text = text.ToLower();

            if (!text.StartsWith(SearchPatch.startString))
            {
                SearchPatch.isAdvancedSearch = false;
                NullifyAdvancedSearch();
                return;
            }
            MelonLogger.Msg(Utils.Separator);

            if (text.Length < SearchPatch.startString.Length + 1)
            {
                SearchPatch.isAdvancedSearch = null;
                NullifyAdvancedSearch();
                MelonLogger.Error("syntax error: advanced search was empty");
                return;
            }
            text = text.Substring(SearchPatch.startString.Length).Trim(' ');

            var parseResult = SearchPatch.TryParseInputWithLogs(text, out SearchPatch.tagGroups);
            if (!parseResult.Key)
            {
                NullifyAdvancedSearch();
                MelonLogger.Error(parseResult.Value[0]);
                if (parseResult.Value.Length > 1)
                {
                    MelonLogger.Msg(ConsoleColor.Magenta, parseResult.Value[1]);
                }
                return;
            }
            int groupIdx = 0;
            string errors;
            foreach (var group in SearchPatch.tagGroups)
            {
                foreach (var term in group)
                {
                    if (!SearchPatch.CheckFilter(term, out errors))
                    {
                        goto breakLoop;
                    }
                    groupIdx++;
                }
            }
            highScores = DataHelper.highest.ToSystem().Select(x => x.ScoresToObjects()).ToList();
            fullCombos = DataHelper.fullComboMusic.ToSystem();
            SearchPatch.isAdvancedSearch = true;
            MelonLogger.Msg("Parsed tags: ß" + string.Join(" ", SearchPatch.tagGroups.Select(x1 => string.Join("|", x1.Select(x2 => PairToString(x2))))) + 'ß');


            try
            {
                OptimizeSearchTags();
                MelonLogger.Msg("Optimized tags: ß" + string.Join(" ", SearchPatch.tagGroups.Select(x1 => string.Join("|", x1.Select(x2 => PairToString(x2))))) + 'ß');
            }
            catch (Exception)
            {
                MelonLogger.Warning("Failed to optimize tags (you shouldn't be able to see this)");
            }
            return;
        breakLoop:
            SearchPatch.isAdvancedSearch = true;
            NullifyAdvancedSearch();
            MelonLogger.Error(errors + $" (tag no. {groupIdx + 1})");
        }

        private static void OptimizeSearchTags()
        {
            SearchPatch.tagGroups = SortSearchTags(OptimizeSearchTags(SearchPatch.tagGroups));
        }

        internal static List<List<KeyValuePair<string, string>>> OptimizeSearchTags(List<List<KeyValuePair<string, string>>> input)
        {
            var result = new List<List<KeyValuePair<string, string>>>();

            foreach (var group in input)
            {
                var combineOrConditions = new Dictionary<string, HashSet<string>>();

                foreach (var item in group)
                {
                    if (!combineOrConditions.ContainsKey(item.Key))
                    {
                        combineOrConditions[item.Key] = new HashSet<string>();
                    }
                    combineOrConditions[item.Key].Add(item.Value);
                }
                result.Add(OptimizeGroup(combineOrConditions));
            }


            var combineAndConditions = new Dictionary<string, HashSet<string>>();
            KeyValuePair<string, string> current;
            for (int i = 0; i < result.Count; i++)
            {
                var group = result[i];
                if (group.Count == 1)
                {
                    current = group[0];
                    if (!combineAndConditions.ContainsKey(current.Key))
                    {
                        combineAndConditions[current.Key] = new HashSet<string>();
                    }
                    combineAndConditions[current.Key].Add(current.Value);
                }
            }
            result = result.Where(x => x.Count > 1).ToList();
            foreach (var item in combineAndConditions)
            {
                foreach (var value in item.Value)
                {
                    result.Add(new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(item.Key, value) });
                }
            }

            return result;

        }

        private static List<KeyValuePair<string,string>> OptimizeGroup(Dictionary<string, HashSet<string>> group)
        {
            var result = new List<KeyValuePair<string, string>>();
            var convertGroup = group.Select(x => new KeyValuePair<string, List<string>>(x.Key, x.Value.ToList())).ToDictionary(x => x.Key, x => x.Value);
            foreach (var item in convertGroup)
            {
                switch (SearchPatch.validFilters[item.Key.StartsWith("-") ? item.Key.Substring(1) : item.Key])
                {
                    case -3:
                    case 3:
                    case -2:
                    case 2:
                    case 0:
                        foreach (var value in item.Value)
                        {
                            result.Add(new KeyValuePair<string,string>(item.Key, value));
                        }
                        break;
                    default:
                        for (int i = 0; i < item.Value.Count; i++)
                        {
                            if (!SearchPatch.EvalRange(item.Value[i], out var start, out var end))
                            {
                                continue;
                            }
                            for (int j = 0; j < item.Value.Count; j++)
                            {
                                if (i == j)
                                {
                                    continue;
                                }
                                if (!MergeIfPossible(start, end, item.Value[j], out var t1, out var t2))
                                {
                                    continue;
                                }
                                start = t1;
                                end = t2;
                                item.Value.RemoveAt(j);
                                if (i >= j)
                                {
                                    i--;
                                }
                                j--;
                            }
                            item.Value[i] = RangeToString(start, end);
                        }
                        foreach (var value in item.Value)
                        {
                            result.Add(new KeyValuePair<string, string>(item.Key, value));
                        }
                        break;
                }
            }

            return result;
        }

        private static string RangeToString(double start, double end)
        {
            if (start == double.NegativeInfinity)
            {
                return $"{end}-";
            }
            if (end == double.PositiveInfinity)
            {
                return $"{start}+";
            }
            if (end == start)
            {
                return $"{start}";
            }
            return $"{start}-{end}";
        }

        private static bool MergeIfPossible(double start1, double end1, string value2, out double start, out double end)
        {
            start = double.NaN;
            end = double.NaN;

            if (!SearchPatch.EvalRange(value2, out var start2, out var end2))
            {
                return false;
            }
            var flag11 = (start2) <= start1 && start1 <= (end2);
            var flag12 = (start2) <= end1 && end1 <= (end2);

            var flag13 = (start2 - 1) >= start1 && (start2 - 1) == end1;
            var flag14 = (end2 + 1) <= end1 && (end2 + 1) == start1;

            var flag21 = (start1) <= start2 && start2 <= (end1);
            var flag22 = (start1) <= end2 && end2 <= (end1);

            var flag23 = (start1 - 1) >= start2 && (start1 - 1) == end2;
            var flag24 = (end1 + 1) <= end2 && (end1 + 1) == start2;

            if (flag11 && flag12)
            {
                start = start2;
                end = end2;
                return true;
            }
            else if (flag21 && flag22)
            {
                start = start1;
                end = end1;
                return true;
            }
            else if (flag11 || flag22)
            {
                start = start2;
                end = end1;
                return true;
            }
            else if (flag12 || flag21)
            {
                start = start1;
                end = end2;
                return true;
            }
            else if (flag13 || flag24)
            {
                start = start1;
                end = end2;
                return true;
            }
            else if (flag14 || flag23)
            {
                start = start2;
                end = end1;
                return true;
            }

            return false;
        }
        internal static List<List<KeyValuePair<string, string>>> SortSearchTags(List<List<KeyValuePair<string,string>>> input)
        {
            return input.OrderBy(x => GetGroupComplexity(x.Select(t => t))).Select(x => x.OrderBy(t => GetComplexity(t)).ToList()).ToList();
        }

        internal static double GetComplexity(KeyValuePair<string,string> tag)
        {
            if (SearchPatch.TagComplexity.ContainsKey(tag.Key))
            {
                return SearchPatch.TagComplexity[tag.Key];
            }
            if (tag.Key == "def")
            {
                if (ModMain.customTags.ContainsKey(tag.Value))
                {
                    return GetTotalComplexity(ModMain.customTags[tag.Value]);
                }
                return 1;
            }
            if (tag.Key == "eval")
            {
                return tag.Value.Length == 0 ? 1 : Math.Pow(tag.Value.Length, 0.80);
            }
            return 2;
        }
        
        internal static double GetGroupComplexity(IEnumerable<KeyValuePair<string, string>> tags)
        {
            double i = 1;
            double count = 0;
            foreach (var tag in tags)
            {
                var t = GetComplexity(tag);
                i += t*t;
                count++;
            }
            count = count == 0 ? 1 : count;
            return i/count;
        }

        internal static double GetTotalComplexity(IEnumerable<IEnumerable<KeyValuePair<string, string>>> input)
        {
            var t = input.Select(x => Math.Pow(GetGroupComplexity(x), 1.25)).ToArray();
            return t.Sum() / t.Length;
        }
        

        internal static string PairToString(KeyValuePair<string, string> pair)
        {
            return pair.Value == null ? $"{pair.Key}" : $"{pair.Key}:{FormatValue(pair.Key, pair.Value)}";
        }

        internal static string FormatValue(string key, string value)
        {
            try
            {
                switch (Math.Abs(SearchPatch.validFilters[key.StartsWith("-") ? key.Substring(1) : key]))
                {
                    case 1:
                    case 3:
                        return value;
                    default:
                        if (value.Contains(' ') || value.Contains('"') || value.Contains('|') || value.Contains(':'))
                        {
                            return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
                        }
                        return value;
                }
            }
            catch (Exception)
            {
                if (value.Contains(' ') || value.Contains('"') || value.Contains('|') || value.Contains(':'))
                {
                    return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
                }
                return value;
            }
        }
        internal static void NullifyAdvancedSearch()
        {
            SearchPatch.tagGroups?.Clear();
            SearchPatch.tagGroups = null;
        }
    }
}