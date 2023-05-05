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
        internal static bool HQToggle
        {
            get
            {
                return hqToggle.Value;
            }
        }

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

        internal static MelonPreferences_Entry<bool> hqToggle;

        internal static MelonPreferences_Entry<bool> forceErrorCheckToggle;

        internal static MelonPreferences_Entry<bool> recursionToggle;

        public override void OnApplicationQuit()
        {
            hqToggle.Category.SaveToFile(false);
        }

        public override void OnLateInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("SearchPlusPlus");
            category.SetFilePath("UserData/SearchPlusPlus.cfg");
            hqToggle = category.CreateEntry<bool>("HQSearchToggle", false, "EnableRankedTag", "\nWhether the \"ranked\" tag is enabled.");
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


            if (HQToggle)
            {
                LoadHQ();
            }
            else
            {
                MelonLogger.Msg(ConsoleColor.DarkMagenta, "\"ranked\" tag is disabled.");
                SearchPatch.validFilters.Remove("ranked");
            }

            MelonLogger.Msg("Loading custom search tags...");


            string filterKey = "| :\\\"";
            foreach (var item in customTagsEntry.Value)
            {
                MelonLogger.Msg(Utils.Separator);
                
                foreach (var c in filterKey)
                {
                    if (item.Key.Contains(c))
                    {
                        MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load custom tag: ß{item.Key}ß");
                        MelonLogger.Msg(ConsoleColor.Red, $"syntax error: key cannot contain ß{c}ß");
                        continue;
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
                int groupIdx = 0;
                string errors;
                foreach (var group in result)
                {
                    foreach (var term in group)
                    {
                        if (term.Key == "def" && !RecursionEnabled)
                        {
                            MelonLogger.Msg(ConsoleColor.Yellow, $"Failed to load custom tag: ß{item.Key}ß");
                            MelonLogger.Msg(ConsoleColor.Red, "input error: the \"def\" tag is not allowed in this context");
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
                MelonLogger.Msg(ConsoleColor.Red, errors + $" (tag no. {groupIdx + 1})");
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


        internal static void LoadHQ()
        {
            try
            {

                MelonLogger.Msg("Sending requests to headquarters...");
                MelonLogger.Msg(ConsoleColor.DarkMagenta, "Disable this tag if your internet is slow, because this WILL take a while.");

                var storeResults = new JObject();

                if (File.Exists("UserData/.SppStorage.json"))
                {
                    storeResults = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("UserData/.SppStorage.json"));
                }
                try
                {
                    storeResults["lastChecked"].ToObject<DateTime>();
                }
                catch (Exception)
                {
                    storeResults["lastChecked"] = DateTime.MinValue;
                }

                try
                {
                    storeResults["charts"].ToObject<Dictionary<string, JObject>>();
                }
                catch (Exception)
                {
                    storeResults["charts"] = new JObject();
                }

                var lastChecked = storeResults["lastChecked"].ToObject<DateTime>();
                MelonLogger.Msg($"Last checked: {lastChecked.ToString()} UTC");
                
                JArray jArr = JsonConvert.DeserializeObject<JArray>(Utils.GetRequestString(apiLink));

                int counter = 0;
                double accumulate = 0;
                int arrLength = jArr.Count;
                double increment = 1d / arrLength;
                foreach (JObject item in jArr)
                {
                    var id = (string)item["id"];
                    for (int i = 1; i < 6; i++)
                    {
                        if (!item.ContainsKey($"difficulty{i}"))
                        {
                            continue;
                        }
                        var diffString = (string)item[$"difficulty{i}"];
                        if (string.IsNullOrEmpty(diffString) || diffString == "0")
                        {
                            continue;
                        }
                        var request = Utils.GetRequestInstance(string.Format(mapFormat, id, i), lastChecked);

                        HttpWebResponse HttpResponse = null;
                        try
                        {
                            HttpResponse = (HttpWebResponse)request.GetResponse();
                        }
                        catch (WebException ex)
                        {
                            HttpResponse = (HttpWebResponse)ex.Response;
                            if (HttpResponse.StatusCode == HttpStatusCode.NotModified)
                            {
                                try
                                {
                                    var findAlbumCached = AlbumManager.LoadedAlbumsByUid.FirstOrDefault(x => x.Value.availableMaps.ContainsValue((string)((JObject)((JObject)storeResults["charts"])[id])[i.ToString()]));
                                    if (findAlbumCached.Key == null)
                                    {
                                        continue;
                                    }
                                    SearchPatch.isHeadquarters.Add(findAlbumCached.Key);
                                    break;
                                }
                                catch (Exception)
                                {
                                    MelonLogger.Msg(ConsoleColor.DarkRed, $"failed to load {id}-{i} from cache");
                                    request = Utils.GetRequestInstance(string.Format(mapFormat, id, i));
                                    try
                                    {
                                        HttpResponse = (HttpWebResponse)request.GetResponse();
                                        MelonLogger.Msg(ConsoleColor.DarkRed, $"resending request...");
                                        goto forceContinue;
                                    }
                                    catch (Exception)
                                    {

                                    }
                                }
                            }
                            HttpResponse.Dispose();
                            continue;
                        }
                        forceContinue: { }
                        if (HttpResponse.StatusCode != HttpStatusCode.OK)
                        {
                            continue;
                        }

                        byte[] readBytes;
                        using (Stream stream = HttpResponse.GetResponseStream())
                        {
                            readBytes = Utils.ReadFully(stream);
                        }


                        string response = Encoding.UTF8.GetString(readBytes, 0, readBytes.Length);

                        if (string.IsNullOrEmpty(response) || response.StartsWith("<") || response.StartsWith("{") || response.StartsWith("["))
                        {
                            continue;
                        }
                        string hash = readBytes.GetMD5().ToString("x2");

                        if (!((JObject)storeResults["charts"]).ContainsKey(id))
                        {
                            ((JObject)storeResults["charts"])[id] = new JObject();
                        }
                        ((JObject)((JObject)storeResults["charts"])[id])[i.ToString()] = hash;

                        var findAlbum = AlbumManager.LoadedAlbumsByUid.FirstOrDefault(x => x.Value.availableMaps.ContainsValue(hash));
                        if (findAlbum.Equals(default))
                        {
                            continue;
                        }
                        SearchPatch.isHeadquarters.Add(findAlbum.Key);
                        HttpResponse.Dispose();

                    }

                    counter++;
                    accumulate += increment;
                    if (accumulate > 0.05)
                    {
                        accumulate = 0;
                        MelonLogger.Msg(ConsoleColor.Blue, $"~{Math.Floor(counter * 20d / arrLength)*5}%");
                    }
                }
                storeResults["lastChecked"] = DateTime.UtcNow;
                MelonLogger.Msg("Headquarters tag initialized");
                File.WriteAllText("UserData/.SppStorage.json", JsonConvert.SerializeObject(storeResults));
            }
            catch (Exception ex)
            {
                MelonLogger.Msg(ConsoleColor.Red, ex.ToString());
                MelonLogger.Msg(ConsoleColor.Yellow, "If you're seeing this, then I have absolutely 0 clue how. Either way, the headquarters tag won't work. (e.g. please report lmao)");
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