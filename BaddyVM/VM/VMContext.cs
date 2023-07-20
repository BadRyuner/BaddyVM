using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using BaddyVM.VM.Handlers;
using BaddyVM.VM.Protections;
using BaddyVM.VM.Utils;
using System.Runtime.InteropServices;
using Math = BaddyVM.VM.Handlers.Math;

namespace BaddyVM.VM;
internal class VMContext
{
	internal VMCore core;
	internal VMLayout layout = new();

	internal TypeDefinition VMType;
	internal FieldDefinition VMTable;
	internal MethodDefinition Router;
	//internal Dictionary<int, MethodDefinition> Invokers = new(4);
	internal MethodDefinition Invoker;
	internal Dictionary<byte, MethodDefinition> Handlers = new(32);
	internal List<MetadataMember> VMTableContent = new(32);
	internal List<IMethodDefOrRef> SafeCallTargets = new(16);
	internal List<IMethodDescriptor> InterfaceCalls = new(16);
	internal List<ITypeDefOrRef> TryCathTypes = new(8);
	internal Dictionary<MethodDefinition, MethodDefinition> ProxyToCode;
	internal MethodSignature VMSig;

	internal TypeDefinition RefContainer;
	internal MethodDefinition RCResolver;
	internal MethodDefinition RCNext;

	internal MethodDefinition Allocator; // src, size -> pointer to first byte. If src == 0 then just increase ptr
	internal IMethodDescriptor MemCpy;
	private FieldDefinition MemBuffer;
	private FieldDefinition MemPos;
	private int maxmem = 1024 * 32;

	internal TypeSignature PTR;

	internal int MaxArgs = 0;
	internal Dictionary<IMethodDefOrRef, ushort> NewObjUnsafeData = new(4);

	internal MetadataMember CreateObject;

	//internal Dictionary<MethodDefinition, DataSegment> FixOffsets = new(32);

	private Dictionary<VMCodes, byte> EncryptedCodesCache = new((int)VMCodes.Max);

	internal VMContext(VMCore core) => this.core = core;

	internal void Init()
	{
		_isnet6 = core.module.CorLibTypeFactory.ExtractDotNetRuntimeInfo().Version.Major == 6;

		//PTR = core.module.CorLibTypeFactory.Int32.MakePointerType();
		PTR = core.module.CorLibTypeFactory.Void.MakePointerType();
		VMSig = new MethodSignature(CallingConventionAttributes.Default, PTR, new TypeSignature[] { PTR, PTR });
		VMType = new TypeDefinition(null, "VMRunner", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, core.module.CorLibTypeFactory.Object.ToTypeDefOrRef());
		Router = this.AllocManagedMethod("Router");
		VMTable = new FieldDefinition("VMTable", FieldAttributes.Static | FieldAttributes.Assembly, PTR);
		VMType.Fields.Add(VMTable);
		core.module.TopLevelTypes.Add(VMType);
		layout.Randomize();
		//CreateObject = (MetadataMember)core.module.DefaultImporter.ImportMethod(typeof(Activator).GetMethod("CreateInstance", new[] { typeof(Type) })); 
		CreateObject = (MetadataMember)core.module.DefaultImporter.ImportMethod(typeof(System.Runtime.Serialization.FormatterServices).GetMethod("GetUninitializedObject", new[] { typeof(Type) }));
		CreateAllocator();
		RefContainer = new MemberCloner(core.module).Include(core.ThisModule.TopLevelTypes.First(t => t.Name == "RefContainer")).Clone().ClonedTopLevelTypes.First();
		core.module.TopLevelTypes.Add(RefContainer);
		RefContainer.Name = "a";
		RCResolver = RefContainer.Methods.First(m => m.Name == "Resolve");
		RCNext = RefContainer.Methods.First(m => m.Name == "Next");
		foreach (var mm in RefContainer.Methods)
			if (!mm.IsConstructor)
				mm.Name = "a";

		if (core.ApplyProtections)
			AntiDebug.Register(this);

		Intrinsics.IntrinsicsFactory.Init(this);
	}

