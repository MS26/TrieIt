using System;
using System.Runtime.CompilerServices;
using TrieIt.Indexes.Helpers;

namespace TrieIt.Indexes
{
    /*
TrieIt.Indexes.TrieCompressed
Words: 370105
Took: 344 ms
Allocated: 96,319 kb
Peak Working Set: 113,764 kb
Gen 0 collections: 17
Gen 1 collections: 7
Gen 2 collections: 2
     */
    public class TrieCompressed : IWordIndex
    {
        private readonly LettersCollapsed _letters;

        public TrieCompressed()
        {
            _letters = new LettersCollapsed(LettersCollapsed.NullTermination, 0);
        }
        
        public void Add(string word)
        {
            Span<char> terminated = stackalloc char[word.Length + 1];
            terminated[word.Length] = LettersCollapsed.NullTermination;

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
    
    /*
     * Each node contains a _character which is the primary character of the node and helps lookups know whether it is the correct node for the lookup.
     * It then contains the _flattenedNodes which is an array of characters which also matches the same match collection as the primary character. This collection is in the order of the word that was added to the trie. i.e. Primary: B, Flattened: M, W
     * There is then finally a _expandedNodes array. This is the characters after the Primary and Flattened that do not match the matches collection in the parent but are obviously needed to map out the words.
     */
    internal class LettersCollapsed
    {
        public static readonly char NullTermination = '^';
        
        private readonly char _character;
        private readonly byte _depth;
        
        private LettersCollapsed[] _expandedNodes;
        private char[] _flattenedNodes;
        private byte _position;
        

        public LettersCollapsed(char character, byte depth)
        {
            _character = character;
            _depth = depth;
            _expandedNodes = null;
            _flattenedNodes = null;
        }

        public int Count
        {
            get
            {
                if (_expandedNodes == null)
                {
                    return 0;
                }
                
                var count = 0;

                for (var i = _position - 1; i >= 0; --i)
                {
                    if (_expandedNodes[i]._character != NullTermination)
                    {
                        ++count;
                    }

                    count += _expandedNodes[i].Count;
                }

                return count;
            }
        }

        public int ExactCount
        {
            get
            {
                if (_expandedNodes == null)
                {
                    return 0;
                }
                
                var count = 0;

                for (var i = _position - 1; i >= 0; --i)
                {
                    if (_expandedNodes[i]._character == NullTermination)
                    {
                        ++count;
                    }
                    
                    count += _expandedNodes[i].ExactCount;
                }

                return count;
            }
        }
        
        public override string ToString()
        {
            var start = $"Word: {(char) _character}";

            if (_flattenedNodes != null)
            {
                start += string.Concat(_flattenedNodes);
            }

            if (_expandedNodes != null)
            {
                start += $", Children: {_position}";
            }
            
            return start;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFind(in ReadOnlySpan<char> word)
        {
            /*
             * A node has a set depth based oon it location in the trie however due to the fact we collapse nodes into one another the depth isn't fixed on a per request basis.
             * So var the depth to ensure we can modify just on the per request basis.
             */
            var depth = _depth;
            
            /*
             * This method is recursive and we have to handle the fact that eventually we will reach the end of the word so once this happens return out.
             */
            if (depth == word.Length)
            {
                return true;
            }

            if (_flattenedNodes != null)
            {
                /*
                 * This node has _flattenedNodes so iterate them ensuring that each of the requested words characters match the characters in the _flattenedNodes.
                 */
                for (byte index = 0; depth < word.Length && index < _flattenedNodes.Length; ++depth, ++index)
                {
                    /*
                     * If the character in the requested word does not match the character in the _flattenedNodes then we know we can not find a match so return an empty matches collection.
                     */
                    if (_flattenedNodes[index] != word[depth])
                    {
                        return false;
                    }
                }
            
                /*
                 * If we completed the requested word whilst iterating the _flattenedNodes then return.
                 */
                if (depth == word.Length)
                {
                    return true;
                }
            }

            if (_expandedNodes != null)
            {
                /*
                 * Retrieve the character from the word for the current depth of the word that we are looking at.
                 * At this point in time we may have checked the _character of the node and the _flattenedNodes characters but still not reached the full depth of the requested word, which is why we use the request based depth variable rather than the nodes _depth.
                 */
                var character = word[depth];

                for (var i = _position - 1; i >= 0; --i)
                {
                    /*
                     * If the expanded nodes _character is the one we are looking for based on our current depth then recursively call into TryFind. 
                     */
                    if (_expandedNodes[i]._character == character)
                    {
                        return _expandedNodes[i].TryFind(word);
                    }
                }
            }

            /*
             * If the ever reach the end without having reached the depth of our requested word then return an empty match as we were unable to find the matches we required for the request.
             */
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LettersCollapsed TryAdd(in ReadOnlySpan<char> word)
        {
            /*
             * A node has a set depth based oon it location in the trie however due to the fact we collapse nodes into one another the depth isn't fixed on a per request basis.
             * So var the depth to ensure we can modify just on the per request basis.
             */
            var depth = _depth;
            
            /*
             * This method is recursive and we have to handle the fact that eventually we will reach the end of the word so once this happens return out.
             */
            if (depth == word.Length)
            {
                return this;
            }
            
            if (_flattenedNodes != null)
            {
                byte index = 0;
                byte split = 0;
                
                /*
                 * If we have _flattenedNodes then we need to iterate through them to find out how many of them we can use for the word being indexed.
                 * We use the index variable to signify how far through the _flattenedNodes we got before we a branch occured.
                 * We use the split variable to signify how far through the _flattenedNodes we got before we reached the last two characters of the word being indexed. This is important because we index the last and terminating character of words in separate nodes as they have a higher rating in the matches slots.
                 */
                for (; depth < word.Length && index < _flattenedNodes.Length; )
                {
                    if (_flattenedNodes[index] != word[depth])
                    {
                        break;
                    }

                    ++depth;
                    ++index;
                    
                    if (depth < word.Length - 1)
                    {
                        ++split;
                    }
                }

                /*
                 * If all of the word being indexed was already in the flattened nodes then return as we don't need to expand the trie in anyway.
                 */
                if (depth == word.Length)
                {
                    return this;
                }

                /*
                 * If the indexing word matched everything in the _flattenedNodes except the last and terminating character then we need to ensure we branch the trie slightly earlier so we can ensure the last and terminating character has there own node so the matches collection can have a higher rating.
                 */
                if (split < index)
                {
                    depth = (byte) (depth - ( index - split ));
                    index = split;
                }

                var flattenedNodesAsSpan = _flattenedNodes.AsSpan();
                
                var flattenedNodesThatNeedToBeExpanded = flattenedNodesAsSpan
                    .Slice(index);
                
                var flattenedNodesThatNeedToReplaceTheCurrentFlattenedNodes = flattenedNodesAsSpan
                    .Slice(0, index);

                /*
                 * If the current _flattenedNodes do not equal what the _flattenedNodes should be based on the word being indexed then replace them.
                 * This allows us to split the _flattenedNodes and explode the remaining into a child node..
                 *
                 * Example:
                 * _character = T
                 * _flattenedNodes = YP
                 * _expandedNodes = E^
                 *
                 * Would become:
                 * _character = T
                 * _flattenedNodes = Y
                 * _expandedNodes = PE^, RE^
                 */
                if (flattenedNodesAsSpan.SequenceEqual(flattenedNodesThatNeedToReplaceTheCurrentFlattenedNodes) == false)
                {
                    SetFlattenedNodes(flattenedNodesThatNeedToReplaceTheCurrentFlattenedNodes);
                }
                
                if (flattenedNodesThatNeedToBeExpanded.Length > 0)
                {
                    /*
                     * If we need to explode some of the _flattenedNodes then the first character become the primary character of the expanded node.
                     * We Clone the matches collection to ensure they are separate objects and altering one doesn't by reference alter the other.
                     * We Move the current nodes _expandedNodes down to the new child.
                     * We set the remaining _flattenedNodes to flattened nodes on the new expanded node.
                     *
                     * We then reset the _expandedNodes on the current node to be only this new expanded node.
                     */
                    var expandedNodeThatHasComeFromTheFlattenedNodes = new LettersCollapsed(flattenedNodesThatNeedToBeExpanded[0], (byte) (depth + 1));
                    expandedNodeThatHasComeFromTheFlattenedNodes.SetExpandedNodes(_expandedNodes, _position);

                    var flattenedNodesThatNeedToBeExpandedChildren = flattenedNodesThatNeedToBeExpanded.Slice(1);
                    
                    if (flattenedNodesThatNeedToBeExpandedChildren.Length > 0)
                    {
                        expandedNodeThatHasComeFromTheFlattenedNodes.SetFlattenedNodes(flattenedNodesThatNeedToBeExpandedChildren);
                    }
                    
                    SetExpandedNodes(new []
                    {
                        expandedNodeThatHasComeFromTheFlattenedNodes
                    }, 1);
                }
            }

            if (_expandedNodes != null)
            {
                for (var i = _position - 1; i >= 0; --i)
                {
                    /*
                     * If the expanded nodes _character is the one we are looking for based on our current depth then recursively call into TryAdd.
                     * Then ensure that the _expandedNodes has a match added to it for the object being indexed.
                     * Then return early as the recursive call to TryAdd would ensure that the complete word had been indexed so we do not have to do any moore operations in this node.
                     */
                    if (_expandedNodes[i]._character == word[depth])
                    {
                        return _expandedNodes[i].TryAdd(word);
                    }
                }
            }

            /*
             * If we were unable to complete the word being indexed in the _flattenedNodes or _expandedNodes we know we need to create a new branch in the trie to complete the word.
             * Take the primary character as the character at the current depth we have got to from the word being indexed.
             */
            var last2 = new LettersCollapsed(word[depth], (byte) (depth + 1));
            
            var remainingNodes = word.Slice(depth + 1);
            
            /*
             * If we only have on character left in the word then just set it as a flattened node. This is ok as it ensures that the last and terminating character live in node by themself and have the higher rating.
             */
            if (remainingNodes.Length <= 1)
            {
                last2.SetFlattenedNodes(remainingNodes);
            }
            else
            {
                /*
                 * If there is more than one character left we split the characters into two groups.
                 * The remainingNodesForFlattened group is all of the characters that do not include the last and terminating character. These can be assigned as _flattenedNodes on the expanded node created above.
                 * The endingNodesForExpanded group are the last and terminating character that need the higher rating and so for this group we create another node to ensure these characters have there own matches collection.
                 *
                 * Example:
                 * _character = T
                 * _flattenedNodes = YP
                 * _expandedNodes = E^
                 */
                var remainingNodesForFlattened = remainingNodes.Slice(0, remainingNodes.Length - 2);
                var endingNodesForExpanded = remainingNodes.Slice(remainingNodes.Length - 2);
                
                depth = (byte) (depth + remainingNodesForFlattened.Length);

                if (remainingNodesForFlattened.Length > 0)
                {
                    last2.SetFlattenedNodes(remainingNodesForFlattened);
                }
            
                if (endingNodesForExpanded.Length > 0)
                {
                    var last3 = new LettersCollapsed(word[depth + 1], (byte) (depth + 2));
                    last3.SetFlattenedNodes(endingNodesForExpanded.Slice(1));
                
                    last2.AddExpandedNode(last3);
                }
            }
            
            /*
             * Once the expanded node creation is completed and we know we have indexed the complete word we add the new node to the current nodes _expandedNodes and return as we have now finished indexing.
             */
            AddExpandedNode(last2);

            return last2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetFlattenedNodes(ReadOnlySpan<char> nodes)
        {
            if (nodes == null || nodes.Length == 0)
            {
                _flattenedNodes = null;
            }
            else
            {
                _flattenedNodes = nodes.ToArray();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetExpandedNodes(LettersCollapsed[] nodes, byte position)
        {
            if (nodes == null || nodes.Length == 0)
            {
                _position = 0;
                _expandedNodes = null;
            }
            else
            {
                _position = position;
                _expandedNodes = nodes;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddExpandedNode(LettersCollapsed node)
        {
            if (_expandedNodes == null)
            {
                _expandedNodes = new LettersCollapsed[HashHelpers.PrimeLargerThan(_position)];
                _expandedNodes[0] = node;
                _position = 0;
            }
            else if (_position >= _expandedNodes.Length)
            {
                var nodes = new LettersCollapsed[HashHelpers.PrimeLargerThan(_position)];

                Array.Copy(_expandedNodes, 0, nodes, 0, _expandedNodes.Length);

                nodes[_position] = node;
                _expandedNodes = nodes;
            }
            else
            {
                _expandedNodes[_position] = node;
            }
            
            ++_position;
        }
    }
}