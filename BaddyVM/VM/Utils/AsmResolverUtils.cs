using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Code.Native;
using AsmResolver.DotNet.Memory;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Builder;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BaddyVM.VM.Utils;
internal static class AsmResolverUtils
{
	internal static VMTypes ToVMTypes(this ElementType e)
	{
		switch (e)
		{
			case ElementType.None:
			case ElementType.Void:
				return VMTypes.None;
			case ElementType.I1:
			case ElementType.Boolean:
				return VMTypes.I1;
			case ElementType.Char:
				return VMTypes.U2;
			case ElementType.U1:
				return VMTypes.U1;
			case ElementType.I2:
				return VMTypes.I2;
			case ElementType.U2:
				return VMTypes.U2;
			case ElementType.I4:
				return VMTypes.I4;
			case ElementType.U4:
				return VMTypes.U4;
			case ElementType.I8:
				return VMTypes.I8;
			case ElementType.U8:
				return VMTypes.U8;
			case ElementType.R4:
				return VMTypes.R4;
			case ElementType.R8:
				return VMTypes.R8;
			case ElementType.String:
				return VMTypes.STR;
			case ElementType.Ptr:
			case ElementType.ByRef:
			case ElementType.I:
			case ElementType.U:
			case ElementType.ValueType:
			case ElementType.Class:
			case ElementType.Sentinel:
			case ElementType.Pinned:
			case ElementType.Type:
			case ElementType.Boxed:
			case ElementType.Array:
			case ElementType.SzArray:
			case ElementType.TypedByRef:
			case ElementType.FnPtr:
			case ElementType.Object:
			case ElementType.GenericInst:
				return VMTypes.PTR;
			case ElementType.MVar:
			case ElementType.CModReqD:
			case ElementType.CModOpt:
			case ElementType.Internal:
			case ElementType.Enum:
			case ElementType.Modifier:
			default:
				return VMTypes.PTR;
		}
	}

	internal static bool Up(this ElementType e)
	{
		switch (e)
		{
			case ElementType.None:
			case ElementType.Void:
			case ElementType.Boolean:
			case ElementType.Char:
			case ElementType.I1:
			case ElementType.I2:
			case ElementType.I4:
			case ElementType.String:
			case ElementType.ByRef:
			case ElementType.ValueType:
			case ElementType.Class:
			case ElementType.Var:
			case ElementType.Array:
			case ElementType.GenericInst:
			case ElementType.TypedByRef:
			case ElementType.FnPtr:
			case ElementType.Object:
			case ElementType.SzArray:
			case ElementType.MVar:
			case ElementType.CModReqD:
			case ElementType.CModOpt:
			case ElementType.Internal:
			case ElementType.Modifier:
			case ElementType.Sentinel:
			case ElementType.Pinned:
			case ElementType.Type:
			case ElementType.Boxed:
			case ElementType.Enum:
				return true;
			case ElementType.I:
			case ElementType.U:
			case ElementType.I8:
			case ElementType.U8:
			case ElementType.U4:
			case ElementType.Ptr:
			case ElementType.U1:
			case ElementType.U2:
			case ElementType.R4:
			case ElementType.R8:
			default:
				return false;
		}
	}

	internal static bool IsStruct(this TypeSignature sig)
	{
		if (sig.ElementType == ElementType.Var || sig.ElementType == ElementType.MVar) return false;
		if (sig.ElementType == ElementType.SzArray) return false;
		if (sig is ByReferenceTypeSignature || sig is PointerTypeSignature) return false;

		var resolved = sig.Resolve();
        if (resolved.BaseType == null) return false;

        if (resolved.BaseType.IsTypeOf("System", "ValueType"))
		{
			switch(sig.ElementType)
			{
				case ElementType.Void:
				case ElementType.Boolean:
				case ElementType.Char:
				case ElementType.I1:
				case ElementType.U1:
				case ElementType.I2:
				case ElementType.U2:
				case ElementType.I4:
				case ElementType.U4:
				case ElementType.I8:
				case ElementType.U8:
				case ElementType.U:
				case ElementType.I:
				case ElementType.R4:
				case ElementType.R8:
				case ElementType.Ptr:
				case ElementType.FnPtr:
				case ElementType.SzArray:
				case ElementType.Array:
					return false;
				default:
					return true;
			}
		}
		return false;
	}

