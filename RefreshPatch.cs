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

namespace SearchPlusPlus
{
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
        internal static void Prefix()
        {
            if (ModMain.startString == null)
            {
                return;
            }

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

            if (!text.StartsWith(ModMain.startString))
            {
                SearchPatch.isAdvancedSearch = false;
                NullifyAdvancedSearch();
                return;
            }
            MelonLogger.Msg(Utils.Separator);

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
            MelonLogger.Msg("Parsed tags: $" + string.Join(" ", SearchPatch.tagGroups.Select(x1 => string.Join("|", x1.Select(x2 => TermToString(x2))))) + '$');
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