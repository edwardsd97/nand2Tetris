using System;
using System.Text;
using System.Collections.Generic;

namespace VM
{
    using EmulatedObjectCollection = Dictionary<int, EmulatedObject>;
    using EmulatedObjectRegistry = Dictionary<string, Dictionary<int, EmulatedObject>>;
    using EmulatedObjectInfo = Dictionary<string, EmulatedObject.Info>;

    public class EmulatedObject
    {
        public int mId;
        public object mObject;
        public Info mInfo;
        public int mHeapAddress;

        public class Info
        {
            public string mTypeName; // object type name
            public int mVirtualSize; // memory allocated in VM heap to fake it being part of VM memory
            public int mNextId;      // next id for new object of this type
        }

        public EmulatedObject(int id, object obj, int heapAddress, Info info)
        {
            mId = id;
            mInfo = info;
            mObject = obj;
            mHeapAddress = heapAddress;
        }

        public bool Valid()
        {
            return mObject != null && mId > 0;
        }
    }

    public class EmulatedObjects
    {
        public EmulatedObjectRegistry mObjects = new EmulatedObjectRegistry(); 
        public EmulatedObjectInfo mObjectInfo = new EmulatedObjectInfo();

        protected Emulator mVM;

        public EmulatedObjects(Emulator vm)
        {
            mVM = vm;
        }

        public void RegisterType(string type, int virtualSize = 0)
        {
            if (mObjectInfo.ContainsKey(type))
            {
                return;
            }

            EmulatedObject.Info info = new EmulatedObject.Info();
            info.mTypeName = type;
            info.mVirtualSize = virtualSize;
            info.mNextId = 1;

            mObjectInfo.Add(type, info);

            mObjects.Add(type, new EmulatedObjectCollection());
        }

        public EmulatedObject Alloc(string type, object value, char[] overrideFakeData)
        {
            return Alloc( type, value, Encoding.ASCII.GetBytes( overrideFakeData ) );
        }

        public EmulatedObject Alloc(string type, object value, byte[] overrideFakeData = null)
        {
            EmulatedObjectCollection emCol;

            if (mObjects.TryGetValue(type, out emCol))
            {
                EmulatedObject.Info info = mObjectInfo[type];

                bool fakeHeapObjects = mVM.OptionGet(Emulator.Option.FAKE_HEAP_OBJECTS);
                int heapAddress = 0;

                int virtualSize = info.mVirtualSize;
                if (overrideFakeData != null)
                {
                    virtualSize = overrideFakeData.Length / 4;
                    if (overrideFakeData.Length % 4 != 0)
                        virtualSize += 1;
                }

                if (fakeHeapObjects)
                {
                    heapAddress = mVM.mHeap.Alloc(virtualSize + 1);
                    if (heapAddress == 0)
                    {
                        mVM.Error("Out of heap memory");
                        return null;
                    }
                }

                if (heapAddress > 0 || !fakeHeapObjects)
                {
                    int newId = info.mNextId++;

                    EmulatedObject obj = new EmulatedObject(newId, value, heapAddress, info);
                    emCol.Add(newId, obj);

                    if (heapAddress > 0)
                    {
                        // Set the contents of heap memory address to the object id
                        mVM.mMemory[heapAddress] = obj.mId;

                        // Fill the virtual memory with random garbage or provided data
                        Random rand = new Random(heapAddress * 54321 + virtualSize * 12345);
                        for (int i = 1; i <= virtualSize; i++)
                        {
                            int b = (i - 1) * 4;
                            int mem = 0;

                            if (overrideFakeData != null && b < overrideFakeData.Length)
                                mem = mem | overrideFakeData[b];
                            else if (overrideFakeData == null)
                                mem = mem | rand.Next() % 256;

                            if (overrideFakeData != null && b + 1 < overrideFakeData.Length)
                                mem = mem | overrideFakeData[b + 1] << 8;
                            else if (overrideFakeData == null)
                                mem = mem | rand.Next() % 256 << 8;

                            if (overrideFakeData != null && b + 2 < overrideFakeData.Length)
                                mem = mem | overrideFakeData[b + 2] << 16;
                            else if (overrideFakeData == null)
                                mem = mem | rand.Next() % 256 << 16;

                            if (overrideFakeData != null && b + 3 < overrideFakeData.Length)
                                mem = mem | overrideFakeData[b + 3] << 24;
                            else if (overrideFakeData == null)
                                mem = mem | rand.Next() % 128 << 24;

                            mVM.mMemory[heapAddress + i] = mem;
                        }
                    }

                    return obj;
                }
            }

            mVM.Error("Object '" + type + "' type unknown");
            return null;
        }

        public bool Free(EmulatedObject obj)
        {
            if (obj != null)
            {
                EmulatedObjectCollection emCol;

                if (mObjects.TryGetValue(obj.mInfo.mTypeName, out emCol))
                {
                    if (emCol.TryGetValue(obj.mId, out obj))
                    {
                        if (obj.mHeapAddress > 0)
                            mVM.mHeap.Free(obj.mHeapAddress);
                        emCol.Remove(obj.mId);
                        return true;
                    }
                }

                mVM.Error("Object '" + obj.mInfo.mTypeName + "' not found: " + obj.mId);
            }

            return false;
        }

        public EmulatedObject Get(string type, int id)
        {
            EmulatedObjectCollection emCol;
            EmulatedObject emObj;

            if (mObjects.TryGetValue(type, out emCol))
            {
                if (emCol.TryGetValue(id, out emObj))
                {
                    return emObj;
                }
            }

            mVM.Error("Object '" + type + "' not found: " + id);
            RegisterType(type, 0);
            return new EmulatedObject(0, null, 0, mObjectInfo[type]);
        }

        public void Set(string type, int id, object value)
        {
            EmulatedObjectCollection emCol;
            EmulatedObject emObj;

            if (mObjects.TryGetValue(type, out emCol))
            {
                if (emCol.TryGetValue(id, out emObj))
                {
                    emObj.mObject = value;
                    return;
                }
            }

            mVM.Error("Object '" + type + "' not found: " + id);
        }
    }
}
