using System;

namespace VM
{
    ///////////////////////////////////////////////
    // Emulator OS Built In Functions
    public static class VMOS_String
    {
        public static void Register(BuiltIn builtIns)
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

        private static EmulatedObject GetStringObject(Emulator vm, int objectRef)
        {
            int id = objectRef;

            vm.mObjects.RegisterType("string", 2);

            if (id < 0)
            {
                // negative string ids are static string ids
                return vm.mObjects.Get("string", -id );
            }

            if (vm.OptionGet(Emulator.Option.FAKE_HEAP_OBJECTS))
                id = vm.mMemory[id];

            return vm.mObjects.Get( "string", id );
        }

        private static bool CanModify(Emulator vm, int strId, bool errorIfNot = true)
        {
            if (strId > vm.mStringsStatic)
            {
                return true;
            }

            if (errorIfNot)
                vm.Error("Cannot modify static string - use String.new( \"My String\" )");

            return false;
        }

        /** Constructs a new string from static string */
        // function String new( String staticString );
        public static void AllocNew(Emulator vm)
        {
            int sourceStrId = vm.StackPop();
            int strId = 0;

            EmulatedObject src = GetStringObject( vm, sourceStrId );
            EmulatedObject obj = null;

            if (src != null && src.mObject != null)
            {
                obj = vm.mObjects.Alloc("string", (string)src.mObject, ((string)src.mObject).ToCharArray() );
                strId = obj.mId;
            }

            if (vm.OptionGet(Emulator.Option.FAKE_HEAP_OBJECTS) && obj != null)
                vm.StackPush(obj.mHeapAddress);
            else
                vm.StackPush(strId);
        }

        /** Disposes this string. */
        // method void dispose();
        public static void Dispose(Emulator vm)
        {
            int strId = vm.StackPop();

            EmulatedObject strObj = GetStringObject(vm, strId);

            if ( strObj.Valid() && CanModify(vm, strObj.mId, false) )
            {
                vm.mObjects.Free(strObj);
            }
        }

        /** Returns the current length of this string. */
        // method int length();
        public static void Length(Emulator vm)
        {
            int strId = vm.StackPop();

            EmulatedObject strObj = GetStringObject(vm, strId);

            if (strObj.Valid())
                vm.StackPush( ((string)strObj.mObject).Length );
        }

        /** Returns the character at the j-th location of this string. */
        // method char charAt(int j);
        public static void CharAt(Emulator vm)
        {
            int j = vm.StackPop();
            int strId = vm.StackPop();

            EmulatedObject strObj = GetStringObject(vm, strId);

            if (strObj.Valid())
            {
                if (j >= 0 || j < ((string)strObj.mObject).Length)
                {
                    vm.StackPush(((string)strObj.mObject)[j]);
                }
                else
                {
                    vm.StackPush(0);
                    vm.Error("String index out of bounds: " + j);
                }
            }
        }

        /** Sets the character at the j-th location of this string to c. */
        // method void setCharAt(int j, char c);
        public static void SetCharAt(Emulator vm)
        {
            char c = (char)vm.StackPop();
            int j = vm.StackPop();
            int strId = vm.StackPop();

            EmulatedObject strObj = GetStringObject(vm, strId);

            if (strObj.Valid() && CanModify(vm, strId))
            {
                if (j >= 0 || j < ((string)strObj.mObject).Length)
                {
                    string newString = "";
                    if (j > 0)
                        newString = ((string)strObj.mObject).Substring(0, j);
                    newString = newString + c;
                    if (j < ((string)strObj.mObject).Length - 1)
                        newString = newString + ((string)strObj.mObject).Substring(j + 1, ((string)strObj.mObject).Length - j - 1);
                    strObj.mObject = newString;
                }
                else
                {
                    vm.Error("String index out of bounds: " + j);
                }
            }
        }

        /** Appends c to this string's end and returns this string. */
        // method String appendChar(char c);
        public static void AppendChar(Emulator vm)
        {
            char c = (char)vm.StackPop();
            int strId = vm.StackPop();

            EmulatedObject strObj = GetStringObject(vm, strId);

            if (strObj.Valid() && CanModify(vm, strId))
            {
                strObj.mObject = ((string)strObj.mObject) + c;
            }
        }

        /** Appends string to this string's end and returns this string. */
        // method String append(String s);
        public static void Append(Emulator vm)
        {
            int strAppend = vm.StackPop();
            int strId = vm.StackPop();

            EmulatedObject strObj = GetStringObject(vm, strId);
            EmulatedObject strAppendObj = GetStringObject(vm, strAppend);

            if (strObj.Valid() && CanModify(vm, strId))
            {
                strObj.mObject = ((string)strObj.mObject) + ((string)strAppendObj.mObject);
            }

            vm.StackPush(strId);
        }

        /** Erases the last character from this string. */
        // method void eraseLastChar();
        public static void EraseLastChar(Emulator vm)
        {
            int strId = vm.StackPop();
            EmulatedObject strObj = GetStringObject(vm, strId);

            if (strObj.Valid() && CanModify(vm, strId) && ((string)strObj.mObject).Length > 0)
            {
                strObj.mObject = ((string)strObj.mObject).Substring(0, ((string)strObj.mObject).Length - 1);
            }
        }

        /** Returns the integer value of this string, 
         *  until a non-digit character is detected. */
        // method int intValue();
        public static void IntValue(Emulator vm)
        {
            int strId = vm.StackPop();
            EmulatedObject strObj = GetStringObject(vm, strId);

            if (strObj.Valid())
            {
                try
                {
                    vm.StackPush(int.Parse(((string)strObj.mObject)));
                }
                catch
                {
                    vm.StackPush(0);
                }
            }
        }

        /** Sets this string to hold a representation of the given value. */
        // method void setInt(int i);
        public static void SetInt(Emulator vm)
        {
            int i = vm.StackPop();
            int strId = vm.StackPop();
            EmulatedObject strObj = GetStringObject(vm, strId);

            if (strObj.Valid() && CanModify(vm, strId))
            {
                strObj.mObject = "" + i;
            }
        }
    }
}
