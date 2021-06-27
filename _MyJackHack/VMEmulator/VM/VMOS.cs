using System;

///////////////////////////////////////////////
// VM OS Built In Functions

public static class VMOS_Sys
{
    public static void Register(VMBuiltIn builtIns)
    {
        builtIns.Register("Sys.halt", VMOS_Sys.Halt);
        builtIns.Register("Sys.wait", VMOS_Sys.Wait);
        builtIns.Register("Sys.error", VMOS_Sys.Error);
    }

    /** Halts the program execution. */
    // function void halt();
    public static void Halt(VM vm)
    {
        if ( vm.mCode != null )
            vm.mCodeFrame = vm.mCode.Length;
    }

    /** Waits approximately duration milliseconds and returns.  */
    // function void wait(int durationMs);
    public static void Wait(VM vm)
    {
        int durationMs = vm.StackPop();
        System.Threading.Thread.Sleep(durationMs);
    }

    /** Displays the given error code in the form "ERR<errorCode>",
     *  and halts the program's execution. */
    // function void error(int errorCode);
    public static void Error(VM vm)
    {
        int errorCode = vm.StackPop();
        vm.Error( "ERROR: " + errorCode );
    }
}

public static class VMOS_Memory
{
    public static void Register(VMBuiltIn builtIns)
    {
        builtIns.Register("Memory.peek", VMOS_Memory.Peek);
        builtIns.Register("Memory.poke", VMOS_Memory.Poke);
        builtIns.Register("Memory.alloc", VMOS_Memory.Alloc);
        builtIns.Register("Memory.deAlloc", VMOS_Memory.Free);
    }

    /** Returns the RAM value at the given address. */
    // function int peek(int address);
    public static void Peek(VM vm)
    {
        int address = vm.StackPop();
        if (address >= 0 && address < vm.mMemory.Length)
            vm.StackPush(vm.mMemory[address]);
        else
            vm.Error("Address out of memory space " + address);
    }

    /** Sets the RAM value at the given address to the given value. */
    // function void poke(int address, int value);
    public static void Poke(VM vm)
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
    public static void Alloc(VM vm)
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
    public static void Free(VM vm)
    {
        int address = vm.StackPop();
        vm.mHeap.Free(address);
    }
}

public static class VMOS_Math
{
    public static void Register(VMBuiltIn builtIns)
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
    public static void Abs(VM vm)
    {
        int x = vm.StackPop();
        vm.StackPush(Math.Abs(x));
    }

    /** Returns the integer part of the square root of x. */
    // function int sqrt(int x);
    public static void Sqrt(VM vm)
    {
        int x = vm.StackPop();
        vm.StackPush((int)Math.Sqrt(x));
    }

    /** Returns the greater number. */
    // function int max(int x, int y);
    public static void Max(VM vm)
    {
        int y = vm.StackPop();
        int x = vm.StackPop();
        vm.StackPush((int)Math.Max(x, y));
    }

    /** Returns the smaller number. */
    // function int min(int x, int y);
    public static void Min(VM vm)
    {
        int y = vm.StackPop();
        int x = vm.StackPop();
        vm.StackPush((int)Math.Min(x, y));
    }

    /** Returns x squared. */
    // function int sqr(int x);
    public static void Sqr(VM vm)
    {
        int x = vm.StackPop();
        vm.StackPush(x * x);
    }

    /** Returns x to the power of y */
    // function int pow(int x, int y);
    public static void Pow(VM vm)
    {
        int y = vm.StackPop();
        int x = vm.StackPop();
        vm.StackPush((int)Math.Pow(x, y));
    }
}

public static class VMOS_Array
{
    public static void Register(VMBuiltIn builtIns)
    {
        builtIns.Register("Array.new", VMOS_Array.AllocNew);
        builtIns.Register("Array.dispose", VMOS_Array.Dispose);
    }

    /** Constructs a new Array of the given size. */
    // function Array new(int size);
    public static void AllocNew(VM vm)
    {
        VMOS_Memory.Alloc(vm);
    }

    /** Disposes this array. */
    // method void dispose();
    public static void Dispose(VM vm)
    {
        VMOS_Memory.Free(vm);
    }
}

public static class VMOS_String
{
    public static void Register(VMBuiltIn builtIns)
    {
        builtIns.Register("String.new", VMOS_String.AllocNew);
        builtIns.Register("String.dispose", VMOS_String.Dispose);
        builtIns.Register("String.length", VMOS_String.Length);
        builtIns.Register("String.charAt", VMOS_String.CharAt);
        builtIns.Register("String.setCharAt", VMOS_String.SetCharAt);
        builtIns.Register("String.appendChar", VMOS_String.AppendChar);
        builtIns.Register("String.append", VMOS_String.Append);
        builtIns.Register("String.eraseLastChar", VMOS_String.EraseLastChar);
        builtIns.Register("String.intValue", VMOS_String.IntValue);
        builtIns.Register("String.setInt", VMOS_String.SetInt);
    }