	private void CreateAllocator()
	{
		//  MemoryCopy(void* dest, void* source, void* size)
		MemCpy = core.module.DefaultImporter.ImportMethod(typeof(Buffer)
			.GetMethod("__Memmove",
			System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
			new[] { typeof(byte*), typeof(byte*), typeof(nuint)}));

		MemBuffer = new FieldDefinition("Buffer", FieldAttributes.Private | FieldAttributes.Static, PTR);
		MemPos = new FieldDefinition("MemPos", FieldAttributes.Private | FieldAttributes.Static, PTR);
		VMType.Fields.Add(MemBuffer);
		VMType.Fields.Add(MemPos);
		Allocator = this.AllocManagedMethod("Allocator");
		var alloc = Allocator.CilMethodBody.Instructions.NewLocal(this, out var result);
		//alloc.Arg1().LoadNumber(8).Compare().IfTrue(() => alloc.Arg0().Ret());
		alloc.Arg1().Load(MemPos).Sum().LoadNumber(maxmem).Cgt().IfTrue(() => alloc.LoadNumber(0).Save(MemPos)) // if arg1 + mempos > maxmem then mempos = 0
		.Load(MemBuffer).Load(MemPos).Sum().Save(result); // result = (membuf + mempos)
		var skip = new CilInstruction(CilOpCodes.Nop);
		alloc.Arg0().LoadNumber(0).Compare().IfTrue(() => alloc.Br(skip));
		alloc.Load(result).Arg0().Arg1().CallHideOutsideVM(this, MemCpy); // MemCpy(result, arg0, arg1)
		alloc.Add(skip);
		alloc.Arg1().Load(MemPos).Sum().Save(MemPos) // mempos += sizetoalloc
		.Load(result).Ret(); // return result
	}

