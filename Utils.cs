using System.Net;
using ArgumentException = System.ArgumentException;
using System;
using System.IO;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using Assets.Scripts.PeroTools.Nice.Actions;
using Assets.Scripts.PeroTools.Nice.Interface;

namespace SearchPlusPlus
{
    public static class Utils
    {
        internal const string Separator = "--------------------------------";
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
    }
}
