using Assets.Scripts.UI.Panels.PnlMusicTag;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchPlusPlus
{
    [HarmonyPatch(typeof(LocalizationName), "GetMusicTagNoMusicTxt")]
    internal class NoMusicPatch
    {
        static bool Prefix(ref string __result)
        {
            if (SearchPatch.isAdvancedSearch == true)
            {
                if (SearchPatch.searchError != null)
                {
                    __result = SearchPatch.searchError.Message;
                }
                else
                {
                    __result = "Looks likes there's nothing here...";
                }
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(LocalizationName), "GetSearchNameTxt")]
    internal class SearchNamePatch
    {
        static bool Prefix(ref string __result)
        {
            if (SearchPatch.isAdvancedSearch == true)
            {
                __result = "Results";
                return false;
            }
            return true;
        }
    }
}
