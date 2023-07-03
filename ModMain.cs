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
using PopupLib;
using UnityEngine;
using Assets.Scripts.UI.Panels.PnlMusicTag;
using Il2CppSystem.Runtime.Remoting.Messaging;

namespace SearchPlusPlus
{
    public class Keybind
    {

        private KeyCode? _key;

        public KeyCode Key
        {
            get
            {
                return _key ?? KeyCode.None;
            }
        }

        private MelonPreferences_Entry<string> _entry;

        public MelonPreferences_Entry<string> Entry
        {
            get
            {
                return _entry;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                _entry = value;
                Reload();
            }
        }
        public Keybind(MelonPreferences_Entry<string> entry)
        {
            Entry = entry;
            entry.OnValueChanged += ReloadEvent;
            Reload();
        }

        private void ReloadEvent(string oldValue, string newValue)
        {
            Reload();
        }

        private void Reload()
        {
            if (!Enum.TryParse(Entry.Value, true, out KeyCode result))
            {
                if (Entry.Value != Entry.DefaultValue)
                {
                    MelonLogger.Error($"Failed to parse key for entry \"{Entry.DisplayName}\", falling back to default key '{Entry.DefaultValue}'");
                    Entry.Value = Entry.DefaultValue;
                }
                else
                {
                    MelonLogger.Error($"Failed to parse default for entry \"{Entry.DisplayName}\", go cry to the mod's creator");
                }
                return;
            };
            _key = result;
        }
    }
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

        internal static MelonPreferences_Entry<bool> seenForumEntry;

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

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Welcome")
            {
                if (!InitFinished)
                {
                    ErrorWindow.Show();
                }
            }
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
            SearchParser.RegisterKey("old", BuiltIns.Term_Old);
            SearchParser.RegisterKey("random", BuiltIns.Term_Random);
            SearchParser.RegisterKey("ranked", BuiltIns.Term_Ranked);
            SearchParser.RegisterKey("headquarters", BuiltIns.Term_Ranked, true);
            SearchParser.RegisterKey("recent", BuiltIns.Term_Recent);
            SearchParser.RegisterKey("reverse_sort", BuiltIns.Term_ReverseSort);
            SearchParser.RegisterKey("reversesort", BuiltIns.Term_ReverseSort, true);
            SearchParser.RegisterKey("reverse", BuiltIns.Term_ReverseSort, true);
            SearchParser.RegisterKey("scene", BuiltIns.Term_Scene);
            SearchParser.RegisterKey("sort", BuiltIns.Term_Sort);
            SearchParser.RegisterKey("tag", BuiltIns.Term_Tag);
            SearchParser.RegisterKey("title", BuiltIns.Term_Title);
            SearchParser.RegisterKey("touhou", BuiltIns.Term_Touhou);
            SearchParser.RegisterKey("unplayed", BuiltIns.Term_Unplayed);

            BuiltIns.albumNames = Singleton<ConfigManager>.instance.GetConfigObject<DBConfigAlbums>(0).list.ToSystem().ToDictionary(x => x.albumUidIndex, x => x.title);
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
            aliasEntry = category.CreateEntry<Dictionary<string, string>>("TagAliases", new Dictionary<string, string>(), "TagAliases", "\nDefines aliases here. (improved custom tags)");


            LoadAliases();
            MelonLogger.Msg(Utils.Separator);

            MelonLogger.Msg("Recovering advanced search...");

            forceErrorCheckToggle = category.CreateEntry<bool>("ForceErrorChecks", true, "ForceErrorChecks", "\nIf enabled, searches with an error are forced to be empty. (So that it's obvious you messed up)\nAlways check the console for errors if you disable this tag.\nDisabling it should slightly improve search times.");

