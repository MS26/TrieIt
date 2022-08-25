using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TrieIt.Indexes
{
    /*
TrieIt.Indexes.Original
Words: 370105
Took: 1,656 ms
Allocated: 396,792 kb
Peak Working Set: 182,520 kb
Gen 0 collections: 65
Gen 1 collections: 22
Gen 2 collections: 6
     */
    public class Original : IWordIndex
    {
        private readonly IDictionary<string, byte> _index;
        private readonly IDictionary<string, byte> _fullWordIndex;

        internal Original()
        {
            _index = new ConcurrentDictionary<string, byte>();
            _fullWordIndex = new ConcurrentDictionary<string, byte>();
        }
        
        public void Add(string word)
        {
            AddToFullWordIndex(word);
            
            for (var x = 1; x < word.Length; ++x)
            {
                AddToDictionary(word.Substring(0, x));
            }
        }
        
        private void AddToFullWordIndex(string word)
        {
            var lower = word.ToLower();
            
            if (!_fullWordIndex.ContainsKey(lower))
            {
                _fullWordIndex[lower] = 1;
            }
        }

        private void AddToDictionary(string word)
        {
            var lower = word.ToLower();
            
            if (!_index.ContainsKey(lower))
            {
                _index[lower] = 1;
            }
        }
    }
}