    private static bool CanModify(VM vm, int strId, bool errorIfNot = true )
    {
        if (strId >= vm.mStringsStatic)
        {
            return true;
        }

        if ( errorIfNot )
            vm.Error("Cannot modify static string - use String.new( \"My String\" )");

        return false;
    }

    private static VM.VMString StrObj(VM vm, int strId)
    {
        VM.VMString stringObj;
        if (vm.mStrings.TryGetValue(strId, out stringObj))
        {
            return stringObj;
        }
        vm.Error("String not found: " + strId);
        return new VM.VMString("");
    }

    /** Constructs a new string from static string */
    // function String new( String staticString );
    public static void AllocNew(VM vm)
    {
        int sourceStrId = vm.StackPop();
        int strId = 0;
        foreach (int key in vm.mStrings.Keys)
        {
            strId = Math.Max(strId, key);
        }
        strId++;
        VM.VMString sourceObj = StrObj(vm, sourceStrId);

        vm.mStrings.Add( strId, new VM.VMString(sourceObj.mString));
        vm.StackPush(strId);
    }

    /** Disposes this string. */
    // method void dispose();
    public static void Dispose(VM vm)
    {
        int strId = vm.StackPop();

        if ( CanModify( vm, strId, false ) )
        {
            VM.VMString stringObj;
            if (vm.mStrings.TryGetValue(strId, out stringObj))
            {
                stringObj.mRef--;
                if (stringObj.mRef <= 0)
                    vm.mStrings.Remove(strId);
            }
        }
    }

    /** Returns the current length of this string. */
    // method int length();
    public static void Length(VM vm)
    {
        vm.StackPush( StrObj( vm, vm.StackPop() ).mString.Length );
    }

    /** Returns the character at the j-th location of this string. */
    // method char charAt(int j);
    public static void CharAt(VM vm)
    {
        int j = vm.StackPop();
        int strId = vm.StackPop();
        VM.VMString strObj = StrObj(vm, strId);

        if (j >= 0 || j < strObj.mString.Length)
        {
            vm.StackPush(strObj.mString[j]);
        }
        else
        {
            vm.StackPush(0);
            vm.Error("String index out of bounds: " + j);
        }
    }

    /** Sets the character at the j-th location of this string to c. */
    // method void setCharAt(int j, char c);
    public static void SetCharAt(VM vm)
    {
        char c = (char) vm.StackPop();
        int j = vm.StackPop();
        int strId = vm.StackPop();
        VM.VMString strObj = StrObj(vm, strId);

        if (CanModify(vm, strId))
        {
            if (j >= 0 || j < strObj.mString.Length)
            {
                string newString = "";
                if (j > 0)
                    newString = strObj.mString.Substring(0, j);
                newString = newString + c;
                if ( j < strObj.mString.Length - 1 )
                    newString = newString + strObj.mString.Substring(j + 1, strObj.mString.Length - j - 1 );
                strObj.mString = newString;
            }
            else
            {
                vm.Error("String index out of bounds: " + j);
            }
        }
    }

    /** Appends c to this string's end and returns this string. */
    // method String appendChar(char c);
    public static void AppendChar(VM vm)
    {
        char c = (char)vm.StackPop();
        int strId = vm.StackPop();
        VM.VMString strObj = StrObj(vm, strId);

        if (CanModify(vm, strId))
        {
            strObj.mString = strObj.mString + c;
        }
    }

    /** Appends string to this string's end and returns this string. */
    // method String append(String s);
    public static void Append(VM vm)
    {
        int strAppend = vm.StackPop();
        int strId = vm.StackPop();
        VM.VMString strObj = StrObj(vm, strId);
        VM.VMString strAppendObj = StrObj(vm, strAppend);

        if (CanModify(vm, strId))
        {
            strObj.mString = strObj.mString + strAppendObj.mString;
        }

        vm.StackPush(strId);
    }

    /** Erases the last character from this string. */
    // method void eraseLastChar();
    public static void EraseLastChar(VM vm)
    {
        int strId = vm.StackPop();
        VM.VMString strObj = StrObj(vm, strId);

        if ( CanModify(vm, strId) && strObj.mString.Length > 0 )
        {
            strObj.mString = strObj.mString.Substring(0, strObj.mString.Length - 1);
        }
    }

    /** Returns the integer value of this string, 
     *  until a non-digit character is detected. */
    // method int intValue();
    public static void IntValue(VM vm)
    {
        int strId = vm.StackPop();
        VM.VMString strObj = StrObj(vm, strId);

        try
        {
            vm.StackPush(int.Parse(strObj.mString));
        }
        catch
        {
            vm.StackPush(0);
        }
    }

    /** Sets this string to hold a representation of the given value. */
    // method void setInt(int i);
    public static void SetInt(VM vm)
    {
        int i = vm.StackPop();
        int strId = vm.StackPop();
        VM.VMString strObj = StrObj(vm, strId);
        if (CanModify(vm, strId))
        {
            strObj.mString = "" + i;
        }
    }
}
