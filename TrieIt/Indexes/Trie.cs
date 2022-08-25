using System;
using System.Runtime.CompilerServices;
using TrieIt.Indexes.Helpers;

namespace TrieIt.Indexes
{
    /*
TrieIt.Indexes.Trie
Words: 370105
Took: 359 ms
Allocated: 114,288 kb
Peak Working Set: 131,436 kb
Gen 0 collections: 20
Gen 1 collections: 8
Gen 2 collections: 2
     */
    public class Trie : IWordIndex
    {
        private readonly Letters _letters;

        public Trie()
        {
            _letters = new Letters(Letters.NullTermination, 0);
        }
        
        public void Add(string word)
        {
            Span<char> terminated = stackalloc char[word.Length + 1];
            terminated[word.Length] = Letters.NullTermination;

            for (var i = 0; i < word.Length; ++i)
            {
                if (word[i] - 'A' <= 'Z' - 'A')
                {
                    terminated[i] = (char)(byte)( word[i] | 0x20 );
                }
                else
                {
                    terminated[i] = word[i];
                }
            }
            
            _letters.TryAdd(terminated);
        }
    }
    
    internal class Letters
    {
        public static readonly char NullTermination = '^';
        
        private readonly char _character;
        private readonly byte _depth;
        
        private Letters[] _nodes;
        private byte _position;
        

        public Letters(char character, byte depth)
        {
            _character = character;
            _depth = depth;
            _nodes = null;
        }

        public int Count
        {
            get
            {
                if (_nodes == null)
                {
                    return 0;
                }
                
                var count = 0;

                for (var i = _position - 1; i >= 0; --i)
                {
                    if (_nodes[i]._character != NullTermination)
                    {
                        ++count;
                    }

                    count += _nodes[i].Count;
                }

                return count;
            }
        }

        public int ExactCount
        {
            get
            {
                if (_nodes == null)
                {
                    return 0;
                }
                
                var count = 0;

                for (var i = _position - 1; i >= 0; --i)
                {
                    if (_nodes[i]._character == NullTermination)
                    {
                        ++count;
                    }
                    
                    count += _nodes[i].ExactCount;
                }

                return count;
            }
        }
        
        public override string ToString()
        {
            var start = $"Word: {(char) _character}";

            if (_nodes != null)
            {
                start += $", Children: {_position}";
            }
            
            return start;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFind(in ReadOnlySpan<char> word)
        {
            if (_depth == word.Length)
            {
                return true;
            }
            
            if (_nodes == null)
            {
                return false;
            }
            
            var character = word[_depth];

            for (var i = _position - 1; i >= 0; --i)
            {
                if (_nodes[i]._character == character)
                {
                    return _nodes[i].TryFind(word);
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Letters TryAdd(in ReadOnlySpan<char> word)
        {
            if (_depth == word.Length)
            {
                return this;
            }

            var character = word[_depth];

            if (_nodes != null)
            {
                for (var i = _position - 1; i >= 0; --i)
                {
                    // ReSharper disable once InvertIf
                    if (_nodes[i]._character == character)
                    {
                        return _nodes[i].TryAdd(word);
                    }
                }
            }
            
            var current = new Letters(character, (byte)(_depth + 1));
            var last = current.TryAdd(word);
            
            AddNodeToNode(this, current);

            return last;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddNodeToNode(Letters node1, Letters node2)
        {
            if (node1._nodes == null)
            {
                node1._nodes = new Letters[HashHelpers.PrimeLargerThan(node1._position)];
                node1._nodes[0] = node2;
            }
            else if (node1._position >= node1._nodes.Length)
            {
                var nodes = new Letters[HashHelpers.PrimeLargerThan(node1._position)];

                Array.Copy(node1._nodes, 0, nodes, 0, node1._nodes.Length);

                nodes[node1._position] = node2;
                node1._nodes = nodes;
            }
            else
            {
                node1._nodes[node1._position] = node2;
            }
            
            ++node1._position;
        }
    }
}