	internal void Inject()
	{
		Constants.Handle(this);
		LoadStore.Handle(this);
		Objects.Handle(this);
		Jumps.Handle(this);
		Arrays.Handle(this);
		TrrrrrYCATCH.Handle(this);
		Math.Handle(this);
		Logic.Handle(this);
		_Utils.Handle(this);
		Converters.Handle(this);

		Main.Handle(this);

		var attr = new MemberCloner(core.module)
			.Include(ModuleDefinition.FromFile(typeof(VMCore).Assembly.Location).TopLevelTypes.First(t => t.Name == "IgnoresAccessChecksToAttribute"))
			.Clone().ClonedTopLevelTypes.First();

		core.module.TopLevelTypes.Add(attr);

		HashSet<Utf8String> ignorechecks = new();

		var oftype = core.module.DefaultImporter.ImportMethod(typeof(Type).GetMethod("GetTypeFromHandle"));

		var init = VMType.GetOrCreateStaticConstructor();
		var i = init.CilMethodBody.Instructions;
		var NoNoNoCLR = new CilLocalVariable(core.module.DefaultImporter.ImportTypeSignature(typeof(RuntimeFieldHandle)));
		init.CilMethodBody.LocalVariables.Add(NoNoNoCLR);
		i.Clear();
		i.Add(CilInstruction.CreateLdcI4(VMTableContent.Count * 8));
		i.Add(CilOpCodes.Call, core.module.DefaultImporter.ImportMethod(typeof(Marshal).GetMethod("AllocHGlobal", new[] { typeof(int) })));
		i.Add(CilOpCodes.Stsfld, VMTable);
		i.Add(CilInstruction.CreateLdcI4(maxmem));
		i.Add(CilOpCodes.Call, core.module.DefaultImporter.ImportMethod(typeof(Marshal).GetMethod("AllocHGlobal", new[] { typeof(int) })));
		i.Add(CilOpCodes.Stsfld, MemBuffer);
		i.Add(CilInstruction.CreateLdcI4(0));
		i.Add(CilOpCodes.Stsfld, MemPos);

		var rmh = typeof(RuntimeMethodHandle);
		var irmi = typeof(RuntimeMethodHandle).Assembly.GetType("System.IRuntimeMethodInfo");
		var getslot = core.module.DefaultImporter.ImportMethod(rmh.GetMethod("GetSlot", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, new[] { irmi }));
		var methodfromhandle = core.module.DefaultImporter.ImportMethod(typeof(System.Reflection.MethodBase).GetMethod("GetMethodFromHandle", new[] { typeof(RuntimeMethodHandle) }));

		var rutnimemethodinfo = typeof(System.Reflection.MethodInfo).Assembly.GetType("System.Reflection.RuntimeMethodInfo");
		var getSig = core.module.DefaultImporter.ImportMethod(rutnimemethodinfo.GetMethod("get_Signature", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));

		for (int x = 0; x < VMTableContent.Count; x++)
		{
			var target = VMTableContent[x];
			i.Load(VMTable);
			if (x != 0)
				i.LoadNumber(x*8).Sum();
			bool isField = target is MemberReference mr && mr.IsField;
			if (isField == false && target is not MemberReference)
				isField = target is IFieldDescriptor fd;
			if (isField)
			{
				var resolved = ((IFieldDescriptor)target).Resolve();
				if (!resolved.IsPublic)
					ignorechecks.Add(resolved.Module.Name);

				if (resolved.IsStatic)
					i.Add(CilOpCodes.Ldsflda, target);
				else
				{
					i.Add(CilOpCodes.Ldtoken, target);
					i.Save(NoNoNoCLR);
					i.LoadRef(NoNoNoCLR);
					i.Add(CilOpCodes.Callvirt, core.module.DefaultImporter.ImportMethod(typeof(System.RuntimeFieldHandle).GetMethod("get_Value")));
					i.Add(CilOpCodes.Ldc_I4_S, 12);
					i.Add(CilOpCodes.Add);
					i.Add(CilOpCodes.Ldind_U4);
					i.Add(CilOpCodes.Conv_U);
					i.Add(CilOpCodes.Ldc_I4, 0x7FFFFFF);
					i.Add(CilOpCodes.And);
					if (!resolved.DeclaringType.IsValueType)
					{
						i.Add(CilOpCodes.Ldc_I4_8);
						i.Add(CilOpCodes.Add);
					}
				}
			}
			else if (target is IMethodDescriptor md)
			{
				var resolved = md.Resolve();
				if (!resolved.IsPublic)
					ignorechecks.Add(resolved.Module.Name);

				if (!resolved.IsVirtual || resolved.DeclaringType.IsValueType)
				{
					if (md.IsImportedInModule(core.module))
						i.Add(CilOpCodes.Ldftn, md); // +_+
					else
						i.Add(CilOpCodes.Ldftn, core.module.DefaultImporter.ImportMethod(md));
				}
				else
				{
					if (md.IsImportedInModule(core.module))
						i.Add(CilOpCodes.Ldtoken, md); // +_+
					else
						i.Add(CilOpCodes.Ldtoken, core.module.DefaultImporter.ImportMethod(md));
					i.Add(CilOpCodes.Call, methodfromhandle);
					i.Add(CilOpCodes.Call, getslot);
				}
			}
			else if (target is SignatureMember sm)
			{
				i.Add(CilOpCodes.Ldtoken, sm.inner);
				i.Call(methodfromhandle);
				i.Call(getSig);
			}
			else if (target is ITypeDefOrRef td)
			{
				var resolved = td.Resolve();
				if (resolved.IsNotPublic)
					ignorechecks.Add(resolved.Module.Name);

				i.Add(CilOpCodes.Ldtoken, td);
				i.Add(CilOpCodes.Call, oftype);
			}
			else if (target is LdTokenMember ld)
			{
				i.Add(CilOpCodes.Ldtoken, ld.target);
			}
			else
				throw new NotSupportedException();
			i.Set8();
		}

		if (core.ApplyProtections)
			AntiDebug.OnVMCctorBuilded(this);

		i.Ret();

		foreach (var x in ignorechecks)
		{
			core.assembly.CustomAttributes.Add(new CustomAttribute(attr.Methods.First(), new CustomAttributeSignature(new CustomAttributeArgument(core.module.CorLibTypeFactory.String, x.ToString().Replace(".dll", null)))));
		}
#if !DEBUG
		VMType.Name = "a";
#endif
	}

	internal byte EncryptVMCode(VMCodes code)
	{
		if (EncryptedCodesCache.TryGetValue(code, out var encoded))
			return encoded;
#if !DEBUG
		while(true)
		{
			encoded = (byte)Random.Shared.Next(1, byte.MaxValue);
			if (EncryptedCodesCache.Values.Contains(encoded)) continue;
			break;
		}
#else
		encoded = (byte)code;
#endif
		EncryptedCodesCache.Add(code, encoded);
		return encoded;
	}

	internal ushort Transform(MetadataMember member)
	{
		var result = VMTableContent.IndexOf(member);
		if (result == -1)
		{
			result = VMTableContent.Count;
			VMTableContent.Add(member);
		}
		return (ushort)(result * 8); // idea: maybe add random +- 1 ? Hmm
	}

