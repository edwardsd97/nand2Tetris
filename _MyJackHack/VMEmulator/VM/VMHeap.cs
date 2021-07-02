
namespace VM
{
    /////////////////////////////////////////////////////////////////////////////////
    // Heap - manages heap section of an already allocated int array of memory
    public class Heap
    {
        int[] mMemory;

        int mHeapFree;
        int mHeapCount;
        int mOptions;

        public enum Option
        {
            DEBUG,   // Debug mode - Marks memory that was freed or defragged
            DEFRAG,  // Defrag when freeing memory

            COUNT
        }

        public Heap(int[] memory, int start, int count)
        {
            OptionSet( Option.DEBUG, false );
            OptionSet( Option.DEFRAG, true );

            mMemory = memory;
            mHeapCount = count;
            mHeapFree = start;

            mMemory[mHeapFree] = 0; // next
            mMemory[mHeapFree + 1] = mHeapCount - 2; // size
        }

        public void OptionSet(Option op, bool enabled)
        {
            if (enabled)
                mOptions = mOptions | (1 << (int)op);
            else
                mOptions = mOptions & ~(1 << (int)op);
        }

        public bool OptionGet(Option op)
        {
            return (mOptions & (1 << (int)op)) != 0;
        }

        public int Alloc(int size)
        {
            int freeList = mHeapFree;
            int resultBlock = 0;

            if (size == 0)
                return 0;

            while (resultBlock == 0 && freeList != 0)
            {
                // freeList[0]: next
                // freeList[1]: size

                if (mMemory[freeList + 1] >= (size + 2))
                {
                    // Found first fit block that is big enough

                    // result block is allocated at the end of the free block's chunk of memory
                    int allocSize = size + 2;
                    resultBlock = freeList + mMemory[freeList + 1] + 2 - allocSize;
                    mMemory[resultBlock] = 0;
                    mMemory[resultBlock + 1] = size;

                    // reduce the free memory size from this block we just pulled from
                    mMemory[freeList + 1] = mMemory[freeList + 1] - allocSize;
                }

                freeList = mMemory[freeList];
            }

            if (resultBlock == 0)
            {
                // Could not allocate memory
                return 0;
            }

            // return the usable memory part of the block
            return resultBlock + 2;
        }

        public void Free(int addr)
        {
            int prevFreeList;

            if (addr == 0)
                return;

            // block address is usable memory pointer - 2
            int block = addr - 2;

            // block[0]: next
            // block[1]: size

            // append the block to the freeList
            prevFreeList = mHeapFree;
            mHeapFree = block;
            mMemory[block] = prevFreeList;

            if ( OptionGet( Option.DEBUG ) )
            {
                // Mark the memory as having been freed for debugging purposes
                int start = block + 2;
                int end = block + 2 + mMemory[block + 1];
                for (int i = start; i < end; i++)
                {
                    mMemory[i] = unchecked((int)4278124286); // FEFEFEFE (free)
                }
            }

            if (OptionGet(Option.DEFRAG))
            {
                DeFrag();
            }
        }

        public bool DeFrag()
        {
            // mHeapFree[0]: next
            // mHeapFree[1]: size

            for (int i = mHeapFree; i != 0; i = mMemory[i])
            {
                int target = i + 2 + mMemory[i + 1];
                int jParent = -1;

                for (int j = mHeapFree; j != 0; j = mMemory[j])
                {
                    if (j == i)
                        continue;

                    if (j == target)
                    {
                        // found a free block j that is right at the end of i block - merge them together

                        int debugStart = 0;
                        int debugEnd = 0;

                        if (OptionGet(Option.DEBUG))
                        {
                            debugStart = i + 2 + mMemory[i + 1];
                            debugEnd = debugStart + mMemory[j + 1] + 2;
                        }

                        // Expand i's size to encompass block j
                        mMemory[i + 1] = mMemory[i + 1] + mMemory[j + 1] + 2;

                        // If this free block was pointing to the target block, now this block points to the target block's next
                        if (mMemory[i] == j)
                            mMemory[i] = mMemory[j];

                        if (OptionGet(Option.DEBUG))
                        {
                            // Mark the memory as having been recovered from defrag for debugging purposes
                            for (int d = debugStart; d < debugEnd; d++)
                            {
                                mMemory[d] = unchecked((int)3755991007); // DFDFDFDF (defragged)
                            }
                        }

                        // then point the parent link to j to i instead
                        if (jParent >= 0)
                        {
                            mMemory[jParent] = i;
                        }
                        else if (mHeapFree == j)
                        {
                            mHeapFree = i;
                        }

                        break;
                    }

                    jParent = j;
                }
            }

            return false;
        }
    }

}