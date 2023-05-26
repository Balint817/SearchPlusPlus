using Assets.Scripts.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchPlusPlus
{

    internal class SearchTerm
    {
        internal string Key;
        internal SearchValue Value;
        internal Func<SearchValue, MusicInfo, List<int>> ValueParser;
    }

    internal class SearchValue
    {
        internal string Value;
    }

    internal abstract class SearchGroupAbstract: SearchValue
    {
        internal new List<SearchTerm> Value;
        internal abstract List<int> GetGroupResult(MusicInfo musicInfo);
    }

    internal class SearchGroupAnd: SearchGroupAbstract
    {
        internal override List<int> GetGroupResult(MusicInfo musicInfo)
        {
            IEnumerable<int> result = Utils.DifficultyResultAll;
            foreach (var item in Value)
            {
                var temp = item.ValueParser(item.Value, musicInfo);
                if (!temp.Any())
                {
                    return Utils.DifficultyResultEmpty;
                }
                result = result.Intersect(temp);
            }
            result.Append(-1);
            return result.ToList();
        }
    }

    internal class SearchGroupOr: SearchGroupAbstract
    {
        internal override List<int> GetGroupResult(MusicInfo musicInfo)
        {
            IEnumerable<int> result = Utils.DifficultyResultEmpty;
            bool tempBool = false;
            foreach (var item in Value)
            {

                var temp = item.ValueParser(item.Value, musicInfo);
                tempBool |= temp.Any();
                result = result.Union(temp);
            }
            return result.ToList();
        }

    }

    internal class SearchGroupStack: SearchGroupAnd
    {
        internal override List<int> GetGroupResult(MusicInfo musicInfo)
        {
            return base.GetGroupResult(musicInfo).Where(x => x != -1).ToList();
        }

    }

    internal class SearchCustom: SearchValue
    {
        internal new List<string> Value;

        internal static Dictionary<string, SearchGroupAnd> CustomTags = new Dictionary<string, SearchGroupAnd>();
        internal static Dictionary<string, int[][]> TagParameters = new Dictionary<string, int[][]>();

    }


}
