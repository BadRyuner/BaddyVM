using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using BaddyVM.VM.Handlers;
using BaddyVM.VM.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
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
	internal MethodSignature VMSig;

	internal TypeSignature PTR;

	internal int MaxArgs = 0;

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
		i.Clear();
		i.Add(CilInstruction.CreateLdcI4(VMTableContent.Count * 8));
		i.Add(CilOpCodes.Call, core.module.DefaultImporter.ImportMethod(typeof(System.Runtime.InteropServices.Marshal).GetMethod("AllocHGlobal", new[] { typeof(int) })));
		i.Add(CilOpCodes.Stsfld, VMTable);
		for (int x = 0; x < VMTableContent.Count; x++)
		{
			var target = VMTableContent[x];
			i.Load(VMTable);
			if (x != 0)
				i.LoadNumber(x*8).Sum();
			if (target is FieldDefinition fd && fd.IsStatic)
			{
				if (!fd.IsPublic)
					ignorechecks.Add(fd.Module.Name);

				i.Add(CilOpCodes.Ldsflda, fd);
			}
			else if (target is IMethodDescriptor md)
			{
				var resolved = md.Resolve();
				if (!resolved.IsPublic)
					ignorechecks.Add(resolved.Module.Name);

				i.Add(CilOpCodes.Ldftn, core.module.DefaultImporter.ImportMethod(md)); // +_+
			}
			else if (target is ITypeDefOrRef td)
			{
				var resolved = td.Resolve();
				if (resolved.IsNotPublic)
					ignorechecks.Add(resolved.Module.Name);

				i.Add(CilOpCodes.Ldtoken, td);
				i.Add(CilOpCodes.Call, oftype);
			}
			else if (target is LdTokenMember)
				i.Add(CilOpCodes.Ldtoken, target.MetadataToken);
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

	internal ushort TransformLdtoken(MetadataMember member)
	{
		var ld = new LdTokenMember(member.MetadataToken);
		var result = VMTableContent.IndexOf(ld);
		if (result == -1)
		{
			result = VMTableContent.Count;
			VMTableContent.Add(ld);
		}
		return (ushort)(result * 8); // idea: maybe add random +- 1 ? Hmm
	}
}
