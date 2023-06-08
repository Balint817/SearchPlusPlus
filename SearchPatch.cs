using System.Collections.Generic;
using System;
using System.Linq;
using Assets.Scripts.Database;
using Assets.Scripts.Structs.Modules;
using PeroPeroGames;
using CustomAlbums;
using MelonLoader;
using Newtonsoft.Json;
using System.IO;
using Assets.Scripts.PeroTools.Nice.Interface;
using Il2CppMono;
using Il2CppSystem.Security.Util;
using Newtonsoft.Json.Linq;
using Assets.Scripts.PeroTools.Commons;
using Assets.Scripts.PeroTools.Managers;

namespace SearchPlusPlus
{

    [HarmonyLib.HarmonyPatch(typeof(SearchResults), "PeroLevelDesigner")]
    internal static class SearchPatch
    {
        internal const string startString = "search:";
        // 0: must NOT have a value field

        // 1: range type
        // 2: string type
        // 3: double-range type

        // -1: adaptive range type
        // -2: adaptive string type
        // -3: adaptive double-range type

        internal static List<List<SearchTerm>> tagGroups;

        internal static bool? isAdvancedSearch = false;

        internal static SearchResponse searchError = null;
        internal static bool Prefix(ref bool __result, PeroString peroString, MusicInfo musicInfo, string containsText)
        {
            if (searchError != null)
            {
                return __result = false;
            }
            switch (isAdvancedSearch)
            {
                case null:
                    __result = true;
                    return false;
                case false:
                    return false;
                case true:
                    if (tagGroups == null)
                    {
                        return __result = false;
                    }
                    break;
            }

            __result = false;

            var searchResult = SearchParser.EvaluateSearch(tagGroups, musicInfo, peroString, isTop: true);
            if (searchResult.Code < 0)
            {
                searchError = searchResult;
                return false;
            }
            if (searchResult.Code == 0)
            {
                __result = true;
            }
            return false;
        }
    }
}