	internal ushort TransformSignature(MetadataMember member)
	{
		var sig = new SignatureMember(member);
		var result = VMTableContent.FindIndex((m) => m is SignatureMember sm && sm.inner == member);
		if (result == -1)
		{
			result = VMTableContent.Count;
			VMTableContent.Add(sig);
		}
		return (ushort)(result * 8);
	}

	internal ushort TransformFieldOffset(IFieldDescriptor member)
	{
		var result = VMTableContent.IndexOf((MetadataMember)member);
		if (result == -1)
		{
			result = VMTableContent.Count;
			VMTableContent.Add((MetadataMember)member);
		}
		return (ushort)(result * 8);
	}

	internal ushort TransformLdtoken(MetadataMember member)
	{
		var ld = new LdTokenMember(member);
		var result = VMTableContent.IndexOf(ld);
		if (result == -1)
		{
			result = VMTableContent.Count;
			VMTableContent.Add(ld);
		}
		return (ushort)(result * 8);
	}

	internal ushort TransformCallInterface(IMethodDescriptor member)
	{
		var result = InterfaceCalls.IndexOf(member);
		if (result == -1)
		{
			result = InterfaceCalls.Count;
			InterfaceCalls.Add(member);
		}
		return (ushort)(result);
	}

	internal byte TransformTryCatch(ITypeDefOrRef member)
	{
		var result = TryCathTypes.IndexOf(member);
		if (result == -1)
		{
			result = TryCathTypes.Count;
			TryCathTypes.Add(member);
		}
		return (byte)(result);
	}

	internal byte GetSafeCallId(ushort idx)
	{
		var method = (IMethodDefOrRef)VMTableContent[idx];
		var ret = SafeCallTargets.IndexOf(method);
		if (ret == -1)
		{
			ret = SafeCallTargets.Count;
			SafeCallTargets.Add(method);
		}
		return (byte)ret;
	}

	internal ushort GetNewObjUnsafeIdx(IMethodDefOrRef method)
	{
		if (NewObjUnsafeData.TryGetValue(method, out var res)) 
			return res;
		res = (ushort)NewObjUnsafeData.Count;
		NewObjUnsafeData.Add(method, res);
		return res;
	}

	private bool _isnet6;
	internal bool IsNet6() => _isnet6;

