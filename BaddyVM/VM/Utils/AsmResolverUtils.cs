using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Code.Native;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Builder;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using System;
using System.Collections.Generic;
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
				throw new NotImplementedException();
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

	internal static void Virtualize(this VMContext ctx, CilMethodBody body, byte[] vmcode, LocalHeapMap map)
	{
		var native = AllocData(ctx, body.Owner.Name);
		native.Code = vmcode;

		body.Instructions.Clear();
		body.LocalVariables.Clear();
		body.ExceptionHandlers.Clear();
		//body.InitializeLocals = false;
		var i = body.Instructions.NewLocal(ctx, out var stack).NewLocal(ctx, out var data);

		var stacksize = body.MaxStack * 8 + 40; // 40 - overhead for stack safety >_<
		i.LoadNumber(stacksize).Stackalloc().Save(stack); // stack = new[stacksize]

		//var datasize = ctx.layout.VMHeaderEnd + (body.LocalVariables.Count + body.Owner.Parameters.Count) * 8 + 8;
		var datasize = map.maxsize + 8;
		i.LoadNumber(datasize).Stackalloc().Save(data); // data = new[datasize]

		i.Load(data).LoadNumber(ctx.layout.LocalStackHeap).Sum().Load(stack).Set8(); // data[stackoffset] = stack

		int counter = ctx.layout.VMHeaderEnd;
		foreach(var arg in body.Owner.Parameters) // TODO: add support for structs
		{
			//if (arg.ParameterType is ByReferenceTypeSignature)
			//	i.Load(data).LoadNumber(counter).Sum().LoadRef(arg).Set8();
			//else
			i.Load(data).LoadNumber(counter).Sum().Load(arg).Set8(); // data[argoffset] = arg
			counter += 8;
		}

		int methodflags = 0;

		if (!body.Owner.Signature.ReturnsValue)
			methodflags |= ctx.layout.MethodNoRet;

		i.Load(data).LoadNumber(ctx.layout.MethodFlags).Sum().LoadNumber(methodflags).Set8(); // data[methodflagsoffset] = methodflags
		i.Load(data).LoadNumber(ctx.layout.VMTable).Sum().Load(ctx.VMTable).Set8();// data[vmtableoffset] = vmtable

		i.Call(native.Owner) 
		.Load(data)
		.Call(ctx.GetInvoke()).RetSafe(); // ret Invoker(code, data);
		/*
		body.Instructions
			.Call(native.Owner)
			.ForeachArgument(0, (p) => {
				body.Instructions.Load(p);
				if (p.ParameterType.ElementType.Up())
					body.Instructions.Add(CilOpCodes.Conv_I);
			})
			.Call(ctx.GetInvoke(body.Owner))
			.RetSafe();
		*/
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

		i.Add(CilOpCodes.Ldarg_0);
		i.Add(CilOpCodes.Ldarg_1);
		i.Add(CilOpCodes.Call, ctx.Router);
		i.Add(CilOpCodes.Ret);


		/*
		i.NewLocal(ctx, out var mem).NewLocal(ctx, out var nostackhax)
		.LoadNumber(512).Stackalloc().Save(mem) // stack = new[512];
		.LoadNumber(512).Stackalloc().Save(nostackhax)
		.Load(mem).LoadNumber(ctx.layout.LocalStackHeap).Sum().Load(nostackhax).Set8() // stack[localheap] = new[512]
		.Load(mem).LoadNumber(ctx.layout.VMTable).Sum().Load(ctx.VMTable).Set8() // stack[vmtable] = VMRunner.VMTable
		.ForeachArgument(1, (p) => i.Load(mem).LoadNumber(offset).Sum().Load(p).Set8().Inc(ref offset, 8)) // stack[offset] = arg; offset += 8;
		.Load(method.Parameters[0]).Load(mem).Call(ctx.Router) // invoke(code, mem)
		.Ret(); */
	}

	internal static NativeMethodBody AllocData(this VMContext ctx, string name)
	{
		var method = new MethodDefinition(name, MethodAttributes.Static | MethodAttributes.PInvokeImpl | MethodAttributes.Assembly, MethodSignature.CreateStatic(ctx.PTR));
		method.ImplAttributes |= MethodImplAttributes.Native | MethodImplAttributes.Unmanaged | MethodImplAttributes.PreserveSig;
		var body = new NativeMethodBody(method);
		method.NativeMethodBody = body;
		ctx.core.module.GetModuleType().Methods.Add(method);
		return body;
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
}
