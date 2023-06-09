using System.Net;
using ArgumentException = System.ArgumentException;
using System;
using System.IO;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using Assets.Scripts.PeroTools.Nice.Actions;
using Assets.Scripts.PeroTools.Nice.Interface;
using System.Linq;
using PeroPeroGames;
using Assets.Scripts.Database;
using CustomAlbums;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Ionic.Zip;

namespace SearchPlusPlus
{
    public static class Utils
    {
        internal const string Separator = "--------------------------------";

        internal static readonly List<int> DifficultyResultAll = Enumerable.Range(1,5).ToList();
        internal static readonly List<int> DifficultyResultEmpty = new List<int>();
        public static Il2CppSystem.Collections.Generic.List<T> IL_List<T>(params T[] args)
        {
            var list = new Il2CppSystem.Collections.Generic.List<T>();
            if (args != null)
            {
                foreach (var item in args)
                {
                    list.Add(item);
                }
            }
            return list;
        }
        public static T GetResult<T>(this IVariable data)
        {
             return VariableUtils.GetResult<T>(data);
        }

        public static T GetResultOrDefault<T>(this IVariable data)
        {
            try
            {
                return VariableUtils.GetResult<T>(data);
            }
            catch (Exception)
            {
                try
                {
                    return (T)(object)((double)(object)default - 1);
                }
                catch (Exception)
                {
                    return default;
                }
            }
        }

        public static Dictionary<string, object> ScoresToObjects(this IData data)
        {

            var dict = new Dictionary<string, object>
            {
                ["uid"] = data.fields["uid"].GetResultOrDefault<string>(),
                ["evaluate"] = data.fields["evaluate"].GetResultOrDefault<int>(),
                ["score"] = data.fields["score"].GetResultOrDefault<int>(),
                ["combo"] = data.fields["combo"].GetResultOrDefault<int>(),
                ["clear"] = data.fields["clear"].GetResultOrDefault<int>(),
                ["accuracyStr"] = data.fields["accuracyStr"].GetResultOrDefault<string>(),
                ["accuracy"] = data.fields["accuracy"].GetResultOrDefault<float>()
            };
            return dict;
        }

        public static List<T> ToSystem<T>(this Il2CppSystem.Collections.Generic.List<T> cpList)
        {
            if (cpList == null)
            {
                return null;
            }
            var list = new List<T>();
            foreach (var item in cpList)
            {
                list.Add(item);
            }
            return list;
        }

        public static Dictionary<TKey, TValue> ToSystem<TKey, TValue>(this Il2CppSystem.Collections.Generic.Dictionary<TKey, TValue> cpDict)
        {
            if (cpDict == null)
            {
                return null;
            }
            var dict = new Dictionary<TKey, TValue>();
            foreach (var item in cpDict)
            {
                dict[item.Key] = item.Value;
            }
            return dict;
        }

        public static Il2CppSystem.Collections.Generic.Dictionary<TKey, TValue> IL_Dict<TKey, TValue>(params (TKey Key, TValue Value)[] args)
        {
            var dict = new Il2CppSystem.Collections.Generic.Dictionary<TKey, TValue>();
            if (args != null)
            {
                foreach (var item in args)
                {
                    if (dict.ContainsKey(item.Key))
                    {
                        throw new ArgumentException("duplicated key while initalizing dictionary");
                    }
                    dict[item.Key] = item.Value;
                }
            }
            return dict;
        }


        internal static byte[] GetRequestBytes(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.KeepAlive = false;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            {
                return ReadFully(stream);
            }

        }

