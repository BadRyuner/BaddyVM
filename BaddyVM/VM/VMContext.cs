using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using BaddyVM.VM.Handlers;
using BaddyVM.VM.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Math = BaddyVM.VM.Handlers.Math;

namespace BaddyVM.VM;
internal class VMContext
{
	internal VMCore core;
	internal VMLayout layout = new();

	internal TypeDefinition VMType;
	internal FieldDefinition VMTable; // TODO: Write init
	internal MethodDefinition Router;
	//internal Dictionary<int, MethodDefinition> Invokers = new(4);
	internal MethodDefinition Invoker;
	internal Dictionary<byte, MethodDefinition> Handlers = new(32);
	internal List<MetadataMember> VMTableContent = new(32);
	internal List<IMethodDefOrRef> SafeCallTargets = new(16);
	internal List<IMethodDescriptor> InterfaceCalls = new(16);
	internal MethodSignature VMSig;

	internal MethodDefinition Allocator;
	internal IMethodDescriptor MemCpy;
	private FieldDefinition MemBuffer;
	private FieldDefinition MemPos;
	private int maxmem = 1024 * 8;

	internal TypeSignature PTR;

	internal int MaxArgs = 0;
	//internal HashSet<int> ObjSizes = new();
	//private Dictionary<uint, ushort> ObjStubs = new(4);
	internal Dictionary<IMethodDefOrRef, ushort> NewObjUnsafeData = new(4);

	internal MetadataMember CreateObject;

	//internal Dictionary<MethodDefinition, DataSegment> FixOffsets = new(32);

	private Dictionary<VMCodes, byte> EncryptedCodesCache = new((int)VMCodes.Max);

	internal VMContext(VMCore core) => this.core = core;

	internal void Init()
	{
		PTR = core.module.CorLibTypeFactory.Int32.MakePointerType();
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
	}

	private void CreateAllocator()
	{
		//  MemoryCopy(void* source, void* destination, long destinationSizeInBytes, long sourceBytesToCopy)
		MemCpy = core.module.DefaultImporter.ImportMethod(typeof(Buffer).GetMethod("MemoryCopy", new[] { typeof(void*), typeof(void*), typeof(long), typeof(long) }));
		MemBuffer = new FieldDefinition("Buffer", FieldAttributes.Private | FieldAttributes.Static, PTR);
		MemPos = new FieldDefinition("MemPos", FieldAttributes.Private | FieldAttributes.Static, PTR);
		VMType.Fields.Add(MemBuffer);
		VMType.Fields.Add(MemPos);
		Allocator = this.AllocManagedMethod("Allocator");
		var alloc = Allocator.CilMethodBody.Instructions.NewLocal(this, out var result);
		//alloc.Arg1().LoadNumber(8).Compare().IfTrue(() => alloc.Arg0().Ret());
		alloc.Arg1().Load(MemPos).Sum().LoadNumber(maxmem).Cgt().IfTrue(() => alloc.LoadNumber(0).Save(MemPos)) // if arg1 + mempos > maxmem then mempos = 0
		.Load(MemBuffer).Load(MemPos).Sum().Save(result) // result = (membuf + mempos)
		.Arg0().Load(result).Arg1().Arg1().Call(MemCpy) // MemBuf(arg0, result, arg1, arg1)
		.Arg1().Load(MemPos).Sum().Save(MemPos) // mempos += sizetoalloc
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

		for (int x = 0; x < VMTableContent.Count; x++)
		{
			var target = VMTableContent[x];
			i.Load(VMTable);
			if (x != 0)
				i.LoadNumber(x*8).Sum();
			if (target is FieldDefinition fd)
			{
				if (!fd.IsPublic)
					ignorechecks.Add(fd.Module.Name);

				if (fd.IsStatic)
					i.Add(CilOpCodes.Ldsflda, fd);
				else
				{
					i.Add(CilOpCodes.Ldtoken, fd);
					i.Save(NoNoNoCLR);
					i.LoadRef(NoNoNoCLR);
					i.Add(CilOpCodes.Callvirt, core.module.DefaultImporter.ImportMethod(typeof(System.RuntimeFieldHandle).GetMethod("get_Value")));
					i.Add(CilOpCodes.Ldc_I4_S, 12);
					i.Add(CilOpCodes.Add);
					i.Add(CilOpCodes.Ldind_U4);
					i.Add(CilOpCodes.Conv_U);
					i.Add(CilOpCodes.Ldc_I4, 0x7FFFFFF);
					i.Add(CilOpCodes.And);
					if (!fd.DeclaringType.IsValueType)
					{
						i.Add(CilOpCodes.Ldc_I4_8);
						i.Add(CilOpCodes.And);
					}
				}
			}
			else if (target is IMethodDescriptor md)
			{
				var resolved = md.Resolve();
				if (!resolved.IsPublic)
					ignorechecks.Add(resolved.Module.Name);

				if (!md.Resolve().IsAbstract)
				{
					if (md.IsImportedInModule(core.module))
						i.Add(CilOpCodes.Ldftn, md); // +_+
					else
						i.Add(CilOpCodes.Ldftn, core.module.DefaultImporter.ImportMethod(md));
				}
				else
					i.Add(CilOpCodes.Ldc_I4_0);
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
		i.Ret();

		foreach(var x in ignorechecks)
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

	private ushort _DFP = ushort.MaxValue;
	internal ushort GetDelegateForPointer()
	{
		if (_DFP != ushort.MaxValue)
			return _DFP;
		return _DFP = Transform((MetadataMember)core.module.DefaultImporter.ImportMethod(
			typeof(Marshal).GetMethod("GetDelegateForFunctionPointer",
				System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public, 
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

	/*
	internal ushort GetObjStub(uint size)
	{
		if (ObjStubs.TryGetValue(size, out var res))
			return res;

		var obj = new TypeDefinition(null, $"Stub{size}", TypeAttributes.Class, core.module.CorLibTypeFactory.Object.ToTypeDefOrRef());
		obj.IsNotPublic = true;
		obj.ClassLayout = new ClassLayout(0, size);
		core.module.TopLevelTypes.Add(obj);
		res = TransformLdtoken(obj);
		ObjStubs.Add(size, res);
		return res;
	}*/
}
