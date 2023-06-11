using System.Collections.Generic;
using MelonLoader;
using System;
using System.Linq;
using CustomAlbums;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ionic.Zip;
using System.Net;
using System.Text;
using Assets.Scripts.Database;
using PeroPeroGames;
using Assets.Scripts.PeroTools.Commons;
using Assets.Scripts.PeroTools.Managers;
using PeroPeroGames.GlobalDefines;

namespace SearchPlusPlus
{
    public class ModMain : MelonMod
    {
        internal bool isFirstRun = true;

        internal const string apiLink = "https://mdmc.moe/api/v5/charts";
        internal const string mapFormat = "https://mdmc.moe/charts/{0}/map{1}.bms";

        internal static bool ForceErrorCheck
        {
            get
            {
                return forceErrorCheckToggle.Value;
            }
        }

        internal static bool RecursionEnabled
        {
            get
            {
                return recursionToggle.Value;
            }
        }

        internal static int RecentDayLimit
        {
            get
            {
                return recentDayCountEntry.Value;
            }
        }

        internal static string startString
        {
            get
            {
                return startSearchStringEntry?.Value;
            }
        }

        internal static DateTime RecentDateLimit;

        internal static Dictionary<string, List<List<SearchTerm>>> customTags = new Dictionary<string, List<List<SearchTerm>>>();

        internal static MelonPreferences_Entry<bool> forceErrorCheckToggle;

        internal static MelonPreferences_Entry<bool> recursionToggle;

        internal static MelonPreferences_Entry<int> recentDayCountEntry;

        internal static MelonPreferences_Entry<string> startSearchStringEntry;

        internal static MelonPreferences_Entry<Dictionary<string, string>> customTagsEntry;

        internal static MelonPreferences_Entry<Dictionary<string, string>> aliasEntry;

        internal static bool InitFinished = false;
        public override void OnApplicationQuit()
        {
            recursionToggle.Category.SaveToFile(false);
        }
        public override void OnPreferencesLoaded()
        {
            if (!InitFinished)
            {
                return;
            }
            RecentDateLimit = DateTime.UtcNow - new TimeSpan(RecentDayLimit, 0, 0, 0);
            MelonLogger.Msg(ConsoleColor.Magenta, "Re-loading custom tags...");
            LoadCustomTags();
            MelonLogger.Msg(ConsoleColor.Magenta, "Re-loading aliases...");
            LoadAliases();
        }
        public override void OnLateInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("SearchPlusPlus");
            category.SetFilePath("UserData/SearchPlusPlus.cfg");

            ServicePointManager.DefaultConnectionLimit = 100;
            WebRequest.DefaultWebProxy = null;

            MelonLogger.Msg("Registering builtins...");

            SearchParser.RegisterKey("acc", BuiltIns.Term_Acc);
            SearchParser.RegisterKey("album", BuiltIns.Term_Album);
            SearchParser.RegisterKey("any", BuiltIns.Term_Any);
            SearchParser.RegisterKey("anyx", BuiltIns.Term_AnyX);
            SearchParser.RegisterKey("author", BuiltIns.Term_Author);
            SearchParser.RegisterKey("bpm", BuiltIns.Term_BPM);
            SearchParser.RegisterKey("callback", BuiltIns.Term_Callback);
            SearchParser.RegisterKey("cinema", BuiltIns.Term_Cinema);
            SearchParser.RegisterKey("custom", BuiltIns.Term_Custom);
            SearchParser.RegisterKey("def", BuiltIns.Term_Def);
            SearchParser.RegisterKey("design", BuiltIns.Term_Designer);
            SearchParser.RegisterKey("designer", BuiltIns.Term_Designer, true);
            SearchParser.RegisterKey("diff", BuiltIns.Term_Diff);
            SearchParser.RegisterKey("eval", BuiltIns.Term_Eval);
            SearchParser.RegisterKey("fc", BuiltIns.Term_FC);
            SearchParser.RegisterKey("hidden", BuiltIns.Term_Hidden);
            SearchParser.RegisterKey("history", BuiltIns.Term_History);
            SearchParser.RegisterKey("new", BuiltIns.Term_New);
            SearchParser.RegisterKey("ranked", BuiltIns.Term_Ranked);
            SearchParser.RegisterKey("headquarters", BuiltIns.Term_Ranked, true);
            SearchParser.RegisterKey("recent", BuiltIns.Term_Recent);
            SearchParser.RegisterKey("scene", BuiltIns.Term_Scene);
            SearchParser.RegisterKey("tag", BuiltIns.Term_Tag);
            SearchParser.RegisterKey("title", BuiltIns.Term_Title);
            SearchParser.RegisterKey("touhou", BuiltIns.Term_Touhou);
            SearchParser.RegisterKey("unplayed", BuiltIns.Term_Unplayed);

