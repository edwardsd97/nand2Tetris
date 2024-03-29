﻿using System;

namespace VM
{
    ///////////////////////////////////////////////
    // Emulator OS Built In Functions

    public static class VMOS_Sys
    {
        public static void Register(BuiltIn builtIns)
        {
            builtIns.Register("Sys.halt", VMOS_Sys.Halt);
            builtIns.Register("Sys.wait", VMOS_Sys.Wait);
            builtIns.Register("Sys.error", VMOS_Sys.Error);
        }

        /** Halts the program execution. */
        // function void halt();
        public static void Halt(Emulator vm)
        {
            if (vm.mCode != null)
                vm.mCodeFrame = vm.mCode.Length;
        }

        /** Waits approximately duration milliseconds and returns.  */
        // function void wait(int durationMs);
        public static void Wait(Emulator vm)
        {
            int durationMs = vm.StackPop();
            System.Threading.Thread.Sleep(durationMs);
        }

        /** Displays the given error code in the form "ERR<errorCode>",
         *  and halts the program's execution. */
        // function void error(int errorCode);
        public static void Error(Emulator vm)
        {
            int errorCode = vm.StackPop();
            vm.Error("ERROR: " + errorCode);
        }
    }

    public static class VMOS_Memory
    {
        public static void Register(BuiltIn builtIns)
        {
            builtIns.Register("Memory.peek", VMOS_Memory.Peek);
            builtIns.Register("Memory.poke", VMOS_Memory.Poke);
            builtIns.Register("Memory.alloc", VMOS_Memory.Alloc);
            builtIns.Register("Memory.free", VMOS_Memory.Free);
        }

        /** Returns the RAM value at the given address. */
        // function int peek(int address);
        public static void Peek(Emulator vm)
        {
            int address = vm.StackPop();
            if (address >= 0 && address < vm.mMemory.Length)
                vm.StackPush(vm.mMemory[address]);
            else
                vm.Error("Address out of memory space " + address);
        }

        /** Sets the RAM value at the given address to the given value. */
        // function void poke(int address, int value);
        public static void Poke(Emulator vm)
        {
            int value = vm.StackPop();
            int address = vm.StackPop();
            if (address >= 0 && address < vm.mMemory.Length)
                vm.mMemory[address] = value;
            else
                vm.Error("Address out of memory space " + address);
        }

        /** Finds an available RAM block of the given size and returns
         *  a reference to its base address. */
        // function int alloc(int size)
        public static void Alloc(Emulator vm)
        {
            int size = vm.StackPop();
            int address = 0;
            if (size > 0)
            {
                address = vm.mHeap.Alloc(size);
                if (address == 0)
                    vm.Error("Out of free heap memory");
            }
            vm.StackPush(address);
        }

        /** De-allocates the given object (cast as an array) by making
         *  it available for future allocations. */
        // function void deAlloc(int addr)
        public static void Free(Emulator vm)
        {
            int address = vm.StackPop();
            vm.mHeap.Free(address);
        }
    }

    public static class VMOS_Math
    {
        public static void Register(BuiltIn builtIns)
        {
            builtIns.Register("Math.abs", VMOS_Math.Abs);
            builtIns.Register("Math.sqrt", VMOS_Math.Sqrt);
            builtIns.Register("Math.sqr", VMOS_Math.Sqr);
            builtIns.Register("Math.max", VMOS_Math.Max);
            builtIns.Register("Math.min", VMOS_Math.Min);
            builtIns.Register("Math.pow", VMOS_Math.Pow);
        }

        /** Returns the absolute value of x. */
        // function int abs(int x);
        public static void Abs(Emulator vm)
        {
            int x = vm.StackPop();
            vm.StackPush(Math.Abs(x));
        }

        /** Returns the integer part of the square root of x. */
        // function int sqrt(int x);
        public static void Sqrt(Emulator vm)
        {
            int x = vm.StackPop();
            vm.StackPush((int)Math.Sqrt(x));
        }

        /** Returns the greater number. */
        // function int max(int x, int y);
        public static void Max(Emulator vm)
        {
            int y = vm.StackPop();
            int x = vm.StackPop();
            vm.StackPush((int)Math.Max(x, y));
        }

        /** Returns the smaller number. */
        // function int min(int x, int y);
        public static void Min(Emulator vm)
        {
            int y = vm.StackPop();
            int x = vm.StackPop();
            vm.StackPush((int)Math.Min(x, y));
        }

        /** Returns x squared. */
        // function int sqr(int x);
        public static void Sqr(Emulator vm)
        {
            int x = vm.StackPop();
            vm.StackPush(x * x);
        }

        /** Returns x to the power of y */
        // function int pow(int x, int y);
        public static void Pow(Emulator vm)
        {
            int y = vm.StackPop();
            int x = vm.StackPop();
            vm.StackPush((int)Math.Pow(x, y));
        }
    }

    public static class VMOS_Array
    {
        public static void Register(BuiltIn builtIns)
        {
            /** Constructs a new Array of the given size. */
            // function Array new(int size);
            builtIns.Register("Array.new", VMOS_Memory.Alloc);

            /** Disposes this array. */
            // method void dispose();
            builtIns.Register("Array.dispose", VMOS_Memory.Free);
        }
    }
}
