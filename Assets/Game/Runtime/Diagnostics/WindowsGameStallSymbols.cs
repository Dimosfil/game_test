using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace ColonyRTS.Diagnostics
{
    [Serializable]
    internal struct GameStallSymbolEvidence
    {
        public bool Available;
        public string FailureReason,MethodName,PcHex,CodeStartHex,OffsetHex;
        public ulong Pc,CodeStart,Offset;
        public int CodeSize;
    }

    // Explicit own-game diagnostics, called only after the timed sample is inactive.
    // A bounded JIT lookup identifies a sampled method, never its execution duration.
    internal sealed class WindowsGameStallSymbols
    {
        const int MaximumNameBytes=16384;
        readonly uint expectedProcessId,expectedMainThreadId;
        bool initialized;
        internal bool NativeAvailable { get; private set; }
        internal string FailureReason { get; private set; }="Not initialized.";

        internal WindowsGameStallSymbols(uint expectedProcessId,uint expectedMainThreadId)
        {
            if(expectedProcessId==0||expectedMainThreadId==0)
                throw new ArgumentOutOfRangeException(nameof(expectedProcessId),"Explicit own-game identity required.");
            this.expectedProcessId=expectedProcessId;
            this.expectedMainThreadId=expectedMainThreadId;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("kernel32.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]
        static extern IntPtr GetModuleHandleW(string name);
        [DllImport("kernel32.dll",ExactSpelling=true,CharSet=CharSet.Ansi)]
        static extern IntPtr GetProcAddress(IntPtr module,string name);
        [DllImport("kernel32.dll",ExactSpelling=true)]static extern uint GetCurrentProcessId();
        [DllImport("kernel32.dll",ExactSpelling=true)]static extern uint GetCurrentThreadId();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr DomainGet();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr JitFind(IntPtr domain,IntPtr address);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr PointerGet(IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int CodeSizeGet(IntPtr info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr MethodNameGet(IntPtr method,int signature);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Free(IntPtr value);
        DomainGet domainGet;
        JitFind jitFind;
        PointerGet methodGet,codeStartGet;
        CodeSizeGet codeSizeGet;
        MethodNameGet methodNameGet;
        Free free;
        IntPtr domain;

        static T Export<T>(IntPtr module,string name) where T:Delegate
        {
            var address=GetProcAddress(module,name);
            if(address==IntPtr.Zero)throw new EntryPointNotFoundException(name);
            return Marshal.GetDelegateForFunctionPointer<T>(address);
        }
        bool IdentityMatches()=>GetCurrentProcessId()==expectedProcessId&&GetCurrentThreadId()==expectedMainThreadId;
#endif

        void Initialize()
        {
            initialized=true;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if(IntPtr.Size!=8){FailureReason="Windows64 required.";return;}
            try
            {
                if(!IdentityMatches()){FailureReason="Own-game main-thread identity mismatch.";return;}
                // Do not load another runtime or consult paths/symbol servers.
                var module=GetModuleHandleW("mono-2.0-bdwgc.dll");
                if(module==IntPtr.Zero){FailureReason="Loaded Mono runtime unavailable.";return;}
                domainGet=Export<DomainGet>(module,"mono_domain_get");
                jitFind=Export<JitFind>(module,"mono_jit_info_table_find");
                methodGet=Export<PointerGet>(module,"mono_jit_info_get_method");
                codeStartGet=Export<PointerGet>(module,"mono_jit_info_get_code_start");
                codeSizeGet=Export<CodeSizeGet>(module,"mono_jit_info_get_code_size");
                methodNameGet=Export<MethodNameGet>(module,"mono_method_full_name");
                free=Export<Free>(module,"mono_free");
                domain=domainGet();
                if(domain==IntPtr.Zero){FailureReason="Current Mono domain unavailable.";return;}
                NativeAvailable=true;FailureReason=null;
            }
            catch(DllNotFoundException){FailureReason="Windows native API unavailable.";}
            catch(EntryPointNotFoundException e){FailureReason="Required native export unavailable: "+e.Message;}
#else
            FailureReason="Windows diagnostic unavailable on this platform.";
#endif
        }

        internal GameStallSymbolEvidence Lookup(ulong pc)
        {
            var result=new GameStallSymbolEvidence{Pc=pc,PcHex="0x"+pc.ToString("x",CultureInfo.InvariantCulture)};
            if(!initialized)Initialize();
            if(!NativeAvailable){result.FailureReason=FailureReason;return result;}
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if(!IdentityMatches()){result.FailureReason="Own-game main-thread identity mismatch.";return result;}
            if(domainGet()!=domain){result.FailureReason="Mono domain changed; lookup rejected.";return result;}
            if(pc==0||pc>long.MaxValue){result.FailureReason="PC outside supported user address range.";return result;}
            var info=jitFind(domain,new IntPtr((long)pc));
            if(info==IntPtr.Zero){result.FailureReason="No JIT information for PC (native, trampoline or unknown).";return result;}
            var start=unchecked((ulong)codeStartGet(info).ToInt64());
            int size=codeSizeGet(info);
            // Subtraction avoids overflow in a start+size upper-bound calculation.
            if(start==0||size<=0||pc<start||pc-start>=(ulong)size)
            {result.FailureReason="PC outside positive JIT code range.";return result;}
            var method=methodGet(info);
            if(method==IntPtr.Zero){result.FailureReason="JIT method unavailable.";return result;}
            IntPtr name=methodNameGet(method,1);
            if(name==IntPtr.Zero){result.FailureReason="JIT method name unavailable.";return result;}
            try
            {
                int length=0;
                while(length<MaximumNameBytes&&Marshal.ReadByte(name,length)!=0)length++;
                if(length==0||length==MaximumNameBytes)
                {result.FailureReason="JIT method name empty or exceeds diagnostic bound.";return result;}
                var bytes=new byte[length];Marshal.Copy(name,bytes,0,length);
                result.MethodName=new UTF8Encoding(false,true).GetString(bytes);
                result.CodeStart=start;result.CodeSize=size;result.Offset=pc-start;
                result.CodeStartHex="0x"+start.ToString("x",CultureInfo.InvariantCulture);
                result.OffsetHex="0x"+(pc-start).ToString("x",CultureInfo.InvariantCulture);
                result.Available=true;
            }
            catch(DecoderFallbackException){result.FailureReason="JIT method name is not valid UTF-8.";}
            finally{free(name);}
#endif
            return result;
        }
    }
}