            BuiltIns.albumNames = Singleton<ConfigManager>.instance.GetConfigObject<DBConfigAlbums>(0).list.ToSystem().ToDictionary(x => x.albumUidIndex, x => x.title);
            BuiltIns.newMusicUids = DBMusicTagDefine.newMusicUids.ToSystem();
            MelonLogger.Msg("Loading search tags...");
            string response = null;
            var responseIdx = -1;
            try
            {
                response = Utils.GetRequestString(BuiltIns.advancedJsonUrl);

                var json = JsonConvert.DeserializeObject<JObject>(response);
                var result = new Dictionary<string, string[]>();
                foreach (var chart in json["album"].ToObject<Dictionary<string, Dictionary<string, JValue>>>())
                {
                    responseIdx++;
                    string uid = chart.Key;
                    Dictionary<string, JValue> chartData = chart.Value;
                    var tags = new List<string>();
                    if (chartData.TryGetValue("name", out var value))
                    {
                        tags.Add(value.ToObject<string>());
                    }
                    if (chartData.TryGetValue("author", out value))
                    {
                        tags.Add(value.ToObject<string>());
                    }
                    if (chartData.TryGetValue("design", out value))
                    {
                        tags.Add(value.ToObject<string>());
                    }
                    if (tags.Any())
                    {
                        tags.Append(string.Join("\0", tags));
                        result[uid] = tags.ToArray();
                    }
                }
                BuiltIns.searchTags = result;

            }
            catch (Exception ex)
            {
                MelonLogger.Msg(ConsoleColor.Red, ex.ToString());
                MelonLogger.Msg(ConsoleColor.Red, $"position: {responseIdx}");
                MelonLogger.Msg(ConsoleColor.Yellow, "Failed to load search tags, using default tags");
            }

            try
            {
                MelonLogger.Msg("Checking charts for cinemas, this shouldn't take long...");
                BuiltIns.hasCinema = AlbumManager.LoadedAlbumsByUid.Where(x => Utils.TryParseCinemaJson(x.Value)).Select(x => x.Key).ToHashSet();
                MelonLogger.Msg("Cinema tag initialized");
            }
            catch (Exception ex)
            {

                MelonLogger.Msg(ConsoleColor.Red, ex.ToString());
                MelonLogger.Msg(ConsoleColor.Yellow, "If you're seeing this, then I have absolutely 0 clue how. Either way, the cinema tag won't work. (e.g. please report lmao)");
            }
            BuiltIns.lastChecked = DateTime.UtcNow;

            LoadHQ();

            InitFinished = true;

            MelonLogger.Msg("Loading custom search tags...");
            customTagsEntry = category.CreateEntry<Dictionary<string, string>>("CustomSearchTags", new Dictionary<string, string>(), "CustomSearchTags", "\nDefine custom tags here. (Custom tags may not reference other custom tags)");
            recursionToggle = category.CreateEntry<bool>("RecursionToggle", false, "AllowCustomReference", "\nIf disabled, will prevent you from using 'def' inside 'eval' or custom tag definitions.\nDisabled by default so you don't accidentally create a self-reference and freeze the game.\nUse only if you know what you're doing!");

            LoadCustomTags();

            MelonLogger.Msg(Utils.Separator);

            MelonLogger.Msg("Loading aliases...");
            aliasEntry = category.CreateEntry<Dictionary<string, string>>("TagAliases", new Dictionary<string, string>(), "TagAliases", "\nDefines aliases here. (improved custom tags)\nAlias references WILL yield unstable results. If necessary, enable recursion and use 'def' instead.");


            LoadAliases();
            MelonLogger.Msg(Utils.Separator);

            MelonLogger.Msg("Recovering advanced search...");
            
            startSearchStringEntry = category.CreateEntry<string>("StartSearchText", "search:", "StartSearchText", "\nThe text that your search needs to start with in order for this mod to be enabled.\nMay be left empty if you want the mod to always use advanced search.\nFor obvious reasons, this is not a good idea.");

            if (startSearchStringEntry.Value == null)
            {
                startSearchStringEntry.Value = startSearchStringEntry.DefaultValue;
            }

            try
            {
                RefreshPatch.Prefix();
            }
            catch (Exception ex)
            {
                MelonLogger.Msg(ConsoleColor.Red, ex.ToString());
                MelonLogger.Msg(ConsoleColor.DarkRed, "Failed to recover advanced search results. This is a critical error, the mod will NOT work.");
            }

