using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Protections;
using System.Runtime.InteropServices;

namespace BaddyVM.VM.Utils;

internal static class HighLevelMSIL
{
	internal static CilInstructionCollection EnterAntiDebug(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ldc_I4_M1);
		i.Add(CilOpCodes.Ldc_I4_0);
		i.Add(CilOpCodes.Ldc_I4_1);
		i.Sum();
		i.Sum();
		i.Add(CilOpCodes.Ldc_I4_3);
		i.Add(CilOpCodes.Ldc_I4_2);
		return i;
	}

	internal static CilInstructionCollection ExitAntiDebug(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Pop);
		i.Add(CilOpCodes.Pop);
		i.Add(CilOpCodes.Pop);
		return i;
	}

	internal static CilInstructionCollection Ldftn(this CilInstructionCollection i, IMethodDescriptor target)
	{
		i.Add(CilOpCodes.Ldftn, target);
		return i;
	}

	internal static CilInstructionCollection LdftnHideOutsideVM(this CilInstructionCollection i, VMContext ctx, IMethodDescriptor target)
	{
		i.Add(CilOpCodes.Ldsfld, ctx.VMTable);
		i.Add(CilInstruction.CreateLdcI4(ctx.Transform((MetadataMember)target)));
		i.Add(CilOpCodes.Add);
		i.Add(CilOpCodes.Ldind_I);
		return i;
	}

	internal static CilInstructionCollection LdftnHideOutsideVM(this CilInstructionCollection i, VMContext ctx, ushort reserved)
	{
		i.Add(CilOpCodes.Ldsfld, ctx.VMTable);
		i.Add(CilInstruction.CreateLdcI4(reserved));
		i.Add(CilOpCodes.Add);
		i.Add(CilOpCodes.Ldind_I);
		return i;
	}

	internal static void InsertLdftnHide(this CilInstructionCollection i, ref int pos, VMContext ctx, IMethodDescriptor target)
	{
		i.Insert(pos, CilOpCodes.Ldarg_1); pos++;
		i.Insert(pos, CilInstruction.CreateLdcI4(ctx.layout.VMTable)); pos++;
		i.Insert(pos, CilOpCodes.Add); pos++;
		i.Insert(pos, CilOpCodes.Ldind_I); pos++;
		i.Insert(pos, CilInstruction.CreateLdcI4(ctx.Transform((MetadataMember)target))); pos++;
		i.Insert(pos, CilOpCodes.Add); pos++;
		i.Insert(pos, CilOpCodes.Ldind_I); pos++;
	}

	internal static CilInstructionCollection Call(this CilInstructionCollection i, IMethodDescriptor target)
	{
		i.Add(CilOpCodes.Call, target);
		return i;
	}

	internal static CilInstructionCollection CallVirt(this CilInstructionCollection i, IMethodDescriptor target)
	{
		i.Add(CilOpCodes.Callvirt, target);
		return i;
	}

	internal static CilInstructionCollection NewObj(this CilInstructionCollection i, IMethodDescriptor target)
	{
		i.Add(CilOpCodes.Newobj, target);
		return i;
	}

	private static Dictionary<(int, bool), StandAloneSignature> CalliSigsCache = new(32);

	internal static CilInstructionCollection Calli(this CilInstructionCollection i, VMContext ctx, int argscount, bool ret)
	{
		if (CalliSigsCache.TryGetValue((argscount, ret), out var sig)) { }
		else
		{
			var args = new TypeSignature[argscount];
			if (argscount != 0)
				Array.Fill(args, ctx.PTR);
			sig = new StandAloneSignature(new MethodSignature(CallingConventionAttributes.Default, ret ? ctx.PTR : ctx.core.module.CorLibTypeFactory.Void, args));
			CalliSigsCache.Add((argscount, ret), sig);
		}
		i.Add(CilOpCodes.Calli, sig);
		return i;
	}

	internal static CilInstructionCollection Calli(this CilInstructionCollection i, VMContext ctx, int argscount, TypeSignature ret)
	{
		var args = new TypeSignature[argscount];
		if (argscount != 0)
			Array.Fill(args, ctx.PTR);
		var sig = new StandAloneSignature(new MethodSignature(CallingConventionAttributes.Default, ret, args));
		i.Add(CilOpCodes.Calli, sig);
		return i;
	}

	internal static CilInstructionCollection Calli(this CilInstructionCollection i, VMContext ctx, MethodSignature sig)
	{
		var callisig = new MethodSignature(sig.Attributes, sig.ReturnType, sig.ParameterTypes.ToArray());
		if (callisig.HasThis)
		{
			callisig.HasThis = false;
			callisig.ParameterTypes.Insert(0, ctx.PTR);
		}
		if (callisig.ReturnType is GenericInstanceTypeSignature gits)
		{
			callisig.ReturnType = gits.Fix(ctx);
		}
		i.Add(CilOpCodes.Calli, new StandAloneSignature(callisig));
		return i;
	}

	internal static void InsertCallHide(this CilInstructionCollection i, ref int pos, VMContext ctx, IMethodDescriptor md)
	{
		i.Insert(pos, CilOpCodes.Ldarg_1); pos++;
		i.Insert(pos, CilInstruction.CreateLdcI4(ctx.layout.VMTable)); pos++;
		i.Insert(pos, CilOpCodes.Add); pos++;
		i.Insert(pos, CilOpCodes.Ldind_I); pos++;
		i.Insert(pos, CilInstruction.CreateLdcI4(ctx.Transform((MetadataMember)md))); pos++;
		i.Insert(pos, CilOpCodes.Add); pos++;
		i.Insert(pos, CilOpCodes.Ldind_I); pos++;
		i.Insert(pos, CilOpCodes.Calli, new StandAloneSignature(md.Signature)); pos++;
	}

	internal static CilInstructionCollection CallHide(this CilInstructionCollection i, VMContext ctx, IMethodDescriptor md)
	{
		return i.AccessToVMTable(ctx)
			.LoadNumber(ctx.Transform((MetadataMember)md))
			.Sum()
			.DerefI()
			.Calli(ctx, md.Signature.GetTotalParameterCount(), md.Signature.ReturnsValue);
	}

	internal static CilInstructionCollection CallHideOutsideVM(this CilInstructionCollection i, VMContext ctx, IMethodDescriptor md)
	{
		return i.AccessToVMTableOutsideVM(ctx)
			.LoadNumber(ctx.Transform((MetadataMember)md))
			.Sum()
			.DerefI()
			.Calli(ctx, md.Signature.GetTotalParameterCount(), md.Signature.ReturnsValue);
	}

	internal static CilInstructionCollection CallHideOutsideVM(this CilInstructionCollection i, VMContext ctx, IMethodDescriptor md, ushort reserved)
	{
		return i.AccessToVMTableOutsideVM(ctx)
			.LoadNumber(reserved)
			.Sum()
			.DerefI()
			.Calli(ctx, md.Signature.GetTotalParameterCount(), md.Signature.ReturnsValue);
	}

	internal static CilInstructionCollection ForeachArgument(this CilInstructionCollection i, int skip, Action<Parameter> action)
	{
		foreach (var p in i.Owner.Owner.Parameters.Skip(skip))
			action(p);
		return i;
	}

	internal static CilInstructionCollection Inc(this CilInstructionCollection i, ref int target, int number)
	{
		target += number;
		return i;
	}

	internal static CilInstructionCollection IncPtr(this CilInstructionCollection i, CilLocalVariable l)
	{
		return i.Load(l).LoadNumber(8).Sum().Save(l);
	}

	internal static CilInstructionCollection Inc(this CilInstructionCollection i, CilLocalVariable l)
	{
		return i.Load(l).LoadNumber(1).Sum().Save(l);
	}

	internal static CilInstructionCollection Dec(this CilInstructionCollection i, CilLocalVariable l)
	{
		return i.Load(l).LoadNumber(1).Sub().Save(l);
	}

	internal static CilInstructionCollection InitBlk(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Initblk);
		return i;
	}

	internal static CilInstructionCollection Load(this CilInstructionCollection i, CilLocalVariable var)
	{
		i.Add(CilOpCodes.Ldloc_S, var);
		//i.Add(CilOpCodes.Conv_I); // idk why, but ilspy with this thing converts switch => { } to yandere-style code +_+
		return i;
	}

	internal static CilInstructionCollection Load(this CilInstructionCollection i, FieldDefinition var)
	{
		i.Add(CilOpCodes.Ldsfld, var);
		return i;
	}

	internal static CilInstructionCollection Load(this CilInstructionCollection i, Parameter p)
	{
		i.Add(CilOpCodes.Ldarg_S, p);
		return i;
	}

	internal static CilInstructionCollection LoadRef(this CilInstructionCollection i, Parameter p)
	{
		i.Add(CilOpCodes.Ldarga_S, p);
		return i;
	}

	internal static CilInstructionCollection LoadRef(this CilInstructionCollection i, CilLocalVariable p)
	{
		i.Add(CilOpCodes.Ldloca_S, p);
		return i;
	}

	internal static CilInstructionCollection LoadNumber(this CilInstructionCollection i, int number)
	{
		i.Add(CilInstruction.CreateLdcI4(number));
		return i;
	}

	internal static CilInstructionCollection NewLocal(this CilInstructionCollection i, VMContext ctx, out CilLocalVariable var)
	{
		var = new CilLocalVariable(ctx.PTR);
		i.Owner.LocalVariables.Add(var);
		return i;
	}

	internal static CilInstructionCollection NewLocal(this CilInstructionCollection i, TypeSignature t, out CilLocalVariable var)
	{
		var = new CilLocalVariable(t);
		i.Owner.LocalVariables.Add(var);
		return i;
	}

	internal static CilInstructionCollection NewLocal(this CilInstructionCollection i, VMContext ctx, VMTypes type, out CilLocalVariable var)
	{
		var = type switch
		{
			VMTypes.I1 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.SByte),
			VMTypes.U1 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.Byte),
			VMTypes.I2 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.Int16),
			VMTypes.U2 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.UInt16),
			VMTypes.I4 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.Int32),
			VMTypes.U4 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.UInt32),
			VMTypes.I8 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.Int64),
			VMTypes.U8 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.UInt64),
			VMTypes.R4 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.Single),
			VMTypes.R8 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.Double),
			VMTypes.PTR => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.IntPtr),
			_ => throw new NotImplementedException()
		};
		i.Owner.LocalVariables.Add(var);
		return i;
	}

	internal static CilLocalVariable NewLocal(this CilInstructionCollection i, VMContext ctx, VMTypes type)
	{
		var var = type switch
		{
			VMTypes.I1 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.SByte),
			VMTypes.U1 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.Byte),
			VMTypes.I2 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.Int16),
			VMTypes.U2 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.UInt16),
			VMTypes.I4 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.Int32),
			VMTypes.U4 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.UInt32),
			VMTypes.I8 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.Int64),
			VMTypes.U8 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.UInt64),
			VMTypes.R4 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.Single),
			VMTypes.R8 => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.Double),
			VMTypes.PTR => new CilLocalVariable(ctx.core.module.CorLibTypeFactory.IntPtr),
			_ => throw new NotImplementedException()
		};
		i.Owner.LocalVariables.Add(var);
		return var;
	}

	internal static CilInstructionCollection Ret(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ret);
		return i;
	}

	internal static void RetSafe(this CilInstructionCollection i) // unsafe
	{
		if (!i.Owner.Owner.Signature.ReturnsValue)
			i.Add(CilOpCodes.Pop);
		i.Add(CilOpCodes.Ret);
	}

	internal static CilInstructionCollection Dup(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Dup);
		return i;
	}

	internal static CilInstructionCollection Save(this CilInstructionCollection i, CilLocalVariable var)
	{
		i.Add(CilOpCodes.Stloc_S, var);
		return i;
	}

	internal static CilInstructionCollection Save(this CilInstructionCollection i, FieldDefinition var)
	{
		i.Add(CilOpCodes.Stsfld, var);
		return i;
	}

	internal static CilInstructionCollection SaveToLocalStorage(this CilInstructionCollection i, VMContext ctx, CilLocalVariable buffer)
	{
		i.Add(CilOpCodes.Ldarg_1);
		i.LoadNumber(ctx.layout.LocalStorage);
		i.Sum();
		i.Load(buffer);
		i.Add(CilOpCodes.Stind_I);
		return i;
	}

	internal static CilInstructionCollection SaveAndPushAdr(this CilInstructionCollection i, CilLocalVariable var)
	{
		i.Add(CilOpCodes.Stloc_S, var);
		i.Add(CilOpCodes.Ldloca_S, var);
		return i;
	}

	internal static CilInstructionCollection LoadFromLocalStorage(this CilInstructionCollection i, VMContext ctx)
	{
		i.Add(CilOpCodes.Ldarg_1);
		i.LoadNumber(ctx.layout.LocalStorage);
		i.Sum();
		i.Add(CilOpCodes.Ldind_I);
		return i;
	}

	internal static CilInstructionCollection LoadRefFromLocalStorage(this CilInstructionCollection i, VMContext ctx)
	{
		i.Add(CilOpCodes.Ldarg_1);
		i.LoadNumber(ctx.layout.LocalStorage);
		i.Sum();
		return i;
	}

	internal static CilInstructionCollection AccessToVMTable(this CilInstructionCollection i, VMContext ctx)
	{
		i.Add(CilOpCodes.Ldarg_1);
		i.LoadNumber(ctx.layout.VMTable);
		i.Sum();
		i.Add(CilOpCodes.Ldind_I); // out -> vmtable pointer
		return i;
	}

	internal static CilInstructionCollection AccessToVMTableOutsideVM(this CilInstructionCollection i, VMContext ctx)
	{
		i.Add(CilOpCodes.Ldsfld, ctx.VMTable);
		return i;
	}

	internal static CilInstructionCollection GetInstanceID(this CilInstructionCollection i, VMContext ctx)
	{
		i.Add(CilOpCodes.Ldarg_1);
		i.LoadNumber(ctx.layout.InstanceId);
		i.Sum();
		i.Add(CilOpCodes.Ldind_I); // out -> InstanceID
		return i;
	}

	internal static CilInstructionCollection GetRCResovler(this CilInstructionCollection i, VMContext ctx)
	{
		i.Add(CilOpCodes.Ldarg_1);
		i.LoadNumber(ctx.layout.RCResolver);
		i.Sum();
		i.Add(CilOpCodes.Ldind_I); // out -> RCResolver fnptr
		return i;
	}

	internal static CilInstructionCollection While(this CilInstructionCollection i, Action action)
	{
		var head = new CilInstruction(CilOpCodes.Nop);
		i.Add(head);
		action();
		i.Add(CilOpCodes.Br, head.CreateLabel());
		return i;
	}

	internal static CilInstructionCollection LessOrEq(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Cgt);
		i.Add(CilOpCodes.Ldc_I4_0);
		i.Add(CilOpCodes.Ceq);
		return i;
	}

	internal static CilInstructionCollection Br(this CilInstructionCollection i, ICilLabel target)
	{
		i.Add(CilOpCodes.Br, target);
		return i;
	}

	internal static CilInstructionCollection Br(this CilInstructionCollection i, in CilInstruction target)
	{
		i.Add(CilOpCodes.Br, target.CreateLabel());
		return i;
	}

	internal static CilInstructionCollection Set1(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Stind_I1);
		return i;
	}

	internal static CilInstructionCollection Set2(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Stind_I2);
		return i;
	}

	internal static CilInstructionCollection Set4(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Stind_I4);
		return i;
	}

	internal static CilInstructionCollection Set8(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Stind_I);
		return i;
	}

	internal static CilInstructionCollection Deref8(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ldind_I8);
		return i;
	}

	internal static CilInstructionCollection DerefI(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ldind_I);
		return i;
	}

	internal static CilInstructionCollection DerefI8(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ldind_I8);
		return i;
	}

	internal static CilInstructionCollection DerefI4(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ldind_I4);
		return i;
	}

	internal static CilInstructionCollection DerefI2(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ldind_I2);
		return i;
	}

	internal static CilInstructionCollection DerefI1(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ldind_I1);
		return i;
	}

	internal static CilInstructionCollection Stackalloc(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Localloc);
		return i;
	}

	internal static CilInstructionCollection AllocGlobal(this CilInstructionCollection i, VMContext ctx)
	{
		i.Add(CilOpCodes.Call, ctx.core.module.DefaultImporter.ImportMethod(typeof(Marshal).GetMethod("AllocHGlobal", new[] { typeof(int) })));
		return i;
	}

	internal static CilInstructionCollection FreeGlobal(this CilInstructionCollection i, VMContext ctx)
	{
		i.Add(CilOpCodes.Call, ctx.core.module.DefaultImporter.ImportMethod(typeof(Marshal).GetMethod("FreeHGlobal")));
		return i;
	}

	private static IMethodDescriptor allocGlobal = null;
	internal static CilInstructionCollection AllocGlobalHide(this CilInstructionCollection i, VMContext ctx)
	{
		if (allocGlobal == null)
			allocGlobal = ctx.core.module.DefaultImporter.ImportMethod(typeof(Marshal).GetMethod("AllocHGlobal", new[] { typeof(int) }));

		return i.CallHideOutsideVM(ctx, allocGlobal);
	}

	private static IMethodDescriptor freeGlobal = null;
	internal static CilInstructionCollection FreeGlobalHide(this CilInstructionCollection i, VMContext ctx)
	{
		if (freeGlobal == null)
			freeGlobal = ctx.core.module.DefaultImporter.ImportMethod(typeof(Marshal).GetMethod("FreeHGlobal"));

		return i.CallHide(ctx, freeGlobal);
	}

	internal static CilInstructionCollection Sum(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Add);
		return i;
	}
	internal static CilInstructionCollection Sub(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Sub);
		return i;
	}

	internal static CilInstructionCollection SumOvf(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Add_Ovf);
		return i;
	}
	internal static CilInstructionCollection SubOvf(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Sub_Ovf);
		return i;
	}

	internal static CilInstructionCollection SumOvfUn(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Add_Ovf_Un);
		return i;
	}
	internal static CilInstructionCollection SubOvfUn(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Sub_Ovf_Un);
		return i;
	}

	internal static CilInstructionCollection Mul(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Mul);
		return i;
	}

	internal static CilInstructionCollection MulOvf(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Mul_Ovf);
		return i;
	}

	internal static CilInstructionCollection MulOvf_Un(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Mul_Ovf_Un);
		return i;
	}

	internal static CilInstructionCollection Div(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Div);
		return i;
	}

	internal static CilInstructionCollection DivUn(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Div_Un);
		return i;
	}

	internal static CilInstructionCollection Rem(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Rem);
		return i;
	}

	internal static CilInstructionCollection RemUn(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Rem_Un);
		return i;
	}

	internal static CilInstructionCollection Or(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Or);
		return i;
	}

	internal static CilInstructionCollection Xor(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Xor);
		return i;
	}

	internal static CilInstructionCollection And(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.And);
		return i;
	}

	internal static CilInstructionCollection Neg(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Neg);
		return i;
	}

	internal static CilInstructionCollection Not(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Not);
		return i;
	}

	internal static CilInstructionCollection Shl(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Shl);
		return i;
	}

	internal static CilInstructionCollection Shr(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Shr);
		return i;
	}

	internal static CilInstructionCollection ShrUn(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Shr_Un);
		return i;
	}

	internal static CilInstructionCollection AsSigned(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Conv_I8);
		return i;
	}

	internal static CilInstructionCollection AsUnsigned(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Conv_U8);
		return i;
	}

	internal static CilInstructionCollection DecodeCode(this CilInstructionCollection i, int size)
	{
		i.Add(CilOpCodes.Ldarg_0);
		switch(size)
		{
			case 1: i.Add(CilOpCodes.Ldind_U1); break;
			case 2: i.Add(CilOpCodes.Ldind_U2); break;
			case 4: i.Add(CilOpCodes.Ldind_U4); break;
			case 8: i.Add(CilOpCodes.Ldind_I8); break;
			default: throw new NotImplementedException();
		}
		i.SkipCode(size);
		return i;
	}

	internal static CilInstructionCollection DecodeSignedCode(this CilInstructionCollection i, int size)
	{
		i.Add(CilOpCodes.Ldarg_0);
		switch (size)
		{
			case 1: i.Add(CilOpCodes.Ldind_I1); break;
			case 2: i.Add(CilOpCodes.Ldind_I2); break;
			case 4: i.Add(CilOpCodes.Ldind_I4); break;
			case 8: i.Add(CilOpCodes.Ldind_I8); break;
			default: throw new NotImplementedException();
		}
		i.SkipCode(size);
		return i;
	}

	internal static CilInstructionCollection CodePtr(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ldarg_0);
		return i;
	}

	internal static CilInstructionCollection SkipCode(this CilInstructionCollection i, int size)
	{
		i.Add(CilOpCodes.Ldarg_0);
		i.Add(CilInstruction.CreateLdcI4(size));
		i.Sum();
		i.Add(CilOpCodes.Starg_S, i.Owner.Owner.Parameters[0]);
		return i;
	}

	internal static CilInstructionCollection SkipCode(this CilInstructionCollection i, CilLocalVariable var)
	{
		i.Add(CilOpCodes.Ldarg_0);
		i.Load(var);
		i.Sum();
		i.Add(CilOpCodes.Starg_S, i.Owner.Owner.Parameters[0]);
		return i;
	}

	internal static CilInstructionCollection SetCode(this CilInstructionCollection i, CilLocalVariable var)
	{
		i.Add(CilOpCodes.Ldarg_0);
		i.Load(var);
		i.Sum();
		i.Add(CilOpCodes.Starg_S, i.Owner.Owner.Parameters[0]);
		return i;
	}

	internal static CilInstructionCollection OverrideCodePos(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Starg_S, i.Owner.Owner.Parameters[0]);
		return i;
	}

	internal static CilInstructionCollection PushMem(this CilInstructionCollection i, VMContext ctx, CilLocalVariable target, CilLocalVariable buffer)
	{
		return i.LoadLocalStackHeap(ctx)
			.LoadNumber(8).Sum().Save(buffer) // mem = arg1->LocalStack + 8
			.Load(buffer).Load(target).Set8() // *mem = target
			.SaveLocalStackHeap(ctx, buffer); // arg1->LocalStack = mem
	}

	internal static CilInstructionCollection PopMem(this CilInstructionCollection i, VMContext ctx, CilLocalVariable buffer)
	{
		return i.LoadLocalStackHeap(ctx)
			.Save(buffer) // mem = arg1->LocalStack
			.Load(buffer).Deref8() // result = *mem;
			.Load(buffer).DecreasePointer(8).Save(buffer)
			.SaveLocalStackHeap(ctx, buffer); // arg1->LocalStack = (mem - 8)
			// out: result
	}

	internal static CilInstructionCollection PeekMem(this CilInstructionCollection i, VMContext ctx, CilLocalVariable to)
	{
		return i.LoadLocalStackHeap(ctx).Deref8().Save(to); // out = *(mem->LocalStack)
	}

	internal static CilInstructionCollection PeekMem(this CilInstructionCollection i, VMContext ctx, CilLocalVariable at, CilLocalVariable to)
	{
		return i.LoadLocalStackHeap(ctx)
			.Load(at).Sub().Deref8().Save(to); // to = *(mem->LocalStack - at)
	}

	internal static CilInstructionCollection OverrideMem(this CilInstructionCollection i, VMContext ctx, CilLocalVariable at, CilLocalVariable it)
	{
		return i.LoadLocalStackHeap(ctx)
			.Load(at).Sub().Load(it).Set8(); // *(mem->LocalStack - at) = it
	}

	internal static CilInstructionCollection DecreasePointer(this CilInstructionCollection i, int at)
	{
		return i.LoadNumber(at).Sub();
	}

	internal static CilInstructionCollection LoadLocalStackHeap(this CilInstructionCollection i, VMContext ctx)
	{
		i.Add(CilOpCodes.Ldarg_1);
		i.Add(CilInstruction.CreateLdcI4(ctx.layout.LocalStackHeap));
		i.Sum();
		i.Add(CilOpCodes.Ldind_I8);
		return i;
	}

	internal static CilInstructionCollection SaveLocalStackHeap(this CilInstructionCollection i, VMContext ctx, CilLocalVariable buffer)
	{
		i.Add(CilOpCodes.Ldarg_1);
		i.Add(CilInstruction.CreateLdcI4(ctx.layout.LocalStackHeap));
		i.Sum();
		i.Load(buffer);
		i.Set8();
		return i;
	}

	internal static CilInstructionCollection SetJMPBack(this CilInstructionCollection i, VMContext ctx, MethodDefinition @this)
	{
		i.Add(CilOpCodes.Ldarg_1);
		i.Add(CilInstruction.CreateLdcI4(ctx.layout.JMPBack));
		i.Sum();
		i.Ldftn(@this);
		i.Set8();
		return i;
	}

	internal static CilInstructionCollection MoveToGlobalMem(this CilInstructionCollection i, VMContext ctx)
	{
		i.CallHide(ctx, ctx.Allocator);
		return i;
	}

	internal static void RegisterHandler(this CilInstructionCollection i, VMContext ctx, VMCodes code)
	{
		AntiDebug.DoSomeDebugChecks(i, ctx);

		i.Add(CilOpCodes.Jmp, ctx.Router);
		//i.Add(CilOpCodes.Ldarg_0);
		//i.Add(CilOpCodes.Ldarg_1);
		//i.Add(CilOpCodes.Call, ctx.Router);
		//i.Add(CilOpCodes.Ret);
		ctx.Handlers.Add(ctx.EncryptVMCode(code), i.Owner.Owner);
	}

	internal static CilInstructionCollection Pop(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Pop);
		return i;
	}

	internal static CilInstructionCollection Throw(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Throw);
		return i;
	}

	internal static void Jmp(this CilInstructionCollection i, MethodDefinition md)
	{
		i.Add(CilOpCodes.Jmp, md);
	}

	internal static void RegisterHandlerNoJmp(this CilInstructionCollection i, VMContext ctx, VMCodes code)
	{
		ctx.Handlers.Add(ctx.EncryptVMCode(code), i.Owner.Owner);
	}

	internal static CilInstructionCollection Compare(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ceq);
		return i;
	}

	internal static CilInstructionCollection Ceq(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ceq);
		return i;
	}

	internal static CilInstructionCollection Clt(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Clt);
		return i;
	}

	internal static CilInstructionCollection Clt_Un(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Clt_Un);
		return i;
	}

	internal static CilInstructionCollection Cgt(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Cgt);
		return i;
	}

	internal static CilInstructionCollection Cgt_Un(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Cgt_Un);
		return i;
	}

	internal static CilInstructionCollection IfTrue(this CilInstructionCollection i, Action action)
	{
		var nop = new CilInstruction(CilOpCodes.Nop);
		i.Add(CilOpCodes.Brfalse, nop.CreateLabel());
		action();
		i.Add(nop);
		return i;
	}

	internal static CilInstructionCollection IfBranch(this CilInstructionCollection i, Action tru, Action fals)
	{
		var nop = new CilInstruction(CilOpCodes.Nop);
		var nooooooooooooop = new CilInstruction(CilOpCodes.Nop);
		i.Add(CilOpCodes.Brfalse, nooooooooooooop.CreateLabel());
		tru();
		i.Add(CilOpCodes.Br, nop.CreateLabel());
		i.Add(nooooooooooooop);
		fals();
		i.Add(nop);
		return i;
	}

	internal static CilInstructionCollection DebugOutput(this CilInstructionCollection i, string str)
	{
#if DEBUG
		i.Add(CilOpCodes.Ldstr, str);
		i.Add(CilOpCodes.Pop);
#endif
		return i;
	}

	internal static CilInstructionCollection GetFlags(this CilInstructionCollection i, VMContext ctx)
	{
		i.Add(CilOpCodes.Ldarg_1);
		i.Add(CilInstruction.CreateLdcI4(ctx.layout.MethodFlags));
		return i.Sum().DerefI();
	}

	internal static CilInstructionCollection CheckIfNoRet(this CilInstructionCollection i, VMContext ctx)
	{
		i.GetFlags(ctx);
		i.Add(CilInstruction.CreateLdcI4(ctx.layout.MethodNoRet));
		i.And();
		i.Add(CilInstruction.CreateLdcI4(ctx.layout.MethodNoRet));
		return i.Compare();
	}

	internal static CilInstructionCollection Arg1(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ldarg_1);
		return i;
	}

	internal static CilInstructionCollection Arg0(this CilInstructionCollection i)
	{
		i.Add(CilOpCodes.Ldarg_0);
		return i;
	}
}