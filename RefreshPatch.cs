using System.Collections.Generic;
using MelonLoader;
using System;
using System.Linq;
using Assets.Scripts.UI.Panels.PnlMusicTag;
using Assets.Scripts.Database;
using Assets.Scripts.PeroTools.Nice.Interface;
using Assets.Scripts.PeroTools.Nice.Datas;
using CustomAlbums;
using Assets.Scripts.PeroTools.Commons;
using Assets.Scripts.PeroTools.Managers;
using Assets.Scripts.GameCore.Managers;
using PeroPeroGames.GlobalDefines;
using Assets.Scripts.Helpers;
using System.IO;
using Assets.Scripts.Structs.Modules;
using Assets.Scripts.PeroTools.GeneralLocalization;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace SearchPlusPlus
{
    public class ComparisonInfo
    {
        internal readonly int Priority = 0;
        internal Comparison<MusicInfo> Comparer;

        public ComparisonInfo(Comparison<MusicInfo> comparer, int priority = 0)
        {
            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }
            Comparer = comparer;
            Priority = priority;
        }
        public static void AddSorter(string key, ComparisonInfo comparisonInfo)
        {
            SearchMergePatch.AddSorter(key, comparisonInfo);
        }
        public static bool IsSorterRegistered(string key)
        {
            return SearchMergePatch.IsRegistered(key);
        }
        public static bool IsSorterActive(string key)
        {
            return SearchMergePatch.IsSorterActive(key);
        }
        public static bool GetActiveSorterValue(string key)
        {
            return SearchMergePatch.ActiveSorterValue(key);
        }
        public static void ActivateSorter(string key, bool inverse = false)
        {
            SearchMergePatch.ActivateSorter(key, inverse);
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(SearchResults), "Merge")]
    internal static class SearchMergePatch
    {
        private static int _langIndex;

        internal static Comparison<MusicInfo> NegateIfNeeded(Comparison<MusicInfo> comparison, string key)
        {
            if (ActiveSorterValue(key))
            {
                return (MusicInfo m1, MusicInfo m2) => { return -comparison(m1, m2); };
            }
            return comparison;
        }
        internal static void Postfix(SearchResults __instance)
        {
            

            if (SearchPatch.isAdvancedSearch != true || SearchPatch.searchError != null)
            {
                _activeSorters.Clear();
                return;
            }
            var processors = _activeSorters.Where(x => x.Key != null)
                .OrderBy(x => x.Key)
                .GroupBy(x => _sorters[x.Key].Priority)
                .OrderByDescending(x => x.Key)
                .SelectMany(x =>
                {
                    return x.Select(y => NegateIfNeeded(_sorters[y.Key].Comparer, y.Key));
                })
                .ToArray();


            _activeSorters.Clear();
            if (processors.Length == 0)
            {
                return;
            }

            _langIndex = Language.LanguageToIndex(SingletonScriptableObject<LocalizationSettings>.instance.GetActiveOption("Language"));

            Comparison<MusicInfo> comparer = (MusicInfo musicInfo1, MusicInfo musicInfo2) =>
            {
                int result;
                foreach (var processor in processors)
                {
                    try
                    {
                        result = processor(musicInfo1, musicInfo2);
                        if (result != 0)
                        {
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error(ex);
                    }
                }
                return 0;
            };

            List<MusicInfo> m_lock = __instance.m_LevelDesignerResult.m_Lock.ToSystem();
            List<MusicInfo> m_unlock = __instance.m_LevelDesignerResult.m_Unlock.ToSystem();
            m_lock.Sort(comparer);
            m_unlock.Sort(comparer);
            __instance.m_LevelDesignerResult.m_Lock = m_lock.ToIL2CPP();
            __instance.m_LevelDesignerResult.m_Unlock = m_unlock.ToIL2CPP();
        }
        private static int SortByUID(MusicInfo musicInfo1, MusicInfo musicInfo2)
        {
            return musicInfo1.uid.CompareTo(musicInfo2.uid);
        }
        private static int SortByName(MusicInfo musicInfo1, MusicInfo musicInfo2)
        {
            return musicInfo1.GetLocal(_langIndex).name.CompareTo(musicInfo2.GetLocal(_langIndex).name);
        }
        private static int SortByAcc(MusicInfo musicInfo1, MusicInfo musicInfo2)
        {
            var hasMaps1 = Utils.GetAvailableMaps(musicInfo1, out var availableMaps1);
            var hasMaps2 = Utils.GetAvailableMaps(musicInfo2, out var availableMaps2);

            if (!hasMaps1)
            {
                if (!hasMaps2)
                {
                    return 0;
                }
                return -1;
            }
            else if (!hasMaps2)
            {
                return 1;
            }

            foreach (var i in availableMaps1.Intersect(availableMaps2))
            {
                string s1 = musicInfo1.uid + "_" + i;
                string s2 = musicInfo2.uid + "_" + i;
                var score1 = RefreshPatch.highScores.FirstOrDefault(x => (string)x["uid"] == s1);
                if (score1 == null)
                {
                    continue;
                }
                var score2 = RefreshPatch.highScores.FirstOrDefault(x => (string)x["uid"] == s2);
                if (score2 == null)
                {
                    continue;
                }
                var acc1 = (float)score1["accuracy"];
                var acc2 = (float)score2["accuracy"];
                int result = acc1.CompareTo(acc2);
                if (result != 0)
                {
                    return result;
                }
            }
            return 0;
        }
        private static int SortByDiff(MusicInfo musicInfo1, MusicInfo musicInfo2)
        {
            var hasMaps1 = Utils.GetAvailableMaps(musicInfo1, out var availableMaps1);
            var hasMaps2 = Utils.GetAvailableMaps(musicInfo2, out var availableMaps2);

            if (!hasMaps1)
            {
                if (!hasMaps2)
                {
                    return 0;
                }
                return -1;
            }
            else if (!hasMaps2)
            {
                return 1;
            }

            var difficulties1 = availableMaps1.Select(x => musicInfo1.GetMusicLevelStringByDiff(x, false)).Where(x => int.TryParse(x, out var t)).Select(x => int.Parse(x)).ToArray();
            var difficulties2 = availableMaps2.Select(x => musicInfo2.GetMusicLevelStringByDiff(x, false)).Where(x => int.TryParse(x, out var t)).Select(x => int.Parse(x)).ToArray();

            if (!difficulties1.Any())
            {
                if (!difficulties2.Any())
                {
                    return 0;
                }
                return -1;
            }
            else if (!difficulties2.Any())
            {
                return 1;
            }
            var result = difficulties1.OrderBy(x => x).Zip(difficulties2.OrderBy(x => x), (x, y) => x.CompareTo(y)).FirstOrDefault(x => x != 0);

            return result == 0
                ? difficulties1.Length.CompareTo(difficulties2.Length)
                : result;
        }

        internal static Dictionary<string, ComparisonInfo> _sorters = new Dictionary<string, ComparisonInfo>()
        {
            ["uid"] = new ComparisonInfo(SortByUID, -1),
            ["name"] = new ComparisonInfo(SortByName),
            ["acc"] = new ComparisonInfo(SortByAcc),
            ["diff"] = new ComparisonInfo(SortByDiff),
        };

        internal static void ActivateSorter(string key, bool inverse = false)
        {
            if (!IsRegistered(key))
            {
                throw new ArgumentException("unknown key", nameof(key));
            }
            _activeSorters[key] = inverse;
        }

        internal static void AddSorter(string key, ComparisonInfo comparisonInfo)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (_sorters.ContainsKey(key))
            {
                throw new ArgumentException("item with the same key is already present", nameof(key));
            }
            _sorters[key] = comparisonInfo;
        }

        internal static bool IsRegistered(string key)
        {
            return _sorters.ContainsKey(key);
        }
        internal static bool ActiveSorterValue(string key)
        {
            return _activeSorters[key];
        }
        internal static bool IsSorterActive(string key)
        {
            return _activeSorters.ContainsKey(key);
        }

        private static ConcurrentDictionary<string, bool> _activeSorters = new ConcurrentDictionary<string, bool>();
    }



    [HarmonyLib.HarmonyPatch(typeof(PnlMusicSearchItem), "RefreshData")]
    internal static class RefreshPatch
    {
        internal static List<Dictionary<string, object>> highScores;

        internal static List<string> fullCombos;

        internal static List<string> history;

        
        //Singleton<TerminalManager
        //DBMusicTagDefine.newMusicUids;
        internal static void Postfix()
        {
            if (SearchPatch.isAdvancedSearch != true)
            {
                return;
            }
            BuiltIns.sortedByLastModified = null;
            if (SearchPatch.searchError != null)
            {
                SearchPatch.isAdvancedSearch = false;
                var t = $"{SearchPatch.searchError.Message} (Code: {SearchPatch.searchError.Code}";
                t += SearchPatch.searchError.Position == null
                    ? ")"
                    : $", Position: {SearchPatch.searchError.Position})";
                MelonLogger.Msg(ConsoleColor.Red, t);

                if (SearchPatch.searchError.Suggestion != null)
                {
                    MelonLogger.Msg(ConsoleColor.Magenta, SearchPatch.searchError.Suggestion);
                }
            }
            if (BuiltIns.isModified)
            {
                BuiltIns.lastChecked = DateTime.UtcNow;
            }
        }

        internal static bool FirstCall = true;
        internal static void Prefix()
        {
            if (FirstCall)
            {
                FirstCall = false;
            }
            else if (!ModMain.InitFinished)
            {
                return;
            }
            if (ModMain.startString == null)
            {
                return;
            }

            SearchPatch.searchError = null;
            string text = Utils.FindKeyword;
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

            if (!text.StartsWith(ModMain.startString))
            {
                SearchPatch.isAdvancedSearch = false;
                NullifyAdvancedSearch();
                return;
            }
            //MelonLogger.Msg(Utils.Separator);

            //if (text.Length < SearchPatch.startString.Length + 1)
            //{
            //    SearchPatch.isAdvancedSearch = null;
            //    NullifyAdvancedSearch();
            //    MelonLogger.Msg(ConsoleColor.Red, "syntax error: advanced search was empty");
            //    return;
            //}
            text = text.Substring(ModMain.startString.Length).Trim(' ');

            var parseResult = SearchParser.ParseSearchText(text);

            var getError = SearchParser.GetSearchError(parseResult);
            if (getError != null)
            {
                NullifyAdvancedSearch();
                MelonLogger.Msg(ConsoleColor.Red, getError.Message);
                if (getError.Suggestion != null)
                {
                    MelonLogger.Msg(ConsoleColor.Magenta, getError.Suggestion);
                }
                return;
            }

            SearchPatch.tagGroups = parseResult;
            history = DataHelper.history.ToSystem();
            highScores = DataHelper.highest.ToSystem().Select(x => x.ScoresToObjects()).ToList();
            fullCombos = DataHelper.fullComboMusic.ToSystem();
            
            SearchPatch.isAdvancedSearch = true;
            //MelonLogger.Msg("Parsed tags: $" + string.Join(" ", SearchPatch.tagGroups.Select(x1 => string.Join("|", x1.Select(x2 => TermToString(x2))))) + '$');
        }
        internal static string TermToString(SearchTerm term)
        {
            return term.Value == null ? $"{term.Key}" : $"{term.Key}:\"{EscapeValue(term.Value)}\"";
        }

        internal static string EscapeValue(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
        internal static void NullifyAdvancedSearch()
        {
            SearchPatch.tagGroups?.Clear();
            SearchPatch.tagGroups = null;
        }
    }
}