            forceErrorCheckToggle = category.CreateEntry<bool>("ForceErrorChecks", true, "ForceErrorChecks", "\nIf enabled, searches with an error are forced to be empty. (So that it's obvious you messed up)\nAlways check the console for errors if you disable this tag.\nDisabling it should slightly improve search times.");

            recentDayCountEntry = category.CreateEntry<int>("RecentDayLimit", 7, "RecentDayLimit", "\nThe amount of time, in days, that an album should be considered 'recent'.");

            


            RecentDateLimit = DateTime.UtcNow - new TimeSpan(RecentDayLimit, 0, 0, 0);

            if (category.HasEntry("HQSearchToggle"))
            {
                category.DeleteEntry("HQSearchToggle");
            }


            //Singleton<DBMusicTag>.instance.GetAlbumTagInfo(1).customInfo;

            //Singleton<DBConfigALBUM>.instance;

            //Singleton<DBConfigAlbums>.instance;
            MelonLogger.Msg("Hello World!");
        }

        private void LoadAliases()
        {
            SearchParser.ClearAliases();
            foreach (var item in aliasEntry.Value)
            {
                MelonLogger.Msg(Utils.Separator);
                try
                {
                    SearchEvaluator function = (musicInfo, peroString, value, valueOverride) =>
                    {
                        if (value != string.Empty)
                        {
                            valueOverride = null;
                        }
                        else
                        {
                            value = valueOverride;
                        }
                        return BuiltIns.Term_Eval(musicInfo, peroString, item.Value, value);
                    };
                    SearchParser.RegisterAlias(item.Key, function);
                    MelonLogger.Msg($"Successfully loaded alias '{item.Key}'");
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load alias '{item.Key}'");
                    MelonLogger.Msg(ConsoleColor.Red, $"Message: {ex.Message}");
                }
            }
        }
        private void LoadCustomTags()
        {
            customTags.Clear();
            foreach (var item in customTagsEntry.Value)
            {
                MelonLogger.Msg(Utils.Separator);
                var firstMatch = item.Key.FirstOrDefault(x => SearchParser.IllegalChars.Contains(x));
                if (firstMatch != '\0')
                {
                    MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load custom tag: ß{item.Key}ß");
                    MelonLogger.Msg(ConsoleColor.Red, $"syntax error: key cannot contain ß{firstMatch}ß");
                    continue;
                }

                var key = item.Key.ToLower();

                if (customTags.ContainsKey(key))
                {
                    MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load custom tag: ß{item.Key}ß");
                    MelonLogger.Msg(ConsoleColor.Red, $"duplicate key \"{key}\"");
                    continue;
                }

                string text = item.Value.ToLower().Trim(' ');

                var result = SearchParser.ParseSearchText(text);

                var getError = SearchParser.GetSearchError(result);
                if (getError != null)
                {
                    MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load custom tag: ß{item.Key}ß");
                    MelonLogger.Msg(ConsoleColor.Red, getError.Message);
                    if (getError.Suggestion != null)
                    {
                        MelonLogger.Msg(ConsoleColor.Magenta, getError.Suggestion);
                    }
                    continue;
                }

                if (!CheckRules(result, item.Key))
                {
                    continue;
                }

                customTags[key] = result;
                MelonLogger.Msg($"Parsed custom tag \"{item.Key}\": ß" + string.Join(" ", result.Select(x1 => string.Join("|", x1.Select(x2 => RefreshPatch.TermToString(x2))))) + 'ß');
            }
            MelonLogger.Msg(Utils.Separator);
            MelonLogger.Msg($"Loaded {customTags.Count} custom tags");
        }

        internal static bool CheckRules(List<List<SearchTerm>> parseResult, string tagName)
        {
            foreach (var group in parseResult)
            {
                foreach (var term in group)
                {
                    if (term.Key == "def" && !RecursionEnabled)
                    {
                        MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load custom tag: ß{tagName}ß");
                        MelonLogger.Msg(ConsoleColor.Red, "search error: the \"def\" tag is not allowed in this context");
                        return false;
                    }
                }
            }
            return true;
        }
        internal static void LoadHQ()
        {
            const string SB_API = "https://mdmc.moe/api/v5/sb";
            try
            {
                BuiltIns.isHeadquarters = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string,string>>>(Utils.GetRequestString(SB_API)).Values.SelectMany(x => x.Values).ToHashSet();
                MelonLogger.Msg($"Headquarters 'ranked' tag initialized ({BuiltIns.isHeadquarters.Count})");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg(ConsoleColor.Red, ex.ToString());
                MelonLogger.Msg(ConsoleColor.Yellow, "If you're seeing this, then I have absolutely 0 clue how. Either way, the headquarters 'ranked' tag won't work.");
            }
        }

        

    }
}