        public static byte[] ReadFully(Stream stream, int initialLength = 0)
        {
            if (initialLength < 1)
            {
                initialLength = 32768;
            }

            byte[] buffer = new byte[initialLength];
            int read = 0;

            int chunk;
            while ((chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
            {
                read += chunk;

                if (read == buffer.Length)
                {
                    int nextByte = stream.ReadByte();
                    if (nextByte == -1)
                    {
                        return buffer;
                    }
                    byte[] newBuffer = new byte[buffer.Length * 2];
                    Array.Copy(buffer, newBuffer, buffer.Length);
                    newBuffer[read] = (byte)nextByte;
                    buffer = newBuffer;
                    read++;
                }
            }
            byte[] ret = new byte[read];
            Array.Copy(buffer, ret, read);
            return ret;
        }


        internal static string RequestToString(HttpWebRequest request)
        {
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        internal static HttpWebRequest GetRequestInstance(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.KeepAlive = false;
            request.CookieContainer = null;
            request.PreAuthenticate = false;
            request.ServicePoint.UseNagleAlgorithm = false;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            return request;
        }

        internal static HttpWebRequest GetRequestInstance(string uri, DateTime dt)
        {
            var request = GetRequestInstance(uri);
            request.IfModifiedSince = dt;
            return request;
        }


        internal static Stream StringToStream(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream, Encoding.ASCII);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        internal static readonly Regex regexBPM = new Regex(@"^[0-9]*\.[0-9]+[^0-9.][0-9]*\.[0-9]+$");

        internal static readonly Regex regexNonNumeric = new Regex(@"[^0-9.]");
        public static bool DetectParseBPM(string input, out double start, out double end, double min, double max)
        {
            start = end = double.NaN;
            input = input.Trim();
            if (!regexBPM.IsMatch(input))
            {
                return false;
            }
            return ParseRange(input.Replace(regexNonNumeric.Match(input).Value, "-"), out start, out end, min, max) ?? false;
        }
        public static bool DetectParseBPM(string input, out double start, out double end)
        {
            return DetectParseBPM(input, out start, out end, double.NegativeInfinity, double.PositiveInfinity);
        }
        public static string CreateMD5(string input)
        {
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
        internal static string GetRequestString(string uri)
        {
            return RequestToString(GetRequestInstance(uri));
        }
        internal static bool LowerContains(this PeroString peroString, string compareText, string containsText)
        {
            compareText = compareText ?? "";
            containsText = containsText ?? "";
            peroString.Clear();
            peroString.Append(compareText.ToLower());
            peroString.ToLower();
            return (peroString.Contains(containsText) || compareText.ToLower().Contains(containsText));
        }

        public static readonly char[] splitChars = new char[] { '-' };
        public static bool ParseRange(string expression, out double start, out double end)
        {
            return ParseRange(expression, out start, out end, double.NegativeInfinity, double.PositiveInfinity) ?? false;
        }
        public static bool? ParseRange(string expression, out double start, out double end, double min, double max)
        {
            start = double.NaN;
            end = double.NaN;
            if (string.IsNullOrEmpty(expression))
            {
                return null;
            }
            if (min > max)
            {
                var swap = min;
                min = max;
                max = swap;
            }

            expression = expression.Trim(' ');
            if (expression == "*")
            {
                start = min;
                end = max;
                return true;
            }
            bool negateStart = expression.StartsWith("-");
            if (negateStart)
            {
                expression = expression.Substring(1);
            }

            if (expression.EndsWith("+"))
            {
                if (!double.TryParse(expression.Substring(0, expression.Length - 1), out double x))
                {
                    return null;
                }
                start = x;
                end = max;
                if (negateStart)
                {
                    start *= -1;
                }
            }
            else if (expression.EndsWith("-"))
            {

                if (!double.TryParse(expression.Substring(0, expression.Length-1), out double x))
                {
                    return null;
                }
                start = min;
                end = x;
                if (negateStart)
                {
                    end *= -1;
                }
            }
            else if (expression.Contains('-'))
            {
                var splitTerm = expression.Split(splitChars, 2);
                if (!double.TryParse(splitTerm[0], out start))
                {
                    return null;
                }
                if (!double.TryParse(splitTerm[1], out end))
                {
                    return null;
                }
                if (negateStart)
                {
                    start *= -1;
                }
            }
            else
            {
                if (!double.TryParse(expression, out double x))
                {
                    return null;
                }
                if (negateStart)
                {
                    x *= -1;
                }
                start = x;
                end = x;
            }
            if (start > end)
            {
                var swap = end;
                end = start;
                start = swap;
            }
            if (!(min <= end && end <= max) || !(min <= start && start <= max))
            {
                return false;
            }
            return true;
        }

        public static bool GetAvailableMaps(MusicInfo musicInfo, out HashSet<int> availableMaps)
        {
            return GetAvailableMaps(musicInfo, out availableMaps, out _);
        }

        public static bool GetAvailableMaps(MusicInfo musicInfo, out HashSet<int> availableMaps, out bool isCustom)
        {
            isCustom = BuiltIns.EvalCustom(musicInfo);
            if (isCustom)
            {
                availableMaps = AlbumManager.LoadedAlbumsByUid[musicInfo.uid].availableMaps.Select(x => x.Key).ToHashSet();
            }
            else
            {
                availableMaps = new HashSet<int>();
                for (int i = 1; i < 6; i++)
                {
                    var musicDiff = musicInfo.GetMusicLevelStringByDiff(i, false);
                    if (!(string.IsNullOrEmpty(musicDiff) || musicDiff == "0"))
                    {
                        availableMaps.Add(i);
                    }
                }
            }
            if (availableMaps.Count == 0)
            {
                return false;
            }
            return true;
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