	internal static void Virtualize(this VMContext ctx, CilMethodBody body, byte[] vmcode, LocalHeapMap map)
	{
		var native = ctx.ProxyToCode[body.Owner].NativeMethodBody;
		native.Code = vmcode;

		body.Instructions.Clear();
		body.LocalVariables.Clear();
		body.ExceptionHandlers.Clear();
		//body.InitializeLocals = false;
		var i = body.Instructions.NewLocal(ctx, out var stack).NewLocal(ctx, out var data);

		var stacksize = body.MaxStack * 8 + 160; // 160 - overhead for stack safety >_<

		i.LoadNumber(stacksize).AllocGlobalHide(ctx).Save(stack); // stack = new[stacksize]

		var datasize = map.maxsize + 16;

		i.LoadNumber(datasize).AllocGlobalHide(ctx).Save(data); // data = new[datasize]

		i.Load(data).LoadNumber(ctx.layout.LocalStackHeap).Sum().Load(stack).Set8(); // data[stackoffset] = stack

		i.Load(data).LoadNumber(ctx.layout.InstanceId).Sum().CallHideOutsideVM(ctx, ctx.RCNext).Set8();
		i.Load(data).LoadNumber(ctx.layout.RCResolver).Sum().LdftnHideOutsideVM(ctx, ctx.RCResolver).Set8();

		int counter = ctx.layout.VMHeaderEnd;
		if (!body.Owner.IsStatic)
		{
			i.Load(data).LoadNumber(counter).Sum();
			i.Add(CilOpCodes.Ldarg_0);
			i.Set8();
			counter += 8;
		}

		foreach(var arg in body.Owner.Parameters)
		{
			i.Load(data).LoadNumber(counter).Sum();

			if (arg.ParameterType.IsStruct()) 
				i.LoadRef(arg)
				.LoadNumber((int)arg.ParameterType.GetImpliedMemoryLayout(false).Size)
				.CallHideOutsideVM(ctx, ctx.Allocator);
			else 
				i.Load(arg);

			i.Set8(); // data[argoffset] = arg
			counter += 8;
		}

		int methodflags = 0;

		if (!body.Owner.Signature.ReturnsValue)
			methodflags |= ctx.layout.MethodNoRet;

		i.Load(data).LoadNumber(ctx.layout.MethodFlags).Sum().LoadNumber(methodflags).Set8(); // data[methodflagsoffset] = methodflags
		i.Load(data).LoadNumber(ctx.layout.VMTable).Sum().Load(ctx.VMTable).Set8();// data[vmtableoffset] = vmtable

		i.CallHideOutsideVM(ctx, native.Owner) 
		.Load(data)
		.CallHideOutsideVM(ctx, ctx.GetInvoke()); // Invoker(code, data);

		if (body.Owner.Signature.ReturnType.IsStruct())
			i.Add(CilOpCodes.Ldobj, body.Owner.Signature.ReturnType.ToTypeDefOrRef());

		i.RetSafe();
	}

	internal static MethodDefinition GetInvoke(this VMContext ctx)
	{
		if (ctx.Invoker == null)
		{
			ctx.Invoker = AllocInvokerMethod(ctx, 2);
			GenerateInvoke(ctx.Invoker, ctx);
		}
		return ctx.Invoker;
	}

	internal static void GenerateInvoke(MethodDefinition method, VMContext ctx)
	{
		var i = method.CilMethodBody.Instructions;
		i.Clear();
		i.Add(CilOpCodes.Jmp, ctx.Router);
	}

	internal static MethodDefinition AllocData(this VMContext ctx, string name)
	{
#if !DEBUG
		name = "a";
#endif
		var method = new MethodDefinition(name, MethodAttributes.Static | MethodAttributes.PInvokeImpl | MethodAttributes.Assembly, MethodSignature.CreateStatic(ctx.PTR));
		method.ImplAttributes |= MethodImplAttributes.Native | MethodImplAttributes.Unmanaged | MethodImplAttributes.PreserveSig;
		var body = new NativeMethodBody(method);
		method.NativeMethodBody = body;
		ctx.core.module.GetModuleType().Methods.Add(method);
		return method;
	}

	internal static NativeMethodBody AllocNativeMethod(this VMContext ctx, string name)
	{
#if !DEBUG
		name = "a";
#endif
		var method = new MethodDefinition(name, MethodAttributes.Static | MethodAttributes.PInvokeImpl | MethodAttributes.Assembly, ctx.VMSig);
		method.ImplAttributes |= MethodImplAttributes.Native | MethodImplAttributes.Unmanaged | MethodImplAttributes.PreserveSig;
		var body = new NativeMethodBody(method);
		method.NativeMethodBody = body;
		ctx.core.module.GetModuleType().Methods.Add(method);
		return body;
	}

	internal static NativeMethodBody AllocNativeMethod(this VMContext ctx, string name, MethodSignature sig)
	{
#if !DEBUG
		name = "a";
#endif
		var method = new MethodDefinition(name, MethodAttributes.Static | MethodAttributes.PInvokeImpl | MethodAttributes.Assembly, sig);
		method.ImplAttributes |= MethodImplAttributes.Native | MethodImplAttributes.Unmanaged | MethodImplAttributes.PreserveSig;
		var body = new NativeMethodBody(method);
		method.NativeMethodBody = body;
		ctx.core.module.GetModuleType().Methods.Add(method);
		return body;
	}

	internal static MethodDefinition AllocManagedMethod(this VMContext ctx, string name)
	{
#if !DEBUG
		name = "a";
#endif
		var method = new MethodDefinition(name, MethodAttributes.Static | MethodAttributes.Assembly, ctx.VMSig);
		var body = new CilMethodBody(method);
		method.CilMethodBody = body;
		//method.CilMethodBody.InitializeLocals = false;
		ctx.VMType.Methods.Add(method);
		return method;
	}

	private static MethodDefinition AllocInvokerMethod(this VMContext ctx, int argscount)
	{
		var arr = new TypeSignature[argscount];
		Array.Fill(arr, ctx.PTR);
		var method = new MethodDefinition("a", MethodAttributes.Static | MethodAttributes.Assembly, MethodSignature.CreateStatic(ctx.PTR, arr));
		var body = new CilMethodBody(method);
		method.CilMethodBody = body;
		ctx.VMType.Methods.Add(method);
		return method;
	}

	internal static byte CalcFloatByte(this IMethodDescriptor md)
	{
		int res = 0;
		var p = md.Signature.ParameterTypes;

		if (p.Count > 0)
			res |= p[0].ElementType.IsFloat();
		if (p.Count > 1)
			res |= p[1].ElementType.IsFloat() << 1;
		if (p.Count > 2)
			res |= p[2].ElementType.IsFloat() << 2;
		if (p.Count > 3)
			res |= p[3].ElementType.IsFloat() << 3;

		return (byte)res;
	}

	private static byte IsFloat(this ElementType t)
	{
		switch(t)
		{
			case ElementType.R4:
			case ElementType.R8:
				return 1;
			default:
				return 0;
		}
	}
}
