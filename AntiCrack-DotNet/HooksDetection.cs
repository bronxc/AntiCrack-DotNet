﻿using System;
using System.IO;
using System.Reflection;
using System.Net.Sockets;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net;

namespace AntiCrack_DotNet
{
    public sealed class HooksDetection
    {
        public static object ProcessMethod { get; private set; }

        #region WinApi

        [DllImport("ntdll.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern void RtlInitUnicodeString(out Structs.UNICODE_STRING DestinationString, string SourceString);

        [DllImport("ntdll.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern void RtlUnicodeStringToAnsiString(out Structs.ANSI_STRING DestinationString, Structs.UNICODE_STRING UnicodeString, bool AllocateDestinationString);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint LdrGetDllHandleEx(ulong Flags, [MarshalAs(UnmanagedType.LPWStr)] string DllPath, [MarshalAs(UnmanagedType.LPWStr)] string DllCharacteristics, Structs.UNICODE_STRING LibraryName, ref IntPtr DllHandle);

        [DllImport("kernelbase.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandleA(string Library);

        [DllImport("kernelbase.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string Function);

        [DllImport("ntdll.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern uint LdrGetProcedureAddressForCaller(IntPtr Module, Structs.ANSI_STRING ProcedureName, ushort ProcedureNumber, out IntPtr FunctionHandle, ulong Flags, IntPtr CallBack);

        #endregion

        /// <summary>
        /// Gets the handle of a specified module using low-level functions.
        /// </summary>
        /// <param name="Library">The name of the library to get the handle for.</param>
        /// <returns>The handle to the module.</returns>
        private static IntPtr LowLevelGetModuleHandle(string Library)
        {
            if (IntPtr.Size == 4)
                return GetModuleHandleA(Library);
            IntPtr hModule = IntPtr.Zero;
            Structs.UNICODE_STRING UnicodeString = new Structs.UNICODE_STRING();
            RtlInitUnicodeString(out UnicodeString, Library);
            LdrGetDllHandleEx(0, null, null, UnicodeString, ref hModule);
            return hModule;
        }

        /// <summary>
        /// Gets the address of a specified function using low-level functions.
        /// </summary>
        /// <param name="hModule">The handle to the module.</param>
        /// <param name="Function">The name of the function to get the address for.</param>
        /// <returns>The address of the function.</returns>
        private static IntPtr LowLevelGetProcAddress(IntPtr hModule, string Function)
        {
            if (IntPtr.Size == 4)
                return GetProcAddress(hModule, Function);
            IntPtr FunctionHandle = IntPtr.Zero;
            Structs.UNICODE_STRING UnicodeString = new Structs.UNICODE_STRING();
            Structs.ANSI_STRING AnsiString = new Structs.ANSI_STRING();
            RtlInitUnicodeString(out UnicodeString, Function);
            RtlUnicodeStringToAnsiString(out AnsiString, UnicodeString, true);
            LdrGetProcedureAddressForCaller(hModule, AnsiString, 0, out FunctionHandle, 0, IntPtr.Zero);
            return FunctionHandle;
        }

        /// <summary>
        /// Reads a byte from a specified memory address.
        /// </summary>
        /// <param name="ptr">The memory address to read from.</param>
        /// <returns>The byte read from the memory address.</returns>
        private static unsafe byte InternalReadByte(IntPtr ptr)
        {
            try
            {
                byte* ptr2 = (byte*)(void*)ptr;
                return *ptr2;
            }
            catch
            {

            }
            return 0;
        }

        /// <summary>
        /// Detects hooks on common Windows API functions.
        /// </summary>
        /// <returns>Returns true if hooks are detected, otherwise false.</returns>
        public static bool DetectHooksOnCommonWinAPIFunctions()
        {
            string[] Libraries = { "kernel32.dll", "kernelbase.dll", "ntdll.dll", "user32.dll", "win32u.dll" };
            string[] CommonKernelLibFunctions = { "IsDebuggerPresent", "CheckRemoteDebuggerPresent", "GetThreadContext", "CloseHandle", "OutputDebugStringA", "GetTickCount", "SetHandleInformation" };
            string[] CommonNtdllFunctions = { "NtQueryInformationProcess", "NtSetInformationThread", "NtClose", "NtGetContextThread", "NtQuerySystemInformation", "NtCreateFile", "NtCreateProcess", "NtCreateSection", "NtCreateThread", "NtYieldExecution", "NtCreateUserProcess" };
            string[] CommonUser32Functions = { "FindWindowW", "FindWindowA", "FindWindowExW", "FindWindowExA", "GetForegroundWindow", "GetWindowTextLengthA", "GetWindowTextA", "BlockInput", "CreateWindowExW", "CreateWindowExA" };
            string[] CommonWin32uFunctions = { "NtUserBlockInput", "NtUserFindWindowEx", "NtUserQueryWindow", "NtUserGetForegroundWindow" };
            foreach (string Library in Libraries)
            {
                IntPtr hModule = LowLevelGetModuleHandle(Library);
                if (hModule != IntPtr.Zero)
                {
                    switch (Library)
                    {
                        case "kernel32.dll":
                            {
                                try
                                {
                                    foreach (string WinAPIFunction in CommonKernelLibFunctions)
                                    {
                                        IntPtr Function = LowLevelGetProcAddress(hModule, WinAPIFunction);
                                        byte FunctionByte = InternalReadByte(Function);
                                        if (FunctionByte == 0x90 || FunctionByte == 0xE9)
                                        {
                                            return true;
                                        }
                                    }
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                            break;
                        case "kernelbase.dll":
                            {
                                try
                                {
                                    foreach (string WinAPIFunction in CommonKernelLibFunctions)
                                    {
                                        IntPtr Function = LowLevelGetProcAddress(hModule, WinAPIFunction);
                                        byte FunctionByte = InternalReadByte(Function);
                                        if (FunctionByte == 255 || FunctionByte == 0x90 || FunctionByte == 0xE9)
                                        {
                                            return true;
                                        }
                                    }
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                            break;
                        case "ntdll.dll":
                            {
                                try
                                {
                                    foreach (string WinAPIFunction in CommonNtdllFunctions)
                                    {
                                        IntPtr Function = LowLevelGetProcAddress(hModule, WinAPIFunction);
                                        byte FunctionByte = InternalReadByte(Function);
                                        if (FunctionByte == 255 || FunctionByte == 0x90 || FunctionByte == 0xE9)
                                        {
                                            return true;
                                        }
                                    }
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                            break;
                        case "user32.dll":
                            {
                                try
                                {
                                    foreach (string WinAPIFunction in CommonUser32Functions)
                                    {
                                        IntPtr Function = LowLevelGetProcAddress(hModule, WinAPIFunction);
                                        byte FunctionByte = InternalReadByte(Function);
                                        if (FunctionByte == 0x90 || FunctionByte == 0xE9)
                                        {
                                            return true;
                                        }
                                    }
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                            break;
                        case "win32u.dll":
                            {
                                try
                                {
                                    foreach (string WinAPIFunction in CommonWin32uFunctions)
                                    {
                                        IntPtr Function = LowLevelGetProcAddress(hModule, WinAPIFunction);
                                        byte FunctionByte = InternalReadByte(Function);
                                        if (FunctionByte == 255 || FunctionByte == 0x90 || FunctionByte == 0xE9)
                                        {
                                            return true;
                                        }
                                    }
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                            break;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Detects inline hooks on specified functions within a module.
        /// </summary>
        /// <param name="moduleName">The name of the module to check for hooks.</param>
        /// <param name="functions">The list of functions to check for hooks.</param>
        /// <returns>Returns true if hooks are detected, otherwise false.</returns>
        public static bool DetectInlineHooks(string moduleName, string[] functions)
        {
            if (moduleName != null && functions != null)
            {
                try
                {
                    foreach (string function in functions)
                    {
                        IntPtr hModule = LowLevelGetModuleHandle(moduleName);
                        IntPtr Function = LowLevelGetProcAddress(hModule, function);
                        byte FunctionByte = InternalReadByte(Function);
                        if (FunctionByte == 255 || FunctionByte == 0x90 || FunctionByte == 0xE9)
                        {
                            return true;
                        }
                    }
                }
                catch { }
            }
            return false;
        }

        public static bool IsModule(IntPtr Address)
        {
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                IntPtr Base = module.BaseAddress;
                IntPtr End = IntPtr.Add(Base, module.ModuleMemorySize);
                if (Address.ToInt64() >= Base.ToInt64() && Address.ToInt64() < End.ToInt64())
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Detects hooks in common .NET methods.
        /// </summary>
        /// <returns>Returns true if hooks are detected, otherwise false.</returns>
        public static bool DetectCLRHooks()
        {
            try
            {
                if (IntPtr.Size == 4)
                {
                    MethodInfo[] ProcessMethods = typeof(Process).GetMethods();
                    MethodInfo[] AssemblyMethods = typeof(Assembly).GetMethods();
                    MethodInfo[] FileMethods = typeof(File).GetMethods();
                    MethodInfo[] SocketMethods = typeof(Socket).GetMethods();
                    MethodInfo[] MarshalMethods = typeof(Marshal).GetMethods();
                    MethodInfo[] StringMethods = typeof(string).GetMethods();
                    foreach (MethodInfo ProcessMethod in ProcessMethods)
                    {
                        IntPtr FP = ProcessMethod.MethodHandle.GetFunctionPointer();
                        byte FirstByte = InternalReadByte(FP);
                        if (FirstByte == 0xE9 || FirstByte == 255)
                        {
                            return true;
                        }
                    }

                    foreach (MethodInfo AssemblyMethod in AssemblyMethods)
                    {
                        byte FirstByte = InternalReadByte(AssemblyMethod.MethodHandle.GetFunctionPointer());
                        if (FirstByte == 0xE9 || FirstByte == 255)
                            return true;
                    }

                    foreach (MethodInfo FileMethod in FileMethods)
                    {
                        byte FirstByte = InternalReadByte(FileMethod.MethodHandle.GetFunctionPointer());
                        if (FirstByte == 0xE9 || FirstByte == 255)
                            return true;
                    }

                    foreach (MethodInfo SocketMethod in SocketMethods)
                    {
                        byte FirstByte = InternalReadByte(SocketMethod.MethodHandle.GetFunctionPointer());
                        if (FirstByte == 0xE9 || FirstByte == 255)
                            return true;
                    }

                    foreach (MethodInfo MarshalMethod in MarshalMethods)
                    {
                        byte FirstByte = InternalReadByte(MarshalMethod.MethodHandle.GetFunctionPointer());
                        if (FirstByte == 0xE9 || FirstByte == 255)
                            return true;
                    }

                    foreach (MethodInfo StringMethod in StringMethods)
                    {
                        byte FirstByte = InternalReadByte(StringMethod.MethodHandle.GetFunctionPointer());
                        if (FirstByte == 0xE9 || FirstByte == 255)
                            return true;
                    }

                    Type[] AllTypes = Assembly.GetExecutingAssembly().GetTypes();
                    foreach (Type type in AllTypes)
                    {
                        MethodInfo[] AllMethods = type.GetMethods();
                        foreach (MethodInfo Method in AllMethods)
                        {
                            byte FirstByte = InternalReadByte(Method.MethodHandle.GetFunctionPointer());
                            if (FirstByte == 0xE9 || FirstByte == 255)
                                return true;
                        }
                    }
                }
                else if(IntPtr.Size == 8)
                {
                    MethodInfo[] ProcessMethods = typeof(Process).GetMethods();
                    MethodInfo[] AssemblyMethods = typeof(Assembly).GetMethods();
                    MethodInfo[] FileMethods = typeof(File).GetMethods();
                    MethodInfo[] SocketMethods = typeof(Socket).GetMethods();
                    MethodInfo[] MarshalMethods = typeof(Marshal).GetMethods();
                    MethodInfo[] StringMethods = typeof(string).GetMethods();
                    foreach (MethodInfo ProcessMethod in ProcessMethods)
                    {
                        IntPtr FP = ProcessMethod.MethodHandle.GetFunctionPointer();
                        byte FirstByte = InternalReadByte(FP);
                        if (FirstByte == 0xE9 || FirstByte == 255)
                        {
                            if(IsModule(FP))
                                return true;
                        }
                    }

                    foreach (MethodInfo AssemblyMethod in AssemblyMethods)
                    {
                        IntPtr FP = AssemblyMethod.MethodHandle.GetFunctionPointer();
                        byte FirstByte = InternalReadByte(FP);
                        if (FirstByte == 0xE9 || FirstByte == 255)
                        {
                            if (IsModule(FP))
                                return true;
                        }
                    }

                    foreach (MethodInfo FileMethod in FileMethods)
                    {
                        IntPtr FP = FileMethod.MethodHandle.GetFunctionPointer();
                        byte FirstByte = InternalReadByte(FP);
                        if (FirstByte == 0xE9 || FirstByte == 255)
                        {
                            if (IsModule(FP))
                                return true;
                        }
                    }

                    foreach (MethodInfo SocketMethod in SocketMethods)
                    {
                        IntPtr FP = SocketMethod.MethodHandle.GetFunctionPointer();
                        byte FirstByte = InternalReadByte(FP);
                        if (FirstByte == 0xE9 || FirstByte == 255)
                        {
                            if (IsModule(FP))
                                return true;
                        }
                    }

                    foreach (MethodInfo MarshalMethod in MarshalMethods)
                    {
                        IntPtr FP = MarshalMethod.MethodHandle.GetFunctionPointer();
                        byte FirstByte = InternalReadByte(FP);
                        if (FirstByte == 0xE9 || FirstByte == 255)
                        {
                            if (IsModule(FP))
                                return true;
                        }
                    }

                    foreach (MethodInfo StringMethod in StringMethods)
                    {
                        IntPtr FP = StringMethod.MethodHandle.GetFunctionPointer();
                        byte FirstByte = InternalReadByte(FP);
                        if (FirstByte == 0xE9 || FirstByte == 255)
                        {
                            if (IsModule(FP))
                                return true;
                        }
                    }

                    Type[] AllTypes = Assembly.GetExecutingAssembly().GetTypes();
                    foreach (Type type in AllTypes)
                    {
                        MethodInfo[] AllMethods = type.GetMethods();
                        foreach (MethodInfo Method in AllMethods)
                        {
                            IntPtr FP = Method.MethodHandle.GetFunctionPointer();
                            byte FirstByte = InternalReadByte(FP);
                            if (FirstByte == 0xE9 || FirstByte == 255)
                            {
                                if (IsModule(FP))
                                    return true;
                            }
                        }
                    }
                }
            }
            catch
            {
                
            }
            return false;
        }
    }

}