            recentDayCountEntry = category.CreateEntry<int>("RecentDayLimit", 7, "RecentDayLimit", "\nThe amount of time, in days, that an album should be considered 'recent'.");

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
                MelonLogger.Msg(ConsoleColor.DarkRed, "Failed to recover advanced search results. This is a critical error, the mod will most likely NOT work.");
            }

            


            RecentDateLimit = DateTime.UtcNow - new TimeSpan(RecentDayLimit, 0, 0, 0);

            MelonLogger.Msg("Hello World!");

            ForumKeybind = new Keybind(category.CreateEntry<string>("ForumKey", "KeypadDivide", description: "\nThe key used to open the in-game forum."));

            seenForumEntry = category.CreateEntry<bool>("SeenForum", false, description: "\nWhether you've seen the in-game forum before");

            if (!seenForumEntry.Value)
            {
                ForumWindow.Show();
                InfoWindow.Show();
            }
        }

        internal static Keybind ForumKeybind;

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
                    MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load custom tag: ${item.Key}$");
                    MelonLogger.Msg(ConsoleColor.Red, $"syntax error: key cannot contain ${firstMatch}$");
                    continue;
                }

                var key = item.Key.ToLower();

                if (customTags.ContainsKey(key))
                {
                    MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load custom tag: ${item.Key}$");
                    MelonLogger.Msg(ConsoleColor.Red, $"duplicate key \"{key}\"");
                    continue;
                }

                string text = item.Value.ToLower().Trim(' ');

                var result = SearchParser.ParseSearchText(text);

                var getError = SearchParser.GetSearchError(result);
                if (getError != null)
                {
                    MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load custom tag: ${item.Key}$");
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
                MelonLogger.Msg($"Parsed custom tag \"{item.Key}\": $" + string.Join(" ", result.Select(x1 => string.Join("|", x1.Select(x2 => RefreshPatch.TermToString(x2))))) + '$');
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
                        MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load custom tag: ${tagName}$");
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

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(ForumKeybind?.Key ?? KeyCode.None))
            {
                ForumWindow.Show();
            }
        }

        private static string Code(string text)
        {
            return $"<color={Format.Color.Gray}FF>{text}</color>";
        }
        private static string CodeBold(string text)
        {
            return Bold($"<color={Format.Color.Gray}FF>{text}</color>");
        }

        private static string Bold(string text)
        {
            return $"<b>{text}</b>";
        }

        private const string Tab = "    ";



        private static MessageWindow _infoWindow;
        private static MessageWindow InfoWindow
        {
            get
            {
                if (_infoWindow == null)
                {
                    _infoWindow = new MessageWindow($"You can open this menu again at any time using the \"{ForumKeybind.Key}\" key (can be changed in config)", "Search++")
                    {
                        _onClose = x =>
                        {
                            seenForumEntry.Value = true;
                            x.Reset();
                        }
                    };
                }
                return _infoWindow;
            }
        }

        private static MessageWindow _errorWindow;
        private static MessageWindow ErrorWindow
        {
            get
            {
                if (_errorWindow == null)
                {
                    _errorWindow = new MessageWindow($"The mod failed basic initialization and has disabled itself.", "Search++")
                    {
                        _onClose = x =>
                        {
                            x.Reset();
                        }
                    };
                }
                return _errorWindow;
            }
        }

        private static ForumWindow _forumWindow;
        private static ForumWindow ForumWindow
        {
            get
            {
                if (_forumWindow == null)
                {
                    _forumWindow = new ForumWindow(
                        new ForumObject("Search++",
                            "Welcome to the Search++ guide," +
                            "\nan overly-complicated search engine mod." +
                            "\nFeel free to browse at your leisure." +
                            "\n" +
                            "\nIf you've ever written a single line of code," +
                            "\nyou can probably skip most of this guide."),
                        new ForumObject("General Info",
                            "First things first, I recommend saving a bunch of filters you wrote beforehand, and copy-pasting them into the game as" +
                            "\nneeded, cause typing these each time is annoying." +
                            $"\nAlternatively, check out the {Code("Custom tags")} section." +
                            $"\n" +
                            $"\nThe config file for this mod is \"{Code("SearchPlusPlus.cfg")}\"" +
                            $"\n" +
                            $"\nAll advanced searches made using this mod must be" +
                            $"\npreceded by a start text," +
                            $"\nwhich is changeable in the config." +
                            $"\nThe default value is \"{Code("search:")}\"." +
                            $"\nFor obvious reasons, this should not be an empty string." +
                            $"\n" +
                            $""),
                        new ForumObject("Syntax",
                            $"\nSearch terms follow the syntax of {Code("key:value")} or" +
                            $"\n{Code("key:\"value with spaces\"")}" +
                            $"\nFor example:" +
                            $"\n- {Code("key")}, such as:" +
                            $"\n{Tab}- {Code("cinema")}" +
                            $"\n- {Code("key:value")}, such as:" +
                            $"\n{Tab}- {Code("title:\"Shinsou Masui\"")}" +
                            $"\n{Tab}- {Code("diff:11-12")}" +
                            $"\nTag names are case insensitive." +
                            $"\n" +
                            $"\nRange syntax:" +
                            $"\n- {Code("key:A")}" +
                            $"\n- {Code("key:A-B")}" +
                            $"\n- {Code("key:A+")} or {Code("key:A-")}" +
                            $"\n" +
                            $"\nSome tags may receive multiple ranges as input." +
                            $"\n({Code("key:\"A-B C-D\"")})" +
                            $"\n" +
                            $"\nYou can negate any condition by prefixing it with an {CodeBold("-")}, like:" +
                            $"\n{Code("-key:value")}" +
                            $"\n" +
                            $"\nTags may not contain any of the following characters:" +
                            $"\n{string.Join(", ", SearchParser.IllegalChars.Select(x => $"\"{CodeBold(x.ToString())}\""))}" +
                            $"\n" +
                            $"\nTags must be seperated by some number of spaces," +
                            $"\nor a single {CodeBold("|")} character." +
                            $"\nWhen tags are seperated by {CodeBold("|")}, only one of the tags within" +
                            $"\nthat group need to pass for a song to be included." +
                            $"\nWhen tags are seperated by spaces, all seperated tags must pass for a song to be included." +
                            $"\nFor example:" +
                            $"\n- {Code("hidden:11 bpm:200+")}" +
                            $"\n{Tab}- This checks if the song has an 11* hidden <b>and</b> the bpm is" +
                            $"\n{Tab}  higher than 200" +
                            $"\n- {Code("diff:11+|hidden:11+ bpm:200+")}" +
                            $"\n{Tab}- Checks if the song has any 11* difficulty <b>or</b> 11* hidden," +
                            $"\n{Tab}  <b>and</b> whether the bpm is higher than 200" +
                            $"\n" +
                            $"\nAs a consequence, bare values may not contain spaces," +
                            $"\n{CodeBold(":")}, {CodeBold("|")}, and quoation marks ({CodeBold("\"")})." +
                            $"\n" +
                            $"\nTo work around these limitations, you may surround" +
                            $"\n the value with double quotes ({Code("\"value\"")})," +
                            $"\nto create a {Code("string")}." +
                            $"\nA string may contain all of the previously mentioned illegal characters." +
                            $"\nSpecial rules:" +
                            $"\n- To type a double quote inside a string, you need to prefix it" + //perfect line length
                            $"\n  with a backslash, like: {CodeBold("\\\"")}" +
                            $"\n- To type a backslash inside a string, you need to type a" +
                            $"\n  double backslash, like: {CodeBold("\\\\")}" +
                            $"\n- If a backslash (inside a string) prefixes" +
                            $"\n  any other character, the backslash is ignored." +
                            $"\n- If a bare quotation mark appears anywhere other than the" +
                            $"\n  start or end of a string, a syntax error is thrown."),
                        new ForumObject("----Tags----",
                            "This is only here to show that" +
                            "\nthis is where the list of tags start!"),

                        new ForumObject("Difficulty:Range",
                            $"Key: {Code("diff")}" +
                            $"\nCheck for a visible difficulty in the given range." +
                            $"\nSupports {CodeBold("?")} => the difficulty isn't a number"),

                        new ForumObject("Callback:Range",
                            $"Key: {Code("callback")}" +
                            $"\nCheck for callback difficulties in the given range." +
                            $"\nCallback difficulty does not appear visually in-game," +
                            $"\nit is always a number."),

                        new ForumObject("Hidden",
                            $"Key: {Code("hidden")}" +
                            $"\nCheck if the song has a hidden."),
                        new ForumObject("Hidden:Range",
                            $"Key: {Code("hidden")}" +
                            $"\nCheck if hidden is in the given range." +
                            $"\nSupports {CodeBold("?")} => the difficulty isn't a number"),
                        new ForumObject("Touhou",
                            $"Key: {Code("touhou")}" +
                            $"\nCheck if there's a touhou hidden"),
                        new ForumObject("Touhou:Range",
                            $"Key: {Code("touhou")}" +
                            $"\nCheck if touhou hidden is in the given range." +
                            $"\nSupports {CodeBold("?")} => the difficulty isn't a number"),
                        new ForumObject("BPM:Range",
                            $"Key: {Code("bpm")}" +
                            $"\nCheck if bpm in json is in the given range." +
                            $"\nSupports {CodeBold("?")} => the bpm isn't given as a number"),
                        new ForumObject("Cinema",
                            $"Key: {Code("cinema")}" +
                            $"\nCheck if there's a cinema"),
                        new ForumObject("Custom",
                            $"Key: {Code("custom")}" +
                            $"\nCheck if the song is a custom"),
                        new ForumObject("Ranked",
                            $"Key: {Code("ranked")}" +
                            $"\nAliases: {Code("headquarters")}" +
                            $"\nCheck if the song is ranked"),
                        new ForumObject("History",
                            $"Key: {Code("history")}" +
                            $"\nThe song was recently played (history tab)"),
                        new ForumObject("New",
                            $"Key: {Code("new")}" +
                            $"\nA recently added default song (new tab)"),
                        new ForumObject("Recent",
                            $"Key: {Code("recent")}" +
                            $"\nA recently added custom song" +
                            $"\nThe time range can be changed in the config"),
                        new ForumObject("Recent:Value",
                            $"Key: {Code("recent")}" +
                            $"\nReturns the `X` most recent songs (given by value)"),
                        new ForumObject("Recent:Range",
                            $"Key: {Code("recent")}" +
                            $"\nReturns the most recent songs that have a position" +
                            $"\nthat is within the given range."),
                        new ForumObject("Old:Value",
                            $"Key: {Code("old")}" +
                            $"\nReturns the `X` oldest songs (given by value)"),
                        new ForumObject("Old:Range",
                            $"Key: {Code("old")}" +
                            $"\nReturns the oldest songs that have a position" +
                            $"\nthat is within the given range."),
                        new ForumObject("Scene:String",
                            $"Key: {Code("scene")}" +
                            $"\nChecks if the scene matches the filter, can be:" +
                            $"\n- a 1 digit number" +
                            $"\n- 2 character string, i.e. \"01\", \"02\", etc." +
                            $"\n- The name of the scene, i.e. \"candyland\", \"retrocity\""),
                        new ForumObject("Design:String",
                            $"Key: {Code("design")}" +
                            $"\nAliases: {Code("designer")}" +
                            $"\nSearches for a specific level designer."),
                        new ForumObject("Author:String",
                            $"Key: {Code("author")}" +
                            $"\nSearches for a song author"),
                        new ForumObject("Title:String",
                            $"Key: {Code("title")}" +
                            $"\nSearches by the title of a song."),
                        new ForumObject("Tag:String",
                            $"Key: {Code("tag")}" +
                            $"\nSearches for a specific search tag." +
                            $"\n(Includes both CAM's tags and the built-in ones)"),
                        new ForumObject("Any:String",
                            $"Key: {Code("any")}" +
                            $"\nShort for \"{Code("design:x|author:x|title:x|tag:x")}\"."),
                        new ForumObject("Anyx:String",
                            $"Key: {Code("anyx")}" +
                            $"\nUses custom search tags made by MNight4 for MuseBot." +
                            $"\nAdditionally runs \"{Code("any:x")}\""),
                        new ForumObject("Album:String",
                            $"Key: {Code("album")}" +
                            $"\nReturns songs within the given pack/album."),
                        new ForumObject("Unplayed",
                            $"Key: {Code("unplayed")}" +
                            $"\nNo difficulty of the song has any clears."),
                        new ForumObject("Unplayed:Range",
                            $"Key: {Code("range")}" +
                            $"\nThe given difficulties of the song have no clears (1-5)" +
                            $"\nSupports {CodeBold("?")} => selects the highest difficulty"),
                        new ForumObject("FullCombo",
                            $"Key: {Code("fc")}" +
                            $"\nAll difficulties of a song are FC-d (full combo)"),
                        new ForumObject("FullCombo",
                            $"Key: {Code("fc")}" +
                            $"\nGiven difficulties are FC-d (full combo)" +
                            $"\nSupports {CodeBold("?")} => selects the highest difficulty"),
                        new ForumObject("Accuracy:Range",
                            $"Key: {Code("acc")}" +
                            $"\nAll difficulties have an accuracy within the given range"),
                        new ForumObject("Accuracy:DoubleRange",
                            $"Key: {Code("acc")}" +
                            $"\nAll difficulties in '{Code("range2")}'" +
                            $"\nhave an accuracy within '{Code("range1")}'" +
                            $"\n{Code("range2")} supports {CodeBold("?")} => selects the highest difficulty"),
                        new ForumObject("Random",
                            $"Key: {Code("random")}" +
                            $"\n1/2 chance to pass."),
                        new ForumObject("Random:Value",
                            $"Key: {Code("random")}" +
                            $"\n1/value chance to pass, where value must at least 1."),
                        new ForumObject("Sort:String",
                            $"Key: {Code("sort")}" +
                            $"\nApplies sorting to the search results." +
                            $"\nBuilt-in values are:" +
                            $"{string.Join("", SearchMergePatch._sorters.Select(x => $"\n- \"{x.Key}\""))}"),
                        new ForumObject("ReverseSort:String",
                            $"Key: {Code("reverse_sort")}" +
                            $"\nAliases: {Code("reverse")}, {Code("reversesort")}" +
                            $"\nApplies reversed sorting in comparison to {Code("sort:string")}"),
                        new ForumObject("Eval:String",
                            $"Key: {Code("eval")}" +
                            $"\nCheck the {Code("Grouping")} section."),
                        new ForumObject("Def:String",
                            $"Key: {Code("def")}" +
                            $"\nCheck the {Code("Grouping")} section."),
                        new ForumObject("----Advanced----",
                            "This is only here to show that" +
                            "this is where the list of tags end," +
                            "and advanced info starts!"),
                        new ForumObject("Grouping",
                            $"The aforementioned mechanics may prove to be lacking" +
                            $"\nin certain scenarios, such as when trying to" +
                            $"\nmake a logical XOR." +
                            $"\nFortunately, the grouping of tags is within" +
                            $"\nthis mod's functionality." +
                            $"\nRefer to the following sections:" +
                            $"\n- {Code("Evaluate Tag")}" +
                            $"\n- {Code("Custom tags")}"),
                        new ForumObject("Evaluate Tag",
                            $"Syntax: {Code("eval:string")}" +
                            $"\n...where the {Code("string")}'s contents" +
                            $"\nrepresent a seperate search." +
                            $"\nE.g.: \"{Code("eval:\"diff:11\"")}\" equals \"{Code("diff:11")}\"" +
                            $"\nThe tag can be nested." +
                            $"\nYou can think of {Code("eval")} as parantheses."),
                        new ForumObject("Custom tags: Alias",
                            $"Aliases are a recent feature aimed to make" +
                            $"\ncommon searches easier, by allowing you to bind" +
                            $"\nsearches to a custom tag." +
                            $"\nThey are the direct successors of the {Code("def")} tag." +
                            $"\nYou can define aliases in the config." +
                            $"\n" +
                            $"\nIf recursion is disabled (by default)," +
                            $"\naliases may not appear inside" +
                            $"\nlegacy {Code("def")} tags," +
                            $"\nother aliases," +
                            $"\nor {Code("eval")} tags"),
                        new ForumObject("Custom tags: Define",
                            $"The \"{Code("def:string")}\" is a legacy feature," +
                            $"\nit is the direct ancestor to aliases." +
                            $"\nYou can define custom tags in the config." +
                            $"\n" +
                            $"\nIf recursion is disabled (by default)," +
                            $"\n{Code("def")} tags may not appear inside" +
                            $"\nother {Code("def")} tags," +
                            $"\naliases," +
                            $"\nor {Code("eval")} tags"),
                        new ForumObject("Cascaded custom tags",
                            $"Take \"{Code("def:\"mytag:value\"")}\" as an example." +
                            $"\nThe value after '{CodeBold(":")}' will override" +
                            $"\nvalues assigned to tags inside the custom tag." +
                            $"\nFor {Code("eval")} tags and aliases," +
                            $"\nthe values of the tags are modified instead." +
                            $"\nFor nested {Code("def")} tags, if enabled:" +
                            $"\n- If an empty value value is given ({Code("def:\"mytag:\"")})," +
                            $"\n  it will inherit the value." +
                            $"\n- If no value is given or the value isn't empty," +
                            $"\n  the value remains unaffected.")
                        )
                        {
                            _onClose = (x) =>
                            {
                                x.Reset();
                            }
                        };
                }
                return _forumWindow;
            }
        }
    }
}