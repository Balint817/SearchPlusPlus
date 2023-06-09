﻿using Assets.Scripts.Database;
using Assets.Scripts.PeroTools.Commons;
using Assets.Scripts.PeroTools.Managers;
using CustomAlbums;
using MelonLoader;
using MelonLoader.Preferences;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using PeroPeroGames;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Assets.Scripts.PeroTools.Nice.Interface;
using PeroPeroGames.GlobalDefines;

namespace SearchPlusPlus
{
    public class Range
    {
        public static readonly Range InvalidRange = new Range() { IsReadonly = true };

        private double _start;
        private double _end;
        public double Start
        {
            get
            {
                return _start;
            }
            set
            {
                if (value > _end)
                {
                    throw new ArgumentOutOfRangeException($"must be less than or equal to max value", nameof(value));
                }
                _start = value;
            }
        }
        public double End
        {
            get
            {
                return _end;
            }
            set
            {
                if (value < _start)
                {
                    throw new ArgumentOutOfRangeException($"must be greater than or equal to min value", nameof(value));
                }
                _end = value;
            }
        }

        public bool IsReadonly
        {
            get;
            private set;
        }
        public Range(double start, double end)
        {
            if (double.IsNaN(start) ^ double.IsNaN(end))
            {
                throw new ArgumentException($"Either both or neither ends should be NaN.");
            }
            if (start > end)
            {
                throw new ArgumentOutOfRangeException($"Min value ({start}) must be less than or equal to max value ({end})!");
            };
            _start = start;
            _end = end;
        }
        public Range(double value)
        {
            _start = _end = value;
        }

        public Range()
        {
            _start = _end = double.NaN;
        }

        public void Update(double start, double end)
        {
            if (IsReadonly)
            {
                return;
            }
            if (start > end)
            {
                throw new ArgumentOutOfRangeException($"Min value ({start}) must be less than or equal to max value ({end})!");
            };
            _start = start;
            _end = end;
        }
        public void Update(double value)
        {
            if (IsReadonly)
            {
                return;
            }
            _start = _end = value;
        }
        public virtual bool Contains(double value)
        {
            return _start <= value && value <= _end;
        }

        public bool IsOverlap(Range range)
        {
            if (_end < range._start)
            {
                return false;
            }
            if (Start > range._end)
            {
                return false;
            }

            return true;
        }
        public bool TryGetOverlap(Range range, out Range overlap)
        {
            overlap = null;
            if (_end < range._start)
            {
                return false;
            }
            if (_start > range._end)
            {
                return false;
            }
            overlap = new Range(Math.Max(range._start, _start), Math.Min(range._end, _end));
            return true;
        }

        public bool TryMerge(Range range, out Range merge)
        {
            merge = null;
            if (_end < range._start - 1)
            {
                return false;
            }
            if (_start > range._end + 1)
            {
                return false;
            }
            merge = new Range(Math.Min(range._start, _start), Math.Max(range._end, _end));
            return true;
        }
    }
    internal static class BuiltIns
    {
        internal const string runtimeParserText = "[Runtime parser] ";

        internal static HashSet<string> hasCinema = new HashSet<string>();

        internal static HashSet<string> isHeadquarters = new HashSet<string>();

        internal static readonly int[][] TermDiffTypes = new int[][]
            {
                new int[]{1,2,3},
                new int[]{4},
                new int[]{5}
            };
        internal const string advancedJsonUrl = "https://raw.fastgit.org/MinecraftNight4/bot_muse/main/musedash/vanilla/advanced.json";

