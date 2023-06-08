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
                MelonLogger.Msg(ConsoleColor.Red, "syntax error: advanced search was empty");
                return;
            }
            text = text.Substring(SearchPatch.startString.Length).Trim(' ');

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

            highScores = DataHelper.highest.ToSystem().Select(x => x.ScoresToObjects()).ToList();
            fullCombos = DataHelper.fullComboMusic.ToSystem();
            SearchPatch.isAdvancedSearch = true;
            MelonLogger.Msg("Parsed tags: ß" + string.Join(" ", SearchPatch.tagGroups.Select(x1 => string.Join("|", x1.Select(x2 => TermToString(x2))))) + 'ß');
        }
        internal static string TermToString(SearchTerm term)
        {
            return term.Value == null ? $"{term.Key}" : $"{term.Key}:\"{term.Value}\"";
        }
        internal static void NullifyAdvancedSearch()
        {
            SearchPatch.tagGroups?.Clear();
            SearchPatch.tagGroups = null;
        }
    }
}