	internal IMethodDescriptor ImportMethod(System.Reflection.MethodInfo method)
	{
		var ns = method.DeclaringType.Namespace;
		var tn = method.DeclaringType.Name;
		var type = core.module.CorLibTypeFactory.CorLibScope.CreateTypeReference(ns, tn);
		TypeSignature returnType = core.module.DefaultImporter.ImportTypeSignature(method.ReturnType);
		System.Reflection.ParameterInfo[] array = (((object)method.DeclaringType != null && method.DeclaringType.IsConstructedGenericType) ? method.Module.ResolveMethod(method.MetadataToken)!.GetParameters() : method.GetParameters());
		TypeSignature[] array2 = new TypeSignature[array.Length];
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i] = core.module.DefaultImporter.ImportTypeSignature(array[i].ParameterType);
		}

		MethodSignature methodSignature = new MethodSignature((!method.IsStatic) ? CallingConventionAttributes.HasThis : CallingConventionAttributes.Default, returnType, array2);

		return core.module.DefaultImporter.ImportMethod(type.CreateMemberReference(method.Name, methodSignature));
	}

	private ushort _DIA = ushort.MaxValue;
	internal ushort GetDelegateInternalAlloc()
	{
		if (_DIA != ushort.MaxValue)
			return _DIA;
		return _DIA = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(
			typeof(Delegate).GetMethod("InternalAlloc",
				System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)));
	}

	private ushort _DFP = ushort.MaxValue;
	internal ushort GetDelegateForPointer()
	{
		if (_DFP != ushort.MaxValue)
			return _DFP;
		return _DFP = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(
			typeof(Marshal).GetMethod("GetDelegateForFunctionPointerInternal",
				System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, 
				new[] { typeof(IntPtr), typeof(Type) })));
	}

	private ushort _DC = ushort.MaxValue;
	internal ushort GetDelegateCtor()
	{
		if (_DC != ushort.MaxValue)
			return _DC;
		return _DC = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(
			typeof(MulticastDelegate).GetMethod("CtorClosed",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
				new[] { typeof(object), typeof(IntPtr) })));
	}

	private ushort _DSC = ushort.MaxValue;
	internal ushort GetDelegateStaticCtor()
	{
		if (_DSC != ushort.MaxValue)
			return _DSC;
		return _DSC = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(
			typeof(MulticastDelegate).GetMethod("CtorClosedStatic",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
				new[] { typeof(object), typeof(IntPtr) })));
	}

	private ushort _RTHV = ushort.MaxValue;
	/// <summary>
	/// RuntimeTypeHandle.Value
	/// </summary>
	internal ushort _TypeHandleGetValue()
	{
		if (_RTHV != ushort.MaxValue)
			return _RTHV;
		return _RTHV = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(typeof(RuntimeTypeHandle).GetMethod("get_Value")));
	}

	private ushort _TFH = ushort.MaxValue;
	/// <summary>
	/// RuntimeTypeHandle.Value
	/// </summary>
	internal ushort _TypeFromHandle()
	{
		if (_TFH != ushort.MaxValue)
			return _TFH;
		return _TFH = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))));
	}

	private ushort _VFRT = ushort.MaxValue;
	/// <summary>
	/// RuntimeType.m_handle
	/// </summary>
	internal ushort _ValueFieldRuntimeType()
	{
		if (_VFRT != ushort.MaxValue)
			return _VFRT;
		return _VFRT = Transform((MetadataMember)core.module.DefaultImporter.ImportField(
			typeof(RuntimeTypeHandle).Assembly.GetType("System.RuntimeType").GetField("m_handle",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)));
	}

	private ushort _RTH = ushort.MaxValue;
	/// <summary>
	/// RuntimeType.GetUnderlyingNativeHandle()
	/// </summary>
	internal ushort _RuntimeTypeHandle()
	{
		if (_RTH != ushort.MaxValue)
			return _RTH;
		return _RTH = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(
			typeof(RuntimeTypeHandle).Assembly.GetType("System.RuntimeType").GetMethod("GetUnderlyingNativeHandle",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)));
	}

	private ushort _TGT = ushort.MaxValue;
	/// <summary>
	/// Type.GetType(string name)
	/// </summary>
	internal ushort _TypeGetType()
	{
		if (_TGT != ushort.MaxValue)
			return _TGT;
		return _TGT = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(
			typeof(Type).GetMethod("GetType", new[] { typeof(string) })));
	}

	private ushort _IIOA = ushort.MaxValue;
	internal ushort _IsInstanceOfAny()
	{
		if (_IIOA != ushort.MaxValue)
			return _IIOA;
		var type = typeof(Type).Assembly.GetType("System.Runtime.CompilerServices.CastHelpers");
		var method = type.GetMethod("IsInstanceOfAny", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		return _IIOA = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(method));
	}

	private ushort _IIOI = ushort.MaxValue;
	internal ushort _IsInstanceOfInterface()
	{
		if (_IIOI != ushort.MaxValue)
			return _IIOI;
		var type = typeof(Type).Assembly.GetType("System.Runtime.CompilerServices.CastHelpers");
		var method = type.GetMethod("IsInstanceOfInterface", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		return _IIOI = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(method));
	}

	private ushort __ChkCastClass = ushort.MaxValue;
	internal ushort _ChkCastClass()
	{
		if (__ChkCastClass != ushort.MaxValue)
			return __ChkCastClass;
		var type = typeof(Type).Assembly.GetType("System.Runtime.CompilerServices.CastHelpers");
		var method = type.GetMethod("ChkCastClass", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		return __ChkCastClass = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(method));
	}

	private ushort __ChkCastInterface = ushort.MaxValue;
	internal ushort _ChkCastInterface()
	{
		if (__ChkCastInterface != ushort.MaxValue)
			return __ChkCastInterface;
		var type = typeof(Type).Assembly.GetType("System.Runtime.CompilerServices.CastHelpers");
		var method = type.GetMethod("ChkCastInterface", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		return __ChkCastInterface = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(method));
	}

	private ushort _HGAlloc = ushort.MaxValue;
	internal ushort _Alloc()
	{
		if (_HGAlloc != ushort.MaxValue)
			return _HGAlloc;
		var type = typeof(Marshal);
		var method = type.GetMethod(nameof(Marshal.AllocHGlobal), new [] { typeof(int) });
		return _HGAlloc = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(method));
	}
}