        internal static Dictionary<string, string> validScenes = new Dictionary<string, string>
        {
            {"spacestation", "01"},
            {"space_station", "01"},
            {"retrocity", "02"},
            {"castle", "03"},
            {"rainynight", "04"},
            {"rainy_night", "04"},
            {"candyland", "05"},
            {"oriental", "06"},
            {"letsgroove", "07"},
            {"let'sgroove", "07"},
            {"lets_groove", "07"},
            {"let's_groove", "07"},
            {"touhou", "08"},
            {"djmax", "09"},
        };
        internal static char[] defSplitChars = new char[] { ':' };
        internal static SearchResponse EvalDefine(PeroString ps, MusicInfo musicInfo, string value, string context)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new SearchResponse($"input error: received empty or null value for 'def'", -1);
            }
            if (value.Contains(':'))
            {
                var splitValue = value.Split(defSplitChars, 2);
                if (splitValue[1] != "")
                {
                    context = splitValue[1];
                }
                value = splitValue[0];
            }
            else
            {
                context = null;
            }
            if (!ModMain.customTags.ContainsKey(value))
            {
                return new SearchResponse($"search error: unknown custom tag \"{value}\"", -1);
            }
            return SearchParser.EvaluateSearch(ModMain.customTags[value], musicInfo, ps, context);
        }
        internal static bool EvalCustom(MusicInfo musicInfo)
        {
            return AlbumManager.LoadedAlbumsByUid.ContainsKey(musicInfo.uid);
        }
        internal static SearchResponse EvalDiff(MusicInfo musicInfo, string value, byte type)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new SearchResponse($"input error: received null or empty value in 'diff'",-1);
            }
            bool diffIncludeString = false;
            var range = Range.InvalidRange;

            value = value.Trim(' ');
            if (value == "?")
            {
                diffIncludeString = true;
            }
            else if (!Utils.ParseRange(value, out range))
            {
                return new SearchResponse($"search error: failed to evaluate range \"{value}\"", -1);
            }

            Utils.GetAvailableMaps(musicInfo, out var availableMaps);

            foreach (int i in TermDiffTypes[type].Intersect(availableMaps))
            {
                var musicDiff = musicInfo.GetMusicLevelStringByDiff(i, false);
                if (!int.TryParse(musicDiff, out int x))
                {
                    if (diffIncludeString)
                    {
                        return SearchResponse.PassedTest;
                    };

                }
                else if (range.Contains(x))
                {
                    return SearchResponse.PassedTest;
                }
            }
            return SearchResponse.FailedTest;
        }
        internal static SearchResponse EvalCallback(MusicInfo musicInfo, string value, byte type)
        {
            if (value == null)
            {
                return new SearchResponse($"input error: received null value in 'callback'", -1);
            }

            value = value.Trim(' ');
            if (!Utils.ParseRange(value, out var range))
            {
                return new SearchResponse($"search error: failed to evaluate range \"{value}\"", -1);
            }

            Utils.GetAvailableMaps(musicInfo, out var availableMaps);


            foreach (int i in availableMaps)
            {
                var x = musicInfo.GetCallBackMusicLevelIntByDiff(i, false);
                if (range.Contains(x))
                {
                    return SearchResponse.PassedTest;
                }
            }
            return SearchResponse.FailedTest;
        }
        internal static SearchResponse EvalHidden(MusicInfo musicInfo, int type)
        {
            var musicDiff = musicInfo.GetMusicLevelStringByDiff(type, false);
            if (string.IsNullOrEmpty(musicDiff) || musicDiff == "0" || (EvalCustom(musicInfo) && !AlbumManager.LoadedAlbumsByUid[musicInfo.uid].availableMaps.Keys.Contains(type)))
            {
                return SearchResponse.FailedTest;
            }
            return SearchResponse.PassedTest;
        }
        internal static SearchResponse EvalScene(MusicInfo musicInfo, string value)
        {
            if (value == null)
            {
                return new SearchResponse("search error: received null as 'scene'", -1);
            }
            value = value.Trim(' ');
            string sceneFilter = null;
            switch (value.Length)
            {
                case 0:
                    return new SearchResponse("search error: received an empty string as 'scene'", -1);
                case 1:
                    if (!char.IsDigit(value[0]))
                    {
                        return new SearchResponse("search error: expected digit as single character input for 'scene'", -1);
                    }
                    sceneFilter = '0' + value;
                    break;
                case 2:
                    if (!value.All(x => char.IsDigit(x)))
                    {
                        return new SearchResponse("search error: expected two digits as double character input for 'scene'", -1);
                    }
                    sceneFilter = value;
                    break;
                default:
                    var t = validScenes.Keys.Where(x => x.Contains(value)).ToArray();
                    if (t.Length > 1)
                    {
                        if (t.Select(x => validScenes[x]).ToHashSet().Count > 1)
                        {
                            return new SearchResponse($"search error: scene filter search \"{t}\" is ambiguous between {string.Join(", ", t.Reverse().Skip(1).Reverse().Select(x => '"' + x + '"'))} and \"{t.Last()}\"", -1);
                        }
                    }
                    else if (t.Length < 1)
                    {
                        return new SearchResponse($"search error: scene filter \"{t}\" couldn't be found", -1);
                    }
                    sceneFilter = validScenes[t[0]];
                    break;
            }
            if (musicInfo.scene.Substring(6) == sceneFilter)
            {
                return SearchResponse.PassedTest;
            }
            return SearchResponse.FailedTest;
        }
        internal static bool EvalCinema(MusicInfo musicInfo)
        {
            if (!EvalCustom(musicInfo))
            {
                return false;
            }
            var customInfo = AlbumManager.LoadedAlbumsByUid[musicInfo.uid];
            if (!customInfo.IsPackaged)
            {
                try
                {
                    JObject items = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(Path.Combine(customInfo.BasePath, "cinema.json")));
                    if (!File.Exists(Path.Combine(customInfo.BasePath, (string)items["file_name"])))
                    {
                        return false;
                    }
                    return true;
                }
                catch (Exception) { }
                return false;
            }
            var lastModified = File.GetLastAccessTimeUtc(customInfo.BasePath);
            if (lastChecked >= lastModified)
            {
                return hasCinema.Contains(musicInfo.uid);
            }
            isModified = true;
            if (Utils.TryParseCinemaJson(customInfo))
            {
                hasCinema.Add(musicInfo.uid);
                return true;
            };
            hasCinema.Remove(musicInfo.uid);
            return false;
        }

        internal static DateTime lastChecked;

        internal static bool isModified;
        internal static bool EvalUnplayed(MusicInfo musicInfo)
        {
            string s = musicInfo.uid + "_";

            return RefreshPatch.highScores.FindIndex(x => ((string)x["uid"]).StartsWith(s)) == -1;
        }
        internal static SearchResponse EvalUnplayed(MusicInfo musicInfo, string value)
        {
            if (!Utils.GetAvailableMaps(musicInfo, out var availableMaps))
            {
                return SearchResponse.FailedTest;
            }
            value = value.Trim(' ');
            if (value != "?")
            {
                var result = Utils.ParseRange(value, out var range, 1, 5);
                if (result == null)
                {
                    return new SearchResponse($"search error: failed to parse range \"{value}\"", -1);
                }
                if (result == false)
                {
                    return new SearchResponse($"search error: \"{value}\" isn't within the acceptable range of values", -1);
                }
                var t = availableMaps.Where((int x) => range.Contains(x));
                if (!t.Any())
                {
                    return SearchResponse.FailedTest;
                }
                return t.Any(x => !IsUnplayed(musicInfo, x))
                    ? SearchResponse.FailedTest
                    : SearchResponse.PassedTest;
            }
            if (!availableMaps.Any())
            {
                return SearchResponse.FailedTest;
            }
            return IsUnplayed(musicInfo, availableMaps.Max())
                ? SearchResponse.PassedTest
                : SearchResponse.FailedTest;
        }
        internal static bool IsUnplayed(MusicInfo musicInfo, int diff)
        {
            string s = musicInfo.uid + "_" + diff;
            return RefreshPatch.highScores.FindIndex(x => (string)x["uid"] == s) == -1;
        }
        internal static bool EvalRanked(MusicInfo musicInfo)
        {
            if (!EvalCustom(musicInfo)) return false;
            return AlbumManager.LoadedAlbumsByUid[musicInfo.uid].availableMaps.Values.Any(x => isHeadquarters.Contains(x));
        }

        internal static ConcurrentDictionary<string, Range> bpmDict = new ConcurrentDictionary<string, Range>();
        internal static SearchResponse EvalBPM(MusicInfo musicInfo, string value)
        {
            if (value == null)
            {
                return new SearchResponse("input error: received null as value in 'bpm'", -1);
            }
            if (!bpmDict.ContainsKey(musicInfo.uid))
            {
                AddBPMInfo(musicInfo);
                return EvalBPM(musicInfo, value);
            }
            value = value.Trim(' ');
            var bpmInfo = bpmDict[musicInfo.uid];
            if (bpmInfo == null)
            {
                if (value == "?")
                {
                    return SearchResponse.PassedTest;
                }
                return SearchResponse.FailedTest;
            }
            if (value == "?")
            {
                return SearchResponse.FailedTest;
            }
            if (!Utils.ParseRange(value, out var bpmRange))
            {
                return new SearchResponse($"search error: failed to evaluate range \"{value}\"", -1);
            }
            if (bpmRange.IsOverlap(bpmInfo))
            {
                return SearchResponse.PassedTest;
            }
            return SearchResponse.FailedTest;
        }
        internal static void AddBPMInfo(MusicInfo musicInfo)
        {
            if (Utils.DetectParseBPM(musicInfo.bpm, out var range))
            {
                bpmDict[musicInfo.uid] = range;
                return;
            }
            bpmDict[musicInfo.uid] = null;
        }
        internal static bool EvalTitle(PeroString pStr, MusicInfo musicInfo, string value)
        {

            if (pStr.LowerContains(musicInfo.name ?? "", value))
            {
                return true;
            }

            if (EvalCustom(musicInfo) && pStr.LowerContains(AlbumManager.LoadedAlbumsByUid[musicInfo.uid].Info.name_romanized ?? "", value))
            {
                return true;
            }

            for (int i = 1; i <= 5; i++)
            {
                if (pStr.LowerContains(musicInfo.GetLocal(i).name ?? "", value))
                {
                    return true;
                }
            }

            return false;
        }
        internal static bool EvalTag(PeroString pStr, MusicInfo musicInfo, string value)
        {
            var uidToInfo = Singleton<ConfigManager>.instance.GetConfigObject<DBConfigMusicSearchTag>(0).m_Dictionary;
            if (uidToInfo.ContainsKey(musicInfo.uid))
            {
                var tags = uidToInfo[musicInfo.uid]?.tag;
                if (tags != null)
                {
                    foreach (var tag in tags)
                    {
                        if (pStr.LowerContains(tag ?? "", value))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        internal static bool EvalAuthor(PeroString pStr, MusicInfo musicInfo, string value)
        {
            if (pStr.LowerContains(musicInfo.author ?? "", value))
            {
                return true;
            }
            for (int i = 1; i <= 5; i++)
            {
                if (pStr.LowerContains(musicInfo.GetLocal(i).author ?? "", value))
                {
                    return true;
                }
            }
            return false;
        }
        internal static bool EvalDesigner(PeroString pStr, MusicInfo musicInfo, string value)
        {
            if (pStr.LowerContains(musicInfo.levelDesigner ?? "", value))
            {
                return true;
            }
            Utils.GetAvailableMaps(musicInfo, out var availableMaps);
            foreach (var i in availableMaps)
            {
                if (pStr.LowerContains(musicInfo.GetLevelDesignerStringByIndex(i) ?? "", value))
                {
                    return true;
                }
            }
            return false;
        }
        internal static bool EvalAny(PeroString pStr, MusicInfo musicInfo, string filter)
        {
            return (EvalTag(pStr, musicInfo, filter) || EvalTitle(pStr, musicInfo, filter) || EvalAuthor(pStr, musicInfo, filter) || EvalDesigner(pStr, musicInfo, filter));
        }
        internal static bool EvalAnyX(PeroString pStr, MusicInfo musicInfo, string filter)
        {
            if (searchTags.ContainsKey(musicInfo.uid))
            {
                foreach (var searchTag in searchTags[musicInfo.uid])
                {
                    if (pStr.LowerContains(searchTag, filter))
                    {
                        return true;
                    }
                }
            }
            return EvalAny(pStr, musicInfo, filter);
        }
        internal static SearchResponse EvalAcc(MusicInfo musicInfo, string value)
        {
            if (!Utils.GetAvailableMaps(musicInfo, out var availableMaps))
            {
                return SearchResponse.FailedTest;
            }
            var splitValue = value.Trim(' ').Split(' ').Where(x => x != "").ToArray();
            Range diffRange;
            Range accRange;
            if (splitValue.Length == 1)
            {
                value = splitValue[0];
                var result = Utils.ParseRange(value, out accRange, 0, 100);
                if (result == null)
                {
                    return new SearchResponse($"search error: failed to parse range \"{value}\"", -1);
                }
                if (result == false)
                {
                    return new SearchResponse($"search error: \"{value}\" isn't within the acceptable range of values", -1);
                }
                accRange.Update(accRange.Start/100, accRange.End/100);
            }
            else if (splitValue.Length == 2)
            {
                var result = Utils.ParseRange(splitValue[0], out accRange, 0, 100);
                if (result == null)
                {
                    if (splitValue[0] == "?")
                    {
                        return new SearchResponse($"search error: wildcard '?' is not allowed in this context", -1);
                    }
                    return new SearchResponse($"search error: failed to parse range \"{value}\"", -1);
                }
                if (result == false)
                {
                    return new SearchResponse($"search error: \"{splitValue[0]}\" isn't within the acceptable range of values", -1);
                }
                accRange.Update(accRange.Start / 100, accRange.End / 100);
                result = Utils.ParseRange(splitValue[1], out diffRange, 1, 5);
                if (result == null)
                {
                    if (splitValue[1] != "?")
                    {
                        return new SearchResponse($"search error: failed to parse range \"{splitValue[1]}\"", -1);
                    }
                    availableMaps = new HashSet<int>() { availableMaps.Max() };
                }
                else if (result == false)
                {
                    return new SearchResponse($"search error: \"{splitValue[1]}\" isn't within the acceptable range of values", -1);
                }
                else
                {
                    availableMaps = availableMaps.Where(x => diffRange.Contains(x)).ToHashSet();
                }
            }
            else
            {
                return new SearchResponse($"search error: how the fuck? (evalAcc, ${value}$)", -1);
            }

            if (availableMaps.Count == 0)
            {
                return SearchResponse.FailedTest;
            }

            foreach (var diff in availableMaps)
            {
                string s = musicInfo.uid + "_" + diff;
                if (!RefreshPatch.highScores.Any(x => (string)x["uid"] == s && accRange.Contains((float)x["accuracy"])))
                {
                    return SearchResponse.FailedTest;
                };
            }
            return SearchResponse.PassedTest;
        }
        internal static SearchResponse EvalFC(MusicInfo musicInfo, string value)
        {
            if (!Utils.GetAvailableMaps(musicInfo, out var availableMaps, out var isCustom))
            {
                return SearchResponse.FailedTest;
            }
            value = value.Trim(' ');
            if (value == "?")
            {
                if (!availableMaps.Any())
                {
                    return SearchResponse.FailedTest;
                }
                return RefreshPatch.fullCombos.Contains(musicInfo.uid + "_" + availableMaps.Max())
                    ? SearchResponse.PassedTest
                    : SearchResponse.FailedTest;
            }
            var result = Utils.ParseRange(value, out var range, 1, 5);
            if (result == null)
            {
                return new SearchResponse($"search error: failed to parse range \"{value}\"", -1);
            }
            if (result == false)
            {
                return new SearchResponse($"search error: \"{value}\" isn't within the acceptable range of values", -1);
            }
            availableMaps = availableMaps.Where(x => range.Contains(x)).ToHashSet();
            if (!availableMaps.Any())
            {
                return SearchResponse.FailedTest;
            }
            foreach (var i in availableMaps)
            {
                string s = musicInfo.uid + "_" + i;
                if (!RefreshPatch.fullCombos.Contains(s))
                {
                    return SearchResponse.FailedTest;
                };
            }
            return SearchResponse.PassedTest;
        }
        internal static bool EvalFC(MusicInfo musicInfo)
        {
            if (!Utils.GetAvailableMaps(musicInfo, out var availableMaps))
            {
                return false;
            }

            string s = musicInfo.uid + "_";
            foreach (var diff in availableMaps)
            {
                var t = s + diff;
                if (!RefreshPatch.fullCombos.Contains(t))
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool EvalRecent(MusicInfo musicInfo)
        {
            if (!AlbumManager.LoadedAlbumsByUid.TryGetValue(musicInfo.uid, out var album))
            {
                return false;
            }
            return File.GetLastWriteTimeUtc(album.BasePath) >= ModMain.RecentDateLimit;
        }
        internal static SearchResponse EvalRecent(MusicInfo musicInfo, string value)
        {
            if (!AlbumManager.LoadedAlbumsByUid.ContainsKey(musicInfo.uid))
            {
                return SearchResponse.FailedTest;
            }
            bool isTop = int.TryParse(value, out int top);
            Range range = null;

            if (isTop)
            {
                if (top < 1)
                {
                    return new SearchResponse("non-positive number is not valid for 'recent'", -1);
                }
            }
            else
            {
                if (!Utils.ParseRange(value, out range))
                {
                    return new SearchResponse("failed to parse value for 'recent'", -1);
                }
                if (range.Start < 1)
                {
                    if (range.Start != double.NegativeInfinity)
                    {
                        return new SearchResponse("non-positive range is not valid for 'recent'", -1);
                    }
                    try
                    {
                        range.Start = 1;
                    }
                    catch (Exception)
                    {
                        return new SearchResponse("non-positive range is not valid for 'recent'", -1);
                    }
                }
            }
            if (sortedByLastModified == null)
            {
                sortedByLastModified = AlbumManager.LoadedAlbumsByUid.OrderByDescending(x => File.GetLastWriteTimeUtc(x.Value.BasePath)).Select(x => x.Key).ToList();
            }
            var idx = sortedByLastModified.IndexOf(musicInfo.uid);

            if (idx == -1)
            {
                return SearchResponse.FailedTest;
            }
            else if (isTop ? (idx >= top) : !range.Contains(idx))
            {
                return SearchResponse.FailedTest;
            }
            return SearchResponse.PassedTest;
        }
        internal static SearchResponse EvalOld(MusicInfo musicInfo, string value)
        {
            if (!AlbumManager.LoadedAlbumsByUid.ContainsKey(musicInfo.uid))
            {
                return SearchResponse.FailedTest;
            }
            bool isTop = int.TryParse(value, out int top);
            Range range = null;

            if (isTop)
            {
                if (top < 1)
                {
                    return new SearchResponse("non-positive number is not valid for 'old'", -1);
                }
            }
            else
            {
                if (!Utils.ParseRange(value, out range))
                {
                    return new SearchResponse("failed to parse value for 'old'", -1);
                }
                if (range.Start < 1)
                {
                    if (range.Start != double.NegativeInfinity)
                    {
                        return new SearchResponse("non-positive range is not valid for 'old'", -1);
                    }
                    try
                    {
                        range.Start = 1;
                    }
                    catch (Exception)
                    {
                        return new SearchResponse("non-positive range is not valid for 'recent'", -1);
                    }
                }
            }
            if (sortedByLastModified == null)
            {
                sortedByLastModified = AlbumManager.LoadedAlbumsByUid.OrderByDescending(x => File.GetLastWriteTimeUtc(x.Value.BasePath)).Select(x => x.Key).ToList();
            }
            var idx = sortedByLastModified.IndexOf(musicInfo.uid);

            if (idx == -1)
            {
                return SearchResponse.FailedTest;
            }
            else if (isTop ? ((sortedByLastModified.Count - idx) > top) : !range.Contains(sortedByLastModified.Count - idx))
            {
                return SearchResponse.FailedTest;
            }
            return SearchResponse.PassedTest;
        }
        internal static SearchResponse EvalAlbum(MusicInfo musicInfo, string value)
        {
            if (value == null)
            {
                return new SearchResponse("received null value in 'album'", -1);
            }
            if (!albumNames.TryGetValue(musicInfo.m_MusicExInfo.m_AlbumUidIndex, out var albumName))
            {
                return SearchResponse.FailedTest;
            }
            return albumName.LowerContains(value)
                ? SearchResponse.PassedTest
                : SearchResponse.FailedTest;
        }
        internal static SearchResponse EvalSort(string value, bool inverse)
        {
            if (value == null)
            {
                return new SearchResponse("received null value in 'sort'", -1);
            }
            if (!ComparisonInfo.IsSorterRegistered(value))
            {
                return new SearchResponse($"sorting type '{value}' is not registered", -1);
            }
            ComparisonInfo.ActivateSorter(value, inverse);
            return SearchResponse.PassedTest;

        }
        public static SearchResponse Term_Diff(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            return EvalDiff(musicInfo, valueOverride ?? value, 0);
        }
        public static SearchResponse Term_Hidden(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            if (value == null)
            {
                return EvalHidden(musicInfo, 4);
            }
            return EvalDiff(musicInfo, value, 1);
        }
        public static SearchResponse Term_Touhou(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            if (value == null)
            {
                return EvalHidden(musicInfo, 5);
            }
            return EvalDiff(musicInfo, value, 2);
        }
        public static SearchResponse Term_Cinema(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            return EvalCinema(musicInfo) ? SearchResponse.PassedTest : SearchResponse.FailedTest;
        }
        public static SearchResponse Term_Custom(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            return EvalCustom(musicInfo) ? SearchResponse.PassedTest : SearchResponse.FailedTest;
        }
        public static SearchResponse Term_Scene(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            return EvalScene(musicInfo, value);
        }
        public static SearchResponse Term_Def(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            return EvalDefine(peroString, musicInfo, value, valueOverride);
        }
        public static SearchResponse Term_Unplayed(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            if (value == null)
            {
                return EvalUnplayed(musicInfo) ? SearchResponse.PassedTest : SearchResponse.FailedTest;
            }
            return EvalUnplayed(musicInfo, value);
        }
        public static SearchResponse Term_Ranked(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            return EvalRanked(musicInfo) ? SearchResponse.PassedTest : SearchResponse.FailedTest;
        }
        public static SearchResponse Term_BPM(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            return EvalBPM(musicInfo, value);
        }
        public static SearchResponse Term_Title(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            return EvalTitle(peroString, musicInfo, value) ? SearchResponse.PassedTest : SearchResponse.FailedTest;
        }
        public static SearchResponse Term_Tag(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            return EvalTag(peroString, musicInfo, value) ? SearchResponse.PassedTest : SearchResponse.FailedTest;
        }
        public static SearchResponse Term_Author(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            return EvalAuthor(peroString, musicInfo, value) ? SearchResponse.PassedTest : SearchResponse.FailedTest;
        }
        public static SearchResponse Term_Designer(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            return EvalDesigner(peroString, musicInfo, value) ? SearchResponse.PassedTest : SearchResponse.FailedTest;
        }
        public static SearchResponse Term_Any(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            return EvalAny(peroString, musicInfo, value) ? SearchResponse.PassedTest : SearchResponse.FailedTest;
        }
        public static SearchResponse Term_AnyX(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            return EvalAnyX(peroString, musicInfo, value) ? SearchResponse.PassedTest : SearchResponse.FailedTest;
        }
        public static SearchResponse Term_Acc(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            return EvalAcc(musicInfo, value);
        }
        public static SearchResponse Term_FC(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            if (value == null)
            {
                return EvalFC(musicInfo) ? SearchResponse.PassedTest : SearchResponse.FailedTest;
            }
            return EvalFC(musicInfo, value);
        }
        public static SearchResponse Term_Eval(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            var result = SearchParser.EvaluateSearch(SearchParser.ParseSearchText(value), musicInfo, peroString, valueOverride);
            if (result.Code == -2)
            {
                var modified = new SearchResponse(runtimeParserText + result.Message, result.Suggestion, result.Code);
                if (!result._positionValue.HasValue)
                {
                    modified.Position = result.Position;
                }
                return modified;
            }
            return result;
        }
        public static SearchResponse Term_History(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            return RefreshPatch.history.Contains(musicInfo.uid)
                ? SearchResponse.PassedTest
                : SearchResponse.FailedTest;
        }

        internal static List<string> sortedByLastModified;
        public static SearchResponse Term_Recent(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            if (value == null)
            {
                return EvalRecent(musicInfo)
                ? SearchResponse.PassedTest
                : SearchResponse.FailedTest;
            }
            return EvalRecent(musicInfo, value);
            
        }

        public static SearchResponse Term_Old(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            return EvalOld(musicInfo, valueOverride ?? value);
        }
        internal static Random randomGenerator = new Random();
        public static SearchResponse Term_Random(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            value = valueOverride ?? value;
            if (value == null)
            {
                return randomGenerator.Next(0, 2) == 1
                    ? SearchResponse.PassedTest
                    : SearchResponse.FailedTest;
            }
            if (!double.TryParse(value, out var n))
            {
                return new SearchResponse("failed to parse value for 'random'", -1);
            }
            if (n < 1)
            {
                return new SearchResponse("value for 'random' may not be smaller than 1", -1);
            }
            var chance = 1 / n;

            return randomGenerator.NextDouble() < chance
                ? SearchResponse.PassedTest
                : SearchResponse.FailedTest;
        }

        internal static Dictionary<int, string> albumNames;
        public static SearchResponse Term_Album(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            return EvalAlbum(musicInfo, valueOverride ?? value);
        }
        public static SearchResponse Term_Callback(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            return EvalCallback(musicInfo, valueOverride ?? value, 0);
        }
        public static SearchResponse Term_New(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            return DBMusicTagDefine.newMusicUids.Contains(musicInfo.uid)
                ? SearchResponse.PassedTest
                : SearchResponse.FailedTest;
        }
        public static SearchResponse Term_Sort(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            return EvalSort(valueOverride ?? value, false);
        }
        public static SearchResponse Term_ReverseSort(MusicInfo musicInfo, PeroString peroString, string value, string valueOverride = null)
        {
            return EvalSort(valueOverride ?? value, true);
        }

        internal static Dictionary<string, string[]> searchTags = new Dictionary<string, string[]>
        {
            ["0-48"] = new string[] { "3R2", "Magical Wonderland (More colorful mix) ", "Howard_Y" },
            ["0-0"] = new string[] { "小野道ono", "Iyaiya ", "Howard_Y" },
            ["0-2"] = new string[] { "Haloweak", "Wonderful Pain ", "Howard_Y" },
            ["0-3"] = new string[] { "TetraCalyx", "Breaking Dawn ", "Howard_Y" },
            ["0-4"] = new string[] { "小野道ono", "One-way subway Feat.karin   单向地铁 Feat.karin   單向地鐵 Feat.karin", "Howard_Y" },
            ["0-1"] = new string[] { "Zris", "Frost Land ", "Howard_Y" },
            ["0-5"] = new string[] { "TetraCalyx", "Heart-Pounding Flight ", "Howard_Y" },
            ["0-29"] = new string[] { "3R2", "Pancake is Love ", "Howard_Y" },
            ["0-6"] = new string[] { "TetraCalyx", "Time Graffiti   时光涂鸦   時光塗鴉", "Howard_Y" },
            ["0-37"] = new string[] { "Haloweak", "Evolution ", "Howard_Y" },
            ["0-7"] = new string[] { "小野道ono", "Dolphins and radio feat.Uranyan   海豚与广播 feat.Uranyan   海豚與廣播 feat.烏拉喵   海豚与广播 feat.乌拉喵", "Howard_Y" },
            ["0-8"] = new string[] { "天游 feat.东京塔子", "Yuki no Shizuku Ame no Oto   雪の雫・雨の音 ", "Howard_Y" },
            ["0-43"] = new string[] { "小野道ono", "Best One feat.tooko   Best One feat.墨橙 ", "Howard_Y" },
            ["0-31"] = new string[] { "ANK feat.熊子", "Candy Color Love Science   糖果色恋爱学   糖果色戀愛學", "Howard_Y" },
            ["0-38"] = new string[] { "cnsouka", "Night Wander (cnsouka Remix) ", "Howard_Y" },
            ["0-46"] = new string[] { "水夏える feat.月乃", "Dohna Dohna no Uta   ドーナドーナのうた ", "Howard_Y" },
            ["0-9"] = new string[] { "3R2", "Spring Carnival ", "Howard_Y" },
            ["0-30"] = new string[] { "ANK feat.熊子", "DISCO NIGHT ", "Howard_Y" },
            ["0-49"] = new string[] { "REDALiCE feat. 犬山たまき", "Koi no Moonlight   恋のMoonlight ", "Howard_Y" },
            ["0-10"] = new string[] { "小野道ono", "Love voice navigation feat.yousa   恋爱语音导航 feat.yousa   戀愛語音導航 feat.yousa", "Howard_Y" },
            ["0-11"] = new string[] { "Ayatsugu_Otowa", "Lights of Muse ", "Howard_Y" },
            ["0-12"] = new string[] { "MusMus", "midstream jam ", "Howard_Y" },
            ["0-40"] = new string[] { "MusikM", "Nihao   ニーハオ   你好 ", "Howard_Y" },
            ["0-13"] = new string[] { "Haloweak & G.K", "Confession ", "Howard_Y" },
            ["0-32"] = new string[] { "M2U", "Galaxy Striker ", "Howard_Y" },
            ["0-14"] = new string[] { "daisan", "Departure Road ", "Howard_Y" },
            ["0-15"] = new string[] { "Haloweak", "Bass Telekinesis ", "Howard_Y" },
            ["0-16"] = new string[] { "a_hisa", "Cage of Almeria   アルメリアの鳥籠 ", "Howard_Y" },
            ["0-17"] = new string[] { "Lactic Acid Bacteria", "Ira ", "Howard_Y" },
            ["0-18"] = new string[] { "Chicala Lpis", "Blackest Luxury Car ", "Howard_Y" },
            ["0-19"] = new string[] { "JurokuNeta.", "Medicine of Sing ", "Howard_Y" },
            ["0-20"] = new string[] { "daisan", "irregulyze ", "Howard_Y" },
            ["0-47"] = new string[] { "かめりあ feat.ななひら", "I don't care about Christmas though   クリスマスなんて興味ないけど ", "Howard_Y" },
            ["0-21"] = new string[] { "uma", "Imaginary World ", "Howard_Y" },
            ["0-22"] = new string[] { "a_hisa", "Dysthymia ", "Howard_Y" },
            ["0-42"] = new string[] { "綾奈なな (Prod.Aya2g+Works)", "from the new world   新世界より", "Howard_Y" },
            ["0-33"] = new string[] { "天游", "NISEGAO   僞顏 ", "Howard_Y" },
            ["0-44"] = new string[] { "白上フブキ", "Say! Fanfare!   Say!ファンファーレ! ", "Howard_Y" },
            ["0-34"] = new string[] { "iKz feat.漆柚", "Star Driver   流星车手 ", "CattLelya" },
            ["0-23"] = new string[] { "SLT", "Formation ", "Howard_Y" },
            ["0-24"] = new string[] { "iKz", "Shinsou Masui   心層麻酔   心层麻醉 ", "Howard_Y" },
            ["0-50"] = new string[] { "bermei.inazawa", "Mezame Eurythmics ", "Howard_Y" },
            ["0-51"] = new string[] { "bermei.inazawa", "Shenri Kuaira -repeat- ", "Howard_Y" },
            ["0-25"] = new string[] { "SLT", "Latitude ", "Howard_Y" },
            ["0-39"] = new string[] { "Sound Souler", "Aqua Stars ", "Howard_Y" },
            ["0-26"] = new string[] { "モリモリあつし", "Funkotsu Saishin Casino   粉骨砕身カジノゥ ", "Howard_Y" },
            ["0-27"] = new string[] { "a_hisa", "Clock Room & Spiritual World   時計の部屋と精神世界 ", "Howard_Y" },
            ["0-52"] = new string[] { "『NEEDY GIRL OVERDOSE』主題歌", "INTERNET OVERDOSE（Aiobahn feat.KOTOKO） ", "Howard_Y" },
            ["0-35"] = new string[] { "Aya~亜夜罪~", "Actress flower    徒 花", "Howard_Y" },
            ["0-28"] = new string[] { "NoKANY", "Mujinku-Vacuum Track#ADD8E6-   無人区-Vacuum Track#ADD8E6- ", "Howard_Y" },
            ["0-36"] = new string[] { "モリモリあつし", "MilK ", "Howard_Y" },
            ["0-41"] = new string[] { "まめこ", "umpopoff ", "Howard_Y" },
            ["0-45"] = new string[] { "LeaF", "Mopemope   もぺもぺ ", "Howard_Y in_tha_W0ndeR1and_ Howard_Y in_tha_W0ndeR1and_ Howard_Y in_tha_W0ndeR1and_ Mwfyuma Peustallora" },
            ["1-0"] = new string[] { "yk!", "Sunshine and Rainbow after August Rain   八月の雨上がり、晴れ後レインボー   八月雨後，初晴與彩虹   八月雨后，初晴与彩虹 ", "Howard_Y" },
            ["1-1"] = new string[] { "味素", "Magical Number   マジカルナンバー   魔法數字   魔法数字 ", "Howard_Y" },
            ["1-2"] = new string[] { "Nano&板烧鹅尼子", "Dreaming Girl ", "Howard_Y" },
            ["1-3"] = new string[] { "Ncha-P", "Daruma-san Fell Over ", "Howard_Y" },
            ["1-4"] = new string[] { "Ncha-P", "Different ", "Howard_Y" },
            ["1-5"] = new string[] { "Ncha-P", "The Future of the Phantom ", "Howard_Y" },
            ["2-0"] = new string[] { "ginkiha", "Oriens ", "Howard_Y" },
            ["2-1"] = new string[] { "モリモリあつし", "PUPA ", "Howard_Y" },
            ["2-2"] = new string[] { "Sakamiya feat.小宮真央", "Luna Express 2032 ", "Howard_Y" },
            ["2-3"] = new string[] { "魔界都市ニイガタ", "Ukiyoe Yokochou   浮世絵横丁 ", "Howard_Y" },
            ["2-4"] = new string[] { "LeaF", "Alice in Misanthrope   Alice in Misanthrope -厭世アリス- ", "Howard_Y" },
            ["2-5"] = new string[] { "EBIMAYO", "GOODMEN ", "Howard_Y" },
            ["3-0"] = new string[] { "アリスシャッハと魔法の楽団", "Maharajah ", "Howard_Y" },
            ["3-1"] = new string[] { "uma", "keep on running ", "Howard_Y" },
            ["3-2"] = new string[] { "Chicala Lpis", "Käfig ", "Howard_Y" },
            ["3-3"] = new string[] { "daisan", "-+ ", "Howard_Y" },
            ["3-4"] = new string[] { "a_hisa", "Tenri Kaku Jou   天理鶴情 ", "Howard_Y" },
            ["3-5"] = new string[] { "JurokuNeta.", "Adjudicatorz-DanZai-   Adjudicatorz-断罪- ", "Howard_Y" },
            ["4-0"] = new string[] { "暖@よみぃ", "MUSEDASH!!!! ", "nikkukun" },
            ["4-1"] = new string[] { "削除", "Imprinting ", "Howard_Y" },
            ["4-2"] = new string[] { "SLT", "Skyward ", "GearMaster_PH" },
            ["4-3"] = new string[] { "Polymath9", "La nuit de vif ", "Howard_Y" },
            ["4-4"] = new string[] { "Yamajet", "Bit-alize ", "Amami.A.Myamsar" },
            ["4-5"] = new string[] { "EBIMAYO", "GOODTEK(Hyper Edit) ", "Howard_Y" },
            ["5-0"] = new string[] { "アリスシャッハと魔法の楽団", "Thirty Million Persona ", "Zpm" },
            ["5-1"] = new string[] { "siromaru + cranky", "conflict ", "Howard_Y" },
            ["5-2"] = new string[] { "uma with モリモリあつし feat.ましろ", "Enka Dance Music   演華舞踊 ~Enka Dance Music~ ", "Howard_Y" },
            ["5-3"] = new string[] { "ginkiha", "XING ", "Howard_Y" },
            ["5-4"] = new string[] { "Sakamiya feat.あらたきき", "Amakakeru Soukyuu no Serenade   天翔ける蒼穹のセレナーデ ", "LuiCat" },
            ["5-5"] = new string[] { "K.key", "Gift box ", "baozale" },
            ["6-0"] = new string[] { "Sound Souler", "Out of Sense ", "CattLelya" },
            ["6-1"] = new string[] { "HyuN feat.Yu-A", "My Life Is For You ", "Howard_Y" },
            ["6-2"] = new string[] { "Polymath9", "Etude -Sunset- ", "Howard_Y" },
            ["6-3"] = new string[] { "KillerBlood", "Goodbye Boss   翹班   翘班 ", "money钱" },
            ["6-4"] = new string[] { "a_hisa", "Stargazer ", "Phizer Phizer Phizer Howard_Y" },
            ["6-5"] = new string[] { "Chicala Lpis", "Lys Tourbillon ", "Howard_Y" },
            ["7-0"] = new string[] { "uma feat.ましろ", "Brave My Soul ", "nikkukun" },
            ["7-1"] = new string[] { "xi", "Halcyon ", "Howard_Y" },
            ["7-2"] = new string[] { "VeetaCrush", "Crimson Nightingle ", "Samplex" },
            ["7-3"] = new string[] { "SLT", "Invader ", "HXJ_ConveX" },
            ["7-4"] = new string[] { "ユメミド", "Lyrith   Lyrith -迷宮リリス- ", "Howard_Y" },
            ["7-5"] = new string[] { "EBIMAYO", "GOODBOUNCE (Groove Edit) ", "Howard_Y" },
            ["8-0"] = new string[] { "削除", "Altale ", "Howard_Y" },
            ["8-1"] = new string[] { "NOMA", "Brain Power ", "Howard_Y" },
            ["8-2"] = new string[] { "Freezer feat.妃苺", "Berry Go!! ", "Howard_Y" },
            ["8-3"] = new string[] { "モリモリあつし vs. uma", "Sweet* Witch* Girl* ", "Howard_Y" },
            ["8-4"] = new string[] { "KAH", "trippers feeling! ", "Phizer Phizer Phizer Howard_Y" },
            ["8-5"] = new string[] { "ikaruga_nex", "Lilith ambivalence lovers ", "Howard_Y" },
            ["9-0"] = new string[] { "a_hisa", "Leave It Alone ", "snowsurvivor" },
            ["9-1"] = new string[] { "Sakamiya feat.前野", "Tsubasa no Oreta Tenshitachi no Requiem   翼の折れた天使たちのレクイエム ", "Howard_Y" },
            ["9-2"] = new string[] { "Lime", "Chronomia ", "Howard_Y" },
            ["9-3"] = new string[] { "VeetaCrush", "Dandelion's Daydream ", "Howard_Y" },
            ["9-4"] = new string[] { "黄泉路テヂーモ", "Lorikeet ~Flat design~   ロリキート ~Flat design~ ", "Howard_Y" },
            ["9-5"] = new string[] { "EBIMAYO", "GOODRAGE ", "Howard_Y" },
            ["10-0"] = new string[] { "削除 feat.Nikki Simmons", "Destr0yer ", "Howard_Y" },
            ["10-1"] = new string[] { "モリモリあつし vs. uma", "Noël ", "Howard_Y" },
            ["10-2"] = new string[] { "LeaF", "Kyoukiranbu   狂喜蘭舞 ", "Howard_Y" },
            ["10-3"] = new string[] { "litmus* vs Sound Souler", "Two Phace ", "無極Rayix" },
            ["10-4"] = new string[] { "HyuN & Ritoru", "Fly Again ", "Amami.A.Myamsar" },
            ["10-5"] = new string[] { "Negentropy", "ouroVoros ", "Howard_Y" },
            ["11-0"] = new string[] { "uma feat.ましろ", "Brave My Heart ", "Howard_Y" },
            ["11-1"] = new string[] { "Street", "Sakura Fubuki ", "Howard_Y" },
            ["11-2"] = new string[] { "Lime", "8bit Adventurer ", "Howard_Y" },
            ["11-3"] = new string[] { "Sakamiya feat.由地波瑠", "Suffering of screw ", "Howard_Y" },
            ["11-4"] = new string[] { "Chicala Lpis", "tiny lady ", "HXJ_ConveX" },
            ["11-5"] = new string[] { "EBIMAYO", "Power Attack ", "Howard_Y" },
            ["12-0"] = new string[] { "Team Grimoire feat.Sennzai", "Gaikan Chrysalis   蓋棺クリサリス ", "Howard_Y" },
            ["12-1"] = new string[] { "VeetaCrush", "Sterelogue ", "Howard_Y" },
            ["12-2"] = new string[] { "a_hisa", "Cheshire's Dance ", "Phizer" },
            ["12-3"] = new string[] { "Chicala Lpis", "Skrik ", "Phizer" },
            ["12-4"] = new string[] { "モリモリあつし vs. XIzE", "Soda Pop Canva5! ", "無極Rayix" },
            ["12-5"] = new string[] { "orangentle", "ЯUBY:LINTe   RUBY ", "Howard_Y" },
            ["13-0"] = new string[] { "ANK", "Instant Neon feat.kumako   速溶霓虹 feat.kumako   速溶霓虹 feat.熊子", "Howard_Y" },
            ["13-1"] = new string[] { "味素 feat.熊子", "星球上的追溯诗   星球上的追溯詩", "Howard_Y" },
            ["13-2"] = new string[] { "iKz", "I want to buy, buy, buy   我要买买买   我要買買買", "Howard_Y" },
            ["13-3"] = new string[] { "iKz feat.Hanser", "Dating Declaration   约会宣言   約會宣言", "Phizer" },
            ["13-4"] = new string[] { "KyuRu☆ feat.清塚幽", "First Snow   初雪", "Phizer" },
            ["13-5"] = new string[] { "Nekock·LK", "Heart on Huahai   心上华海   心上華海", "Howard_Y" },
            ["14-0"] = new string[] { "アリスシャッハと魔法の楽団", "Elysion's Old Mans ", "Howard_Y" },
            ["14-1"] = new string[] { "削除", "AXION ", "Howard_Y" },
            ["14-2"] = new string[] { "a_hisa", "Amnesia ", "HXJ_ConveX" },
            ["14-3"] = new string[] { "Freezer feat.妃苺", "Onsen Dai Sakusen   温泉大作戦 ", "Howard_Y" },
            ["14-4"] = new string[] { "adaptor", "Gleam stone ", "asdwadsxc asdwadsxc noodle" },
            ["14-5"] = new string[] { "EBIMAYO", "GOODWORLD ", "Phizer" },
            ["15-0"] = new string[] { "ANK", "The Curse feat. Sayaki Rotor   魔咒 feat.早木旋子", "Howard_Y" },
            ["15-1"] = new string[] { "cnsouka Feat.karin", "Brilliant Stars, Colorful Paintings, Travel Poems   斑斓星，彩绘，旅行诗   斑斕星，彩繪，旅行詩", "Phizer" },
            ["15-2"] = new string[] { "UncleWang Feat.熊子", "Satell Knight ", "Howard_Y" },
            ["15-3"] = new string[] { "小野道ono", "Black River Feat.Mes ", "Hit me" },
            ["15-4"] = new string[] { "Nekock·LK", "I'm sorry to be human   生而为人，我很抱歉   生而為人，我很抱歉", "Howard_Y" },
            ["15-5"] = new string[] { "MusikM", "Ueta Tori Tachi   飢えた鳥たち ", "Phizer" },
            ["16-0"] = new string[] { "Cosmograph", "Future Dive ", "press to START 1UP Trixxxter" },
            ["16-1"] = new string[] { "uma vs. モリモリあつし", "Re：End of a Dream ", "Howard_Y Howard_Y Howard_Y's Darkside" },
            ["16-2"] = new string[] { "Polymath9", "Etude -Storm- ", "Howard_Y" },
            ["16-3"] = new string[] { "Reku Mochizuki", "Unlimited Katharsis ", "Howard_Y" },
            ["16-4"] = new string[] { "Nizikawa", "Magic Knight Girl ", "Howard_Y" },
            ["16-5"] = new string[] { "ミツキ", "Eeliaas ", "Amami.A.Myamsar" },
            ["17-0"] = new string[] { "Endorfin.", "Cotton Candy Wonderland ", "Howard_Y" },
            ["17-1"] = new string[] { "立秋 feat.ちょこ", "Punai Punai Taiso   プナイプナイたいそう ", "TRIXXXTER • TERUYAMA" },
            ["17-2"] = new string[] { "P4koo(feat.rerone)", "Fly↑High ", "Howard_Y" },
            ["17-3"] = new string[] { "a_hisa", "prejudice ", "Howard_Y" },
            ["17-4"] = new string[] { "MYUKKE.", "The 89's Momentum ", "Howard_Y" },
            ["17-5"] = new string[] { "KAH", "energy night(DASH mix) ", "無極Rayix" },
            ["18-0"] = new string[] { "砂糖协会（ANK feat.熊子）", "SWEETSWEETSWEET ", "Howard_Y" },
            ["18-1"] = new string[] { "砂糖协会（ANK feat.熊子）", "Dark Blue and Night's Breath   深蓝与夜的呼吸   深藍与夜的呼吸", "Howard_Y" },
            ["18-2"] = new string[] { "MusikM / Jun Kuroda", "Joy Connection ", "money钱" },
            ["18-3"] = new string[] { "小野道ono", "Self Willed Girl Ver.B   わがまま Ver.B   任性 Ver.B ", "HXJ_ConveX" },
            ["18-4"] = new string[] { "iKz feat.Hanser", "Just disobedient   就是不听话   就是不聽話", "Howard_Y" },
            ["18-5"] = new string[] { "麦当叔劳劳", "Holy Shit Grass Snake   怖い草蛇   草蛇驚一   草蛇惊一 ", "Phizer" },
            ["19-0"] = new string[] { "Cosmograph", "INFINITY ", "Phizer∞ Phizer∞ [HD mix] Phizer∞ [SHD mix]" },
            ["19-1"] = new string[] { "立秋 feat.ちょこ", "Punai Punai Senso   プナイプナイせんそう ", "HOWARD • TERUYAMA" },
            ["19-2"] = new string[] { "Nizikawa", "Maxi ", "Amami.A.Myamsar" },
            ["19-3"] = new string[] { "siqlo", "YInMn Blue ", "money钱" },
            ["19-4"] = new string[] { "brz1128", "Plumage ", "asdwadsxc Staties asdwadsxc" },
            ["19-5"] = new string[] { "米線p.", "Dr.Techro ", "GearMaster_HY" },
            ["20-0"] = new string[] { "a_hisa", "Moonlight Banquet ", "Howard_Y" },
            ["20-1"] = new string[] { "litmus*", "Flashdance ", "Howard_Y" },
            ["20-2"] = new string[] { "Reku Mochizuki", "INFiNiTE ENERZY -Overdoze- ", "Howard_Y" },
            ["20-3"] = new string[] { "siqlo", "One Way Street ", "noodle" },
            ["20-4"] = new string[] { "EmoCosine", "This Club is Not 4 U   FOR YOU ", "HXJ_ConveX" },
            ["20-5"] = new string[] { "翡乃イスカ", "ULTRA MEGA HAPPY PARTY!!! ", "money钱" },
            ["21-0"] = new string[] { "kors k", "Glimmer feat.祈Inory", "Howard_Y" },
            ["21-1"] = new string[] { "G.K & Haloweak", "EXIST feat.米雅", "Howard_Y" },
            ["21-2"] = new string[] { "Haloweak", "Irreplaceable feat.夏铜子   Irreplaceable feat.夏銅子", "Howard_Y" },
            ["22-0"] = new string[] { "litmus*", "The NightScape ", "無極Rayix" },
            ["22-1"] = new string[] { "xi", "FREEDOM DiVE↓ ", "HOWARD_Y↓ HOWARD_Y↓ HOWARD_Y↓ HOWARD↓Y" },
            ["22-2"] = new string[] { "Street", "|o   Φ ", "Howard_Y" },
            ["22-3"] = new string[] { "ああああ + しーけー", "Lueur de la nuit ", "Howard_Y" },
            ["22-4"] = new string[] { "BTB", "Creamy Sugary OVERDRIVE!!! ", "asdwadsxc" },
            ["22-5"] = new string[] { "HyuN", "Disorder (feat.YURI) ", "money钱" },
            ["23-0"] = new string[] { "砂糖协会（ANK feat.熊子）", "Dessert after the rain   雨后甜点   雨後甜點", "Howard_Y" },
            ["23-1"] = new string[] { "S9ryne Feat.祈Inory", "Confession support formula   告白应援方程式   告白應援方程式", "Howard_Y" },
            ["23-2"] = new string[] { "Nekock·LK", "Omatsuri feat.兔子ST", "baozale" },
            ["23-3"] = new string[] { "砂糖协会（ANK feat.熊子）", "FUTUREPOP ", "Howard_Y" },
            ["23-4"] = new string[] { "sctl feat.Syepias", "The Breeze ", "Laddie Amoyensis" },
            ["23-5"] = new string[] { "諸星なな feat.加藤はるか&廣瀬祐輝", "I LOVE LETTUCE FRIED RICE!!   ウォー・アイ・レタス炒飯！！ ", "Howard_Y" },
            ["24-0"] = new string[] { "ARForest", "The Last Page ", "Howard_Y" },
            ["24-1"] = new string[] { "ETIA. feat.Jenga", "IKAROS ", "Amami.A.Myamsar" },
            ["24-2"] = new string[] { "Halv", "Tsukuyomi   月夜見 ", "HXJ_ConveX HXJ_ConveX SugarSu feat.RavE" },
            ["24-3"] = new string[] { "Qutabire", "Future Stream ", "Howard_Y" },
            ["24-4"] = new string[] { "MYUKKE.", "FULi AUTO SHOOTER ", "Howard_Y" },
            ["24-5"] = new string[] { "EBIMAYO", "GOODFORTUNE ", "Howard_Y" },
            ["25-0"] = new string[] { "砂糖协会", "tape/stop/night ", "Howard_Y" },
            ["25-1"] = new string[] { "Snail's House", "Pixel Galaxy ", "Howard_Y" },
            ["25-2"] = new string[] { "Moe Shop / TORIENA", "Notice ", "Howard_Y" },
            ["25-3"] = new string[] { "砂糖协会", "strawberry godzilla   <b>sᴛʀᴀᴡʙᴇʀʀʏ ɢᴏᴅᴢɪʟʟᴀ</b> ", "Howard_Y" },
            ["25-4"] = new string[] { "ディスコブラザーズ", "OKIMOCHI EXPRESSION ", "Howard_Y" },
            ["25-5"] = new string[] { "loopcoda", "Kimi to pool disco   君とpool disco", "Howard_Y" },
            ["26-0"] = new string[] { "sctl", "Legend of Eastern Rabbit -SKY DEFENDER-   東方兔傳說 -SKY DEFENDER- ", "Howard_Y" },
            ["26-1"] = new string[] { "Tanchiky", "ENERGY SYNERGY MATRIX ", "Howard_Y" },
            ["26-2"] = new string[] { "立秋 feat.ちょこ", "Punai Punai Genso ~Punai Punai in Wonderland~   プナイプナイげんそう ～プナイプナイinワンダーランド～ ", "- Team TERUYAMA -" },
            ["26-3"] = new string[] { "ルゼ", "Better Graphic Animation ", "Trixxxter Trixxxter Better Tate Renda" },
            ["26-4"] = new string[] { "M-UE", "Variant Cross ", "noodle" },
            ["26-5"] = new string[] { "翡乃イスカ vs. 梅干茶漬け", "Ultra Happy Miracle Bazoooooka!! ", "Howard_Y" },
            ["27-0"] = new string[] { "かめりあ feat.ななひら", "Can I friend you on Bassbook? lol   ベースラインやってる?笑 ", "SugarSuやってる?笑" },
            ["27-1"] = new string[] { "かめりあ feat.ななひら", "Gaming☆Everything   ゲーミング☆Everything ", "Howard_Y☆Everything" },
            ["27-2"] = new string[] { "かめりあ feat.ななひら", "Renji de haochi☆Denshi choriki shiyou chuka ryori 4000nen rekishi shunkan chori kanryo butouteki ryoricho☆   レンジで好吃 ☆電子調理器使用中華料理四千年歴史瞬間調理完了武闘的料理長☆ ", "money钱で好吃" },
            ["27-3"] = new string[] { "かめりあ feat.ななひら", "You Make My Life 1UP ", "Howard_Y Makes Your Life 1UP" },
            ["27-4"] = new string[] { "かめりあ feat.ななひら", "Newbies take 3 years, geeks 8 years, and internets are forever   ニワカ三年オタ八年、インターネッツはforever ", "ATKとHXJはforever" },
            ["27-5"] = new string[] { "ARM×狐夢想 feat.ななひら", "Onegai!Kon kon Oinarisama   お願い！コンコンお稲荷さま ", "お願い！コンコンHoward_Yさま" },
            ["28-0"] = new string[] { "ヒゲドライバー", "Heisha Onsha   弊社御社 ", "弊社のHoward_Y" },
            ["28-1"] = new string[] { "MYUKKE.", "Ginevra ", "Howard_Y Howard_Y Howard_Y Howard_Y's Avatar" },
            ["28-2"] = new string[] { "Nego_tiator", "Paracelestia ", "無極Rayix" },
            ["28-3"] = new string[] { "モリモリあつし", "un secret ", "money钱" },
            ["28-4"] = new string[] { "litmus*", "Good Life ", "Laddie Amoyensis" },
            ["28-5"] = new string[] { "テヅカ × Qayo", "ニニ-nini-   ニニ   ニニ-邇々-", "八咫鏡 八尺瓊勾玉 天叢雲剣" },
            ["29-0"] = new string[] { "COSIO (ZUNTATA) feat. ChouCho", "Groove Prayer ", "Welcome to GROOVE COASTER ! Let' s GROOVE ! Anytime, anywhere, GROOVE !" },
            ["29-1"] = new string[] { "COSIO(ZUNTATA)", "FUJIN Rumble ", "烈風のHoward 烈風のHoward 烈風のHoward 風神「Howard_Y」" },
            ["29-2"] = new string[] { "t+pazolite", "Marry me, Nightmare ", "Marry me, Howard_Y Marry me, Howard_Y Marry me, Trixxxter" },
            ["29-3"] = new string[] { "IOSYS TRAX (D.watt with さきぴょ)", "HG Magical Modified Polyvinyl Boy   HG魔改造ポリビニル少年", "HXJ_ConveX HXJ_ConveX HXJ_ConveX HXJ魔改造コンベックス少年" },
            ["29-4"] = new string[] { "世阿弥 vs Tatsh", "Saint's Breath   聖者の息吹", "money钱の鼓動 money钱の息吹 money钱の覚醒" },
            ["29-5"] = new string[] { "Cranky VS MASAKI", "ouroboros -twin stroke of the end- ", "ヾ(⌒(ﾉ'ω')ﾉ Howard_Y！！ ヾ(⌒(ﾉ'ω')ﾉ Howard_Y！！ ヾ(⌒(ﾉ'ω')ﾉ Howard_Y！！ 終焉の双撃を放ちし者「Howard_Y」" },
            ["30-0"] = new string[] { "PSYQUI feat. Marpril", "Girly Cupid ", "baozale" },
            ["30-1"] = new string[] { "Marpril", "sheep in the light ", "馬" },
            ["30-2"] = new string[] { "HAMA feat. Marpril", "Breaker city   ブレーカーシティ ", "Howard_Y" },
            ["30-3"] = new string[] { "memex", "heterodoxy ", "Howard_Y" },
            ["30-4"] = new string[] { "ミディ", "Computer Music Girl   コンピューターミュージックガール ", "money钱" },
            ["30-5"] = new string[] { "ANK", "Focus feat. Sayaki Rotor   焦点 feat.早木旋子   焦點 feat.早木旋子", "Howard_Y" },
            ["31-0"] = new string[] { "MYUKKE.", "The 90's Decision ", "Howard_Y" },
            ["31-1"] = new string[] { "HiTECH NINJA", "Medusa ", "Howard_Y" },
            ["31-2"] = new string[] { "Lime", "Final Step! ", "Howard_Y" },
            ["31-3"] = new string[] { "EmoCosine", "MAGENTA POTION ", "HXJ_ConveX" },
            ["31-4"] = new string[] { "HyuN", "Cross†Ray (feat. Lia under the Moon)   Cross†Ray (feat. 月下Lia)", "ShiroN_L" },
            ["31-5"] = new string[] { "ミツキ", "Square Lake ", "Zpm feat.RavE Zpm feat.RavE Zpm feat.RavE HXJ_ConveX" },
            ["32-0"] = new string[] { "水夏える", "Preparara   プレパララ ", "Phizer" },
            ["32-1"] = new string[] { "水夏える", "Whatcha;Whatcha Doin' ", "默蓝琉璃" },
            ["32-2"] = new string[] { "Fumihisa Tanaka", "Madara   斑 ", "Callisto" },
            ["32-3"] = new string[] { "水夏える", "pICARESq ", "Howard_Y" },
            ["32-4"] = new string[] { "水夏える", "Desastre ", "money钱" },
            ["32-5"] = new string[] { "水夏える", "Shoot for the Moon ", "Howard_Y" },
            ["33-0"] = new string[] { "Nikki Simmons", "Fireflies (Funk Fiction remix) ", "Howard_Y" },
            ["33-1"] = new string[] { "onoken feat. moco", "Light up my love!! ", "SugarSu" },
            ["33-2"] = new string[] { "3R2 as DJ Mashiro", "Happiness Breeze ", "yhzzzll yhzzzll yhzzzll ATK & FHCY" },
            ["33-3"] = new string[] { "t+pazolite", "Chrome VOX ", "Howard_Y" },
            ["33-4"] = new string[] { "Æsir", "CHAOS ", "Howard_Y" },
            ["33-5"] = new string[] { "Rabpit", "Saika ", "Laddie Amoyensis" },
            ["33-6"] = new string[] { "3R2", "Standby for Action ", "Ignition/Set/Go!" },
            ["33-7"] = new string[] { "Tatsh", "Hydrangea ", "Howard_Y" },
            ["33-8"] = new string[] { "Yesod (litmus* + Rabbit House + A. Ki)", "Amenemhat ", "HXJ_ConveX" },
            ["33-9"] = new string[] { "yuritoworks.", "Santouka   三灯火 ", "Howard_Y" },
            ["33-10"] = new string[] { "TEZUKApo11o band ft.ランコ(豚乙女)+OFFICE萬田㈱", "HEXENNACHTROCK-katashihaya-   「妖怪録、我し来にけり。」 ", "money钱" },
            ["33-11"] = new string[] { "HiTECH NINJA", "Blah!! ", "Howard_Y" },
            ["33-12"] = new string[] { "Æsir", "CHAOS (Glitch) ", "Howard_Y" },
            ["34-0"] = new string[] { "REDALiCE", "ALiVE ", "PEROPERO Chart Team \"HXJ_ConveX\"" },
            ["34-1"] = new string[] { "TANO*C Sound Team", "BATTLE NO.1 ", "PEROPERO Chart Team \"HXJ_ConveX\" PEROPERO Chart Team \"HXJ_ConveX\" PEROPERO Chart Team?! PEROPERO Chart Team without \"HXJ_ConveX\"" },
            ["34-2"] = new string[] { "USAO", "Cthugha ", "PEROPERO Chart Team \"money钱\" PEROPERO Chart Team \"money钱\" PEROPERO Chart Team \"money钱\" money \"The Burning One\"" },
            ["34-3"] = new string[] { "P*Light", "TWINKLE★MAGIC ", "PEROPERO Chart Team \"Y*Howard\" PEROPERO Chart Team \"P*Hizer\" PEROPERO Chart Team \"P*Hizer\" PEROPERO Chart Team \"Y*Howard\"" },
            ["34-4"] = new string[] { "DJ Noriken & aran", "Comet Coaster ", "PEROPERO Chart Team \"Howard_Y\" PEROPERO Chart Team \"Howard_Y\" PEROPERO Chart Team \"Howard_Y\" #PRiNC3_OF_CHVRTZ" },
            ["34-5"] = new string[] { "DJ Myosuke & Gram", "XODUS ", "PEROPERO Chart Team \"Howard_Y\" PEROPERO Chart Team \"Howard_Y\" PEROPERO Chart Team \"Howard_Y\" catastropHY." },
            ["35-0"] = new string[] { "立秋 feat.ちょこ", "PeroPeroGames, the creator of MuseDash, went bankrupt~   MuseDashを作っているPeroPeroGamesさんが倒産しちゃったよ～", "HXJはまだ給料もらってないよ〜" },
            ["35-1"] = new string[] { "LeaF", "MARENOL ", "HOWARDY 1mg" },
            ["35-2"] = new string[] { "老爷", "My Japanese style is really good   僕の和風本当上手   僕の和风本当上手", "僕の古筝本当上手 僕の三弦本当上手 僕の唢呐本当上手" },
            ["35-3"] = new string[] { "iKz", "Rush B ", "money钱 (Silver I) money钱 (Silver II) money钱 (Silver more than II but less than IV)" },
            ["35-4"] = new string[] { "Cosmograph", "DataErr0r ", "Howard_Y Howard_Y Howard_Y [!!NODATA!!]" },
            ["35-5"] = new string[] { "NceS", "Burn ", "您 您 您" },
            ["36-0"] = new string[] { "わかどり", "NightTheater ", "Howard_Y" },
            ["36-1"] = new string[] { "EmoCosine", "Cutter ", "Howard_Y" },
            ["36-2"] = new string[] { "立秋 feat.ちょこ", "bamboo   竹 ", "money钱 money钱 money钱 Howard_Y" },
            ["36-3"] = new string[] { "linear ring", "enchanted love ", "money钱 money钱 ForeverFXS" },
            ["36-4"] = new string[] { "Aoi", "c.s.q.n. ", "Howard_Y" },
            ["36-5"] = new string[] { "翡乃イスカ vs. s-don", "Booouncing!! ", "HXJ_ConveX" },
            ["37-0"] = new string[] { "Nekock·LK", "Glass Color Prelude   琉璃色前奏曲", "Howard_Y" },
            ["37-1"] = new string[] { "TEMPLIME feat 星宫とと", "Neonlights   ネオンライト ", "money钱 & Howard_Y" },
            ["37-2"] = new string[] { "Sound piercer feat.DAZBEE", "Hope for the flowers   花たちに希望を ", "Howard_Y" },
            ["37-3"] = new string[] { "dawn-system feat.おーくん", "Seaside Cycling on May 30   5月30日、自転車日和 ", "money钱 money钱 ForeverFXS" },
            ["37-4"] = new string[] { "P4koo(feat.rerone)", "SKY↑HIGH ", "HXJ_ConveX HXJ_ConveX YuzukiY" },
            ["37-5"] = new string[] { "Ponchi♪ feat.はぁち", "Mousou Chu!!   妄想♡ちゅー!! ", "money钱 money钱 ShiroN_L" },
            ["38-0"] = new string[] { "MYUKKE.", "NO ONE YES MAN ", "Howard_Y" },
            ["38-1"] = new string[] { "A-39", "Snowfall, Merikuri (MD edit)   雪降り、メリクリ （MD edit）", "Howard_Y Howard_Y 無極Rayix vs Catcats" },
            ["38-2"] = new string[] { "Se-U-Ra", "Igallta ", "信仰の剣『生』 平川の剣『士』 夜明の剣『靈』 断罪の剣『神』" },
            ["39-0"] = new string[] { "砂糖协会", "The Days to Cut the Sea   去剪海的日子", "Howard_Y" },
            ["39-1"] = new string[] { "砂糖协会", "happy hour ", "HXJ_ConveX HXJ_ConveX happy thb" },
            ["39-2"] = new string[] { "天游 & 玛安娜", "Seikimatsu no Natsu   世紀末の夏   世紀末的夏天   世纪末的夏天 ", "Howard_Y" },
            ["39-3"] = new string[] { "nyankobrq & yaca feat. somunia", "twinkle night ", "money钱 money钱 ATK" },
            ["39-4"] = new string[] { "シオカラ（Haruka Toki + TEA）", "ARUYA HARERUYA   アルヤハレルヤ ", "Howard_Y" },
            ["39-5"] = new string[] { "fusq", "Blush (feat. MYLK) ", "ForeverFXS" },
            ["39-6"] = new string[] { "オリーブがある", "Naked Summer   裸のSummer", "money钱 money钱 Callisto" },
            ["39-7"] = new string[] { "オリーブがある", "BLESS ME(Samplingsource) ", "HXJ_ConveX HXJ_ConveX D丶R" },
            ["39-8"] = new string[] { "砂糖协会", "FM 17314 SUGAR RADIO ", "Howard_Y" },
            ["40-0"] = new string[] { "litmus*", "Rush-More ", "money钱 money钱 ForeverFXS" },
            ["40-1"] = new string[] { "アリスシャッハと魔法の楽団", "Kill My Fortune ", "Howard_Y" },
            ["40-2"] = new string[] { "かゆき feat.燈露", "Yosari Tsukibotaru Suminoborite   よさり 月蛍澄み昇りて ", "Howard_Y" },
            ["40-4"] = new string[] { "Street", "Hibari   雲雀   종다리 ", "money钱" },
            ["40-5"] = new string[] { "MYUKKE.", "OCCHOCO-REST-LESS ", "Howard_Y" },
            ["41-0"] = new string[] { "fizzd", "Super Battleworn Insomniac   超・東方不眠症   超·東方不眠夜   超·东方不眠夜 ", "Howard_Y" },
            ["41-1"] = new string[] { "Stinkbug & Coda, Original song(Artificial Intelligence Bomb) from: naruto", "Bomb-Sniffing Pomeranian   嗅炸彈的博美犬   嗅炸弹的博美犬 ", "HXJ_ConveX HXJ_ConveX yhzzzll" },
            ["41-2"] = new string[] { "Vince Kaichan", "Rollerdisco Rumble   ロールディスコ 鼓動   롤러 디스코 럼블   輪滑迪斯科震顫   轮滑迪斯科震颤 ", "money钱 money钱 HikaHolic" },
            ["41-3"] = new string[] { "RoDy.cOde", "Rose Garden   玫瑰花園   玫瑰花园 ", "ForeverFXS" },
            ["41-4"] = new string[] { "Ras", "EMOMOMO ", "HXJ(thinking)" },
            ["41-5"] = new string[] { "Yooh", "Heracles   赫拉克勒斯 ", "Howard_Y" },
            ["42-0"] = new string[] { "Alstroemeria Records", "Bad Apple!! feat. Nomico ", "Howard_Y" },
            ["42-1"] = new string[] { "幽閉サテライト", "The color scatters through the smell   色は匂へど散りぬるを", "HXJ_ConveX HXJ_ConveX HXJ_ConveX  超妖怪弾頭" },
            ["42-2"] = new string[] { "ARM＋夕野ヨシミ (IOSYS) feat. miko", "Cirno's Perfect Math Class   チルノのパーフェクトさんすう教室 ", "1! d(≧▽≦d) 2!! (b≧▽≦)b ⑨!!!!!!!!! Σ(°Д °;)  氷の小さな妖精" },
            ["42-3"] = new string[] { "EastNewSound", "Under the Scarlet Moon, Mad Bloom's Severance   緋色月下、狂咲ノ絶", "Howard_Y Howard_Y Phizer  Howard_Y" },
            ["42-4"] = new string[] { "Yonder Voice", "Flowery Moonlit Night   花月夜 ", "money钱 money钱 Ctymax  money钱" },
            ["42-5"] = new string[] { "森羅万象", "Unconscious Requiem   無意識レクイエム ", "money钱" },
            ["43-0"] = new string[] { "3R2", "The Happycore Idol ", "money钱" },
            ["43-1"] = new string[] { "削除", "Amatsumikaboshi   天津甕星 ", "Howard_Y" },
            ["43-2"] = new string[] { "MYUKKE.", "ARIGA THESIS ", "Howard_Y" },
            ["43-3"] = new string[] { "ビートまりお", "Night of Knights   ナイト・オブ・ナイツ", "Howard_Y" },
            ["43-4"] = new string[] { "uma with モリモリあつし feat. ましろ", "#Psychedelic_Meguro_River   #サイケデリック目黒川 ", "Howard_Y" },
            ["43-5"] = new string[] { "MK", "can you feel it ", "money钱 money钱 無極Rayix Beautiful Girl" },
            ["43-6"] = new string[] { "mossari feat.TEA", "Midnight O'clock ", "Howard_Y" },
            ["43-7"] = new string[] { "a_hisa", "Rin   燐 ", "ForeverFXS" },
            ["43-8"] = new string[] { "onoken", "Smile-mileS ", "Smile-fxS" },
            ["43-9"] = new string[] { "他人事 feat. 否", "Believing and Being   信仰と存在   신앙과 존재   信仰與存在   信仰与存在 ", "Howard_Y" },
            ["43-10"] = new string[] { "P4koo(feat.つゆり花鈴)", "catalyst   カタリスト", "ForeverFXS" },
            ["43-11"] = new string[] { "iKz feat.Bunny Girl Rin", "don't！stop！eroero！ ", "money钱" },
            ["43-12"] = new string[] { "ころねぽち With 立秋", "pa pi pu pi pu pi pa   ぱぴぷぴぷぴぱ ", "Villager With ForeverFXS Villager With ForeverFXS Sraimi With ShiroN_L" },
            ["43-13"] = new string[] { "a_hisa", "Sand Maze ", "無極魔王の迷宮" },
            ["43-15"] = new string[] { "吐息．", "AKUMU / feat.tug   悪夢 / feat.つぐ ", "money钱" },
            ["43-16"] = new string[] { "LeaF", "Queen Aluett   Queen Aluett -女王アルエット- ", "Howard_Y" },
            ["43-17"] = new string[] { "Zekk, poplavor", "DROPS (feat. Such) ", "HXJ_ConveX" },
            ["43-18"] = new string[] { "Halozy", "The crazy Fran-chan is a great song   物凄い狂っとるフランちゃんが物凄いうた", "Howard_Y" },
            ["43-19"] = new string[] { "wotaku feat. SHIKI", "snooze ", "HXJ_ConveX" },
            ["43-20"] = new string[] { "Neko Hacker", "Kuishinbo Hacker feat.Kuishinbo Akachan   くいしんぼハッカー feat.くいしんぼあかちゃん ", "Hacker_FXS" },
            ["43-21"] = new string[] { "Aiobahn feat.Nanahira", "Inu no outa   いぬのおうた   犬之歌 ", "SugarSu" },
            ["43-22"] = new string[] { "t+pazolite", "Prism Fountain ", "ForeverFXS" },
            ["44-0"] = new string[] { "t+pazolite", "Party in the HOLLOWood feat. ななひら", "ForeverFXS" },
            ["44-1"] = new string[] { "iKz feat.祖娅纳惜", "Big Battle   嘤嘤大作战   嚶嚶大作戰", "money钱钱" },
            ["44-2"] = new string[] { "brz1128", "Howlin' Pumpkin ", "Howard_Y" },
            ["45-0"] = new string[] { "t+pazolite feat. ななひら", "ONOMATO Pairing!!! ", "ForeverFXS" },
            ["45-1"] = new string[] { "t+pazolite & Massive New Krew feat. リリィ(CV青木志貴)", "with U ", "HXJ_ConveX HXJ_ConveX HXJ_ConveX 無極Rayix" },
            ["45-2"] = new string[] { "USAO", "Chariot ", "Howard_Y" },
            ["45-3"] = new string[] { "onoken", "GASHATT ", "money钱" },
            ["45-4"] = new string[] { "sasakure.UK", "LIN NE KRO NE feat. lasah ", "Howard_Y" },
            ["45-5"] = new string[] { "REDALiCE & cosMo@暴走P", "ANGEL HALO   天使光輪 ", "Howard_Y" },
            ["46-0"] = new string[] { "BOOGEY VOXX", "Bang!! ", "ForeverFXS" },
            ["46-1"] = new string[] { "Sound Souler", "Paradise Ⅱ ", "Howard_Y" },
            ["46-2"] = new string[] { "Silaver", "Symbol ", "money钱 money钱 Ctymax" },
            ["46-3"] = new string[] { "EmoCosine", "Nekojarashi   ネコジャラシ ", "money钱" },
            ["46-4"] = new string[] { "tezuka x Yunosuke", "A Philosophical Wanderer ", "HXJ_ConveX" },
            ["46-5"] = new string[] { "Zris", "Isouten   異想天   异想天 ", "Howard_Y" },
            ["47-0"] = new string[] { "karatoPαnchii feat.はるの", "Haze of Autumn   秋の陽炎 ", "money钱" },
            ["47-1"] = new string[] { "C-Show", "GIMME DA BLOOD ", "HXJ_ConveX HXJ_ConveX 無極Rayix with RavE" },
            ["47-2"] = new string[] { "Zekk", "Libertas ", "HXJ_ConveX" },
            ["47-3"] = new string[] { "USAO", "Cyaegha ", "Howard_Y" },
            ["48-0"] = new string[] { "BEXTER x Mycin.T", "glory day ", "ForeverFXS" },
            ["48-1"] = new string[] { "M2U", "Bright Dream ", "ForeverFXS ForeverFXS yhzzzll" },
            ["48-2"] = new string[] { "Mycin.T", "Groovin Up ", "HXJ_ConveX HXJ_ConveX ATK" },
            ["48-3"] = new string[] { "Lin-G", "I Want You ", "money钱" },
            ["48-4"] = new string[] { "ESTi", "OBLIVION ", "ForeverFXS" },
            ["48-5"] = new string[] { "Forte Escape", "Elastic STAR ", "無極Rayix 無極Rayix Beautiful Girl" },
            ["48-6"] = new string[] { "HAYAKO", "U.A.D ", "Howard_Y" },
            ["48-7"] = new string[] { "3rd Coast", "Jealousy ", "money钱 money钱 Ctymax" },
            ["48-8"] = new string[] { "M2U", "Memory of Beach ", "money钱" },
            ["48-9"] = new string[] { "Paul Bazooka", "Don't Die ", "Howard_Y" },
            ["48-10"] = new string[] { "ND Lee", "Y (CE Ver.) ", "HXJ_ConveX" },
            ["48-11"] = new string[] { "SiNA x CHUCK", "Fancy Night ", "HXJ_ConveX HXJ_ConveX SugarSu" },
            ["48-12"] = new string[] { "Forte Escape", "Can We Talk ", "money钱 money钱 FH+CY" },
            ["48-13"] = new string[] { "ND Lee", "Give Me 5 ", "ForeverFXS ForeverFXS ShiroN_L" },
            ["48-14"] = new string[] { "M2U", "Nightmare ", "Howard_Y" },
            ["49-0"] = new string[] { "BOOGEY VOXX", "Pray a LOVE ", "HXJ_ConveX" },
            ["49-1"] = new string[] { "Matthiola Records", "Romantic avoidance addiction   恋愛回避依存症", "money钱" },
            ["49-2"] = new string[] { "Neko Hacker", "Daisuki Dayo feat.Wotoha   だーいすきだよ feat. をとは ", "ForeverFXS" },
            ["50-0"] = new string[] { "daniwell", "Nyan Cat ", "Nyan HXJ" },
            ["50-1"] = new string[] { "立秋 feat.ちょこ", "PeroPero in the Universe   ペロペロ in the Universe ", "宇宙の無極" },
            ["50-2"] = new string[] { "ヒゲドライバー", "In-kya Yo-kya Onmyoji   陰キャ陽キャ陰陽師 ", "陰キャNEKO 陰キャNEKO 陰キャDDR" },
            ["50-3"] = new string[] { "t+pazolite", "KABOOOOOM!!!! ", "WACOOOOOW!!!!" },
            ["50-4"] = new string[] { "LeaF", "Doppelganger ", "Howard_Y's Shadow Howard_Y's Avatar Howard_Y's Darkside Howard_Y's Doppelganger" },
            ["51-0"] = new string[] { "小野道ono feat.早木旋子", "Masked Diary   假面日记   仮面日記   假面日記", "Howard_Y" },
            ["51-1"] = new string[] { "technoplanet feat. 天輝おこめ (from KAWAII MUSIC)", "Reminiscence ", "money钱" },
            ["51-2"] = new string[] { "Project Mons feat.胡桃Usa", "DarakuDatenshi   ダラクダテンシ   墮墮天使   堕堕天使 ", "堕堕無極" },
            ["51-3"] = new string[] { "BOOGEY VOXX", "D.I.Y. ", "D.R.Y." },
            ["51-4"] = new string[] { "IOSYS", "Boys in ☆ Virtual Land   男子in☆バーチャランド", "HXJ_ConveX" },
            ["51-5"] = new string[] { "Apo11o program ft.ドーラ（にじさんじ）", "kuí   虁 ", "ForeverFXS" },
            ["52-0"] = new string[] { "砂糖协会", "marooned night   夜的擱淺   夜的搁浅 ", "ForeverFXS" },
            ["52-2"] = new string[] { "オリーブがある with 砂糖协会", "Ornamentじゃない(Muse Dash Mix)", "money钱" },
            ["52-3"] = new string[] { "Moe Shop", "Baby Pink (w/ YUC'e) ", "HXJ_ConveX" },
            ["52-4"] = new string[] { "Aiobahn", "I'm Here   ここにいる   ここにいる (I'm Here) ", "Howard_Y" },
            ["53-0"] = new string[] { "ETIA. feat. Jenga", "On And On!! ", "Howard_Y" },
            ["53-1"] = new string[] { "FOIV feat.Tomin Yukino", "Trip! ", "money钱 money钱 公大笑乐" },
            ["53-2"] = new string[] { "ああああ", "Hoshi no otoshimono   ほしのおとしもの ", "ForeverFXS" },
            ["53-3"] = new string[] { "よみぃ", "Plucky Race ", "money钱" },
            ["53-4"] = new string[] { "PYKAMIA", "Fantasia Sonata Destiny ", "無感のD 壓製のR 激昂のSHADOW" },
            ["53-5"] = new string[] { "Ryo Arue", "Run through ", "HXJ_ConveX" },
            ["54-0"] = new string[] { "rejection", "White Canvas (feat. 藍月なくる)", "money钱" },
            ["54-1"] = new string[] { "Zekk & poplavor", "Gloomy Flash (feat. Mami) ", "ForeverFXS" },
            ["54-2"] = new string[] { "kazeoff", "Find this month's featured playlists   今月のおすすめプレイリストを検索します", "Marathon Committee" },
            ["54-3"] = new string[] { "Mameyudoufu", "Sunday Night (feat. Kanata.N) ", "ShiroN_L" },
            ["54-4"] = new string[] { "rejection", "Goodbye Goodnight (feat. Shully) ", "HXJ_ConveX HXJ_ConveX Sya" },
            ["54-5"] = new string[] { "Mameyudoufu", "ENDLESS CIDER (feat. Such) ", "ForeverFXS ForeverFXS GRACE" },
            ["55-0"] = new string[] { "幽閉サテライト", "Moon, Murakumo Flower and Wind   月に叢雲華に風", "money钱 money钱 money钱  祀られる風の人間" },
            ["55-1"] = new string[] { "minami＋七条レタス(IOSYS) feat. 岩杉夏", "Patchouli's - Best Hit GSK   パチュリーズ・ベストヒットGSK ", "money钱 money钱 money钱  動かない大図書館" },
            ["55-2"] = new string[] { "Halozy", "Koishi sings a terrible song on a terrible space shuttle   物凄いスペースシャトルでこいしが物凄いうた", "ForeverFXS ForeverFXS villager feat. FXS  閉じた恋の瞳" },
            ["55-3"] = new string[] { "豚乙女", "A world without enclosures is a moonlight   囲い無き世は一期の月影", "ForeverFXS ForeverFXS yhzzzll  永遠と須臾の罪人" },
            ["55-4"] = new string[] { "SOUND HOLIC feat. Nana Takahashi", "Psychedelic Kizakura Doumei   サイケデリック鬼桜同盟 ", "D丶R D丶R D丶R  普通の魔法使い" },
            ["55-5"] = new string[] { "森羅万象", "Mischievous Sensation   悪戯センセーション ", "ForeverFXS ForeverFXS villager feat. FXS  永遠に紅い幼き月 vs. 悪魔の妹" },
            ["56-0"] = new string[] { "t+pazolite", "Psyched Fevereiro ", "D丶R" },
            ["56-1"] = new string[] { "Ponchi♪ feat.はぁち", "Inferno City   インフェルノシティ ", "ShiroN_L ShiroN_L KuroN_L" },
            ["56-2"] = new string[] { "モリモリあつし", "Paradigm Shift ", "ForeverFXS ForeverFXS F_Eternal feat. FXS" },
            ["56-3"] = new string[] { "NeLiME", "Snapdragon ", "money钱" },
            ["56-4"] = new string[] { "ikaruga_nex & 影虎。", "Prestige and Vestige ", "Howard_Y" },
            ["56-5"] = new string[] { "Capchii", "Tiny Fate ", "SugarSu" },
            ["57-0"] = new string[] { "TRIFLATS", "Tokimeki★Meteostrike   ときめき★メテオストライク ", "NekoSraimi NekoSraimi GiNER_Yang" },
            ["57-1"] = new string[] { "James Landino ft. Raj Ramayya", "Down Low ", "money钱" },
            ["57-2"] = new string[] { "NOMA", "LOUDER MACHINE ", "EE-ᴇᴇᴇᴇᴇᴇᴇᴇᴇᴇ AA-ᴀᴀᴀᴀᴀᴀᴀᴀᴀᴀ OO-oooooooooo" },
            ["57-3"] = new string[] { "タケノコ少年 feat. 周央サンゴ（にじさんじ）", "That's another Rabuchu   それはもうらぶちゅ", "Howard_Y" },
            ["57-4"] = new string[] { "3R2", "Rave_Tech ", "RavE RavE RavE with HXJ" },
            ["57-5"] = new string[] { "Halv", "Brilliant & Shining! (Game Edit.) ", "ForeverFXS" },
            ["58-0"] = new string[] { "Neko Hacker feat. Nanahira", "People People   ピポピポ -People People ", "ForeverFXS ForeverFXS villager feat. FXS Villager Hacker feat. FXS" },
            ["58-1"] = new string[] { "Neko Hacker feat. Nanahira", "Endless Error Loop ", "▒▒▒▒▒▓▓ 烫烫烫烫烫烫烫烫烫烫烫 锟斤拷锟斤拷锟斤拷锟斤拷" },
            ["58-2"] = new string[] { "Camellia feat. Nanahira", "Forbidden Pizza!   フォビどぅん・ピザ！ ", "HXJ_ConveX" },
            ["58-3"] = new string[] { "t+pazolite feat. Nanahira", "Don't mess with the vocals   |ボーカルに無茶させんな", "Howard_Y" },
            ["59-0"] = new string[] { "塞壬唱片-MSR", "Boiling Blood ", "ForeverFXS" },
            ["59-1"] = new string[] { "塞壬唱片-MSR", "ManiFesto： ", "Howard_Y" },
            ["59-2"] = new string[] { "塞壬唱片-MSR", "Operation Blade ", "money钱" },
            ["59-3"] = new string[] { "塞壬唱片-MSR", "Radiant ", "money钱" },
            ["59-4"] = new string[] { "塞壬唱片-MSR", "Renegade ", "ForeverFXS" },
            ["59-5"] = new string[] { "塞壬唱片-MSR", "Speed of Light ", "HXJ_ConveX" },
            ["59-6"] = new string[] { "塞壬唱片-MSR", "Dossoles Holiday   多索雷斯假日 ", "Ch'en the Holungday" },
            ["59-7"] = new string[] { "塞壬唱片-MSR", "Autumn Moods   秋绪 ", "Dr.ShiroN_L Dr.ShiroN_L Croissant&Angelina and the Sweet Autumn" },
            ["60-0"] = new string[] { "C-Show", "N3V3R G3T OV3R ", "HXJ_ConveX" },
            ["60-1"] = new string[] { "t+pazolite", "Oshama Scramble! ", "ForeverFXS" },
            ["60-2"] = new string[] { "owl＊tree feat. chi＊tree", "Valsqotch ", "SugarSu" },
            ["60-3"] = new string[] { "ナユタン星人", "Paranormal My Mind   超常マイマイン ", "Vignette☆" },
            ["60-4"] = new string[] { "kanone feat. せんざい", "Flower, snow and Drum'n'bass.   花と、雪と、ドラムンベース。 ", "元と、首と、ハワード・ワイ。" },
            ["60-5"] = new string[] { "削除", "Amenohoakari   天火明命 ", "D丶R" },
            ["61-0"] = new string[] { "立秋", "MuseDash How many times?   |MuseDashヵヽﾞ何ヵヽ干∋ッ`⊂ぉヵヽＵ＜ﾅょッﾅﾆ気ヵヽﾞ￡ゑょ", "♂圣ヵ壩鐠||帀②亾蒩→" },
            ["61-1"] = new string[] { "LeaF", "Aleph-0 ", "2^ℵ0=ℵ1" },
            ["61-2"] = new string[] { "ななひら&ころねぽち", "Buttoba Super Nova   |ぶっとばスーパーノヴァ", "「永遠のスーパーノヴァ」" },
            ["61-3"] = new string[] { "litmus*", "Rush-Hour ", "SGFwcHkgQX ByaWwgRm 9vbCdzIERhe SwgQmFiYWJh" },
            ["61-4"] = new string[] { "Sound Souler", "3rd Avenue ", "money钱 money钱 マ〇ニ" },
            ["61-5"] = new string[] { "EBIMAYO", "WORLDINVADER ", "#×]~（0^/v3><" },
            ["0-a"] = new string[] { "Heart-Pounding Flight " },
            ["40-3"] = new string[] { "JUMP! HardCandy ", "money钱 money钱 silentgd/MLLL" },
            ["43-14"] = new string[] { "Diffraction ", "HXJ_ConveX HXJ_ConveX 默蓝琉璃" },
            ["52-1"] = new string[] { "daydream girl ", "Howard_Y" }
        };
    }
}
