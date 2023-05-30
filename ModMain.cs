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

        internal static Dictionary<string, List<List<KeyValuePair<string, string>>>> customTags = new Dictionary<string, List<List<KeyValuePair<string, string>>>>();

        internal static MelonPreferences_Entry<bool> forceErrorCheckToggle;

        internal static MelonPreferences_Entry<bool> recursionToggle;

        public override void OnApplicationQuit()
        {
            recursionToggle.Category.SaveToFile(false);
        }

        public override void OnLateInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("SearchPlusPlus");
            category.SetFilePath("UserData/SearchPlusPlus.cfg");

            forceErrorCheckToggle = category.CreateEntry<bool>("ForceErrorChecks", true, "ForceErrorChecks", "\nIf enabled, searches with an error are forced to be empty. (So that it's obvious you messed up)\nAlways check the console for errors if you disable this tag.\nDisabling it should slightly improve search times.");
            recursionToggle = category.CreateEntry<bool>("RecursionToggle", false, "AllowCustomReference", "\nIf disabled, will prevent you from using 'def' inside 'eval' or custom tag definitions.\nDisabled by default so you don't accidentally create a self-reference and freeze the game.\nUse only if you know what you're doing!");
            var customTagsEntry = category.CreateEntry<Dictionary<string, string>>("CustomSearchTags", new Dictionary<string, string>(), "CustomSearchTags", "\nDefine custom tags here. (Custom tags may not reference other custom tags)");

            ServicePointManager.DefaultConnectionLimit = 100;
            WebRequest.DefaultWebProxy = null;

            MelonLogger.Msg("Loading search tags...");
            string response = null;
            try
            {
                response = Utils.GetRequestString(SearchPatch.advancedJsonUrl);

                var json = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(response);
                var result = new Dictionary<string, string[]>();
                foreach (var item in json["author"])
                {
                    if (!result.ContainsKey(item.Key))
                    {
                        result[item.Key] = new string[0];
                    }
                    result[item.Key] = Enumerable.Append(result[item.Key], item.Value.ToObject<string>()).ToArray();
                }
                foreach (var item in json["name"])
                {
                    if (!result.ContainsKey(item.Key))
                    {
                        result[item.Key] = new string[0];
                    }
                    result[item.Key] = Enumerable.Append(result[item.Key], item.Value.ToObject<string>()).ToArray();
                }
                foreach (var item in json["design"])
                {
                    if (!result.ContainsKey(item.Key))
                    {
                        result[item.Key] = new string[0];
                    }
                    result[item.Key] = Enumerable.Append(result[item.Key], item.Value.ToObject<string>().Substring(1).Trim(' ')).ToArray();
                }
                SearchPatch.searchTags = result;

            }
            catch (Exception ex)
            {
                MelonLogger.Msg(ConsoleColor.Red, ex.ToString());
                MelonLogger.Msg(ConsoleColor.Red, response);
                MelonLogger.Msg(ConsoleColor.Yellow, "Failed to load search tags, using default tags");
            }

            try
            {
                MelonLogger.Msg("Checking charts for cinemas, this shouldn't take long...");
                SearchPatch.hasCinema = AlbumManager.LoadedAlbumsByUid.Where(x => TryParseCinemaJson(x.Value)).Select(x => x.Key).ToHashSet();
                MelonLogger.Msg("Cinema tag initialized");
            }
            catch (Exception ex)
            {

                MelonLogger.Msg(ConsoleColor.Red, ex.ToString());
                MelonLogger.Msg(ConsoleColor.Yellow, "If you're seeing this, then I have absolutely 0 clue how. Either way, the cinema tag won't work. (e.g. please report lmao)");
            }


            LoadHQ();

            MelonLogger.Msg("Loading custom search tags...");


            string filterKey = "| :\\\"";
            foreach (var item in customTagsEntry.Value)
            {
                string errors;
                int groupIdx = -1;
                MelonLogger.Msg(Utils.Separator);
                
                foreach (var c in filterKey)
                {
                    if (item.Key.Contains(c))
                    {
                        errors = $"syntax error: key cannot contain ß{c}ß";
                        goto breakLoop;
                    }
                }

                string text = item.Value.ToLower().Trim(' ');

                var result = new List<List<KeyValuePair<string, string>>>();


                var parseResult = SearchPatch.TryParseInputWithLogs(text, out result);
                if (!parseResult.Key)
                {
                    MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load custom tag: ß{item.Key}ß");
                    MelonLogger.Msg(ConsoleColor.Red, parseResult.Value[0]);
                    if (parseResult.Value.Length > 1)
                    {
                        MelonLogger.Msg(ConsoleColor.Magenta, parseResult.Value[1]);
                    }
                    continue;
                }
                groupIdx = 0;
                foreach (var group in result)
                {
                    foreach (var term in group)
                    {
                        if (term.Key == "def" && !RecursionEnabled)
                        {
                            errors = "input error: the \"def\" tag is not allowed in this context";
                            goto breakLoop;
                        }
                        if (!SearchPatch.CheckFilter(term, out errors))
                        {
                            goto breakLoop;
                        }
                        groupIdx++;
                    }
                }


                try
                {
                    result = RefreshPatch.SortSearchTags(RefreshPatch.OptimizeSearchTags(result));
                }
                catch (Exception)
                {
                    //MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to optimize custom tag \"{item.Key}\" (you shouldn't be able to see this)");
                }
                customTags[item.Key.ToLower()] = result;
                MelonLogger.Msg($"Parsed custom tag \"{item.Key}\": ß" + string.Join(" ", result.Select(x1 => string.Join("|", x1.Select(x2 => RefreshPatch.PairToString(x2))))) + 'ß');
                continue;
            breakLoop:
                MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load custom tag: ß{item.Key}ß");
                MelonLogger.Msg(ConsoleColor.Red, errors + (groupIdx == -1 ? "" : $" (tag no. {groupIdx + 1})"));
            }
            MelonLogger.Msg(Utils.Separator);
            MelonLogger.Msg($"Loaded {customTags.Count} custom tags");


            MelonLogger.Msg("Recovering advanced search...");

            try
            {
                RefreshPatch.Prefix();
            }
            catch (Exception ex)
            {
                MelonLogger.Msg(ConsoleColor.Red, ex.ToString());
                MelonLogger.Msg(ConsoleColor.DarkRed, "Failed to recover advanced search results. This is a critical error, the mod will NOT work.");
            }

            MelonLogger.Msg("Hello World!");
        }

        internal static string[] RankedHashes = new string[0];
        internal static void LoadHQ()
        {
            const string SB_API = "https://mdmc.moe/api/v5/sb";
            try
            {   
                RankedHashes = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string,string>>>(Utils.GetRequestString(SB_API)).Values.SelectMany(x => x.Values).ToArray();
                MelonLogger.Msg("Headquarters 'ranked' tag initialized");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg(ConsoleColor.Red, ex.ToString());
                MelonLogger.Msg(ConsoleColor.Yellow, "If you're seeing this, then I have absolutely 0 clue how. Either way, the headquarters 'ranked' tag won't work.");
            }
        }

        internal static bool TryParseCinemaJson(Album album)
        {
            
            string path = album.BasePath;
            JObject items;
            try
            {
                if (album.IsPackaged)
                {
                    using (ZipFile zipFile = ZipFile.Read(path))
                    {
                        items = zipFile["cinema.json"].OpenReader().JsonDeserialize<JObject>();
                        if (!zipFile.ContainsEntry((string)items["file_name"]))
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    items = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(Path.Combine(path, "cinema.json")));
                    if (!File.Exists(Path.Combine(path, (string)items["file_name"])))
                    {
                        return false;
                    }
                }


                return true;
            }
            catch (Exception)
            {

            }
            return false;
        }

    }
}