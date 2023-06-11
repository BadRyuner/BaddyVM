using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using System;

namespace BaddyVM.VM.Handlers;
internal class Arrays
{
	private static System.Reflection.MethodInfo createarr = typeof(Array).GetMethod("CreateInstance", new[] { typeof(Type), typeof(int) });

	internal static void Handle(VMContext ctx)
	{
		NewArr(ctx);
		PrepareArray(ctx);
	}

	private static void NewArr(VMContext ctx) => ctx.AllocManagedMethod("NewArr").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var result)
		.PopMem(ctx, buf).PopMem(ctx, buf) // pop -> typeof, pop -> size
		.AccessToVMTable(ctx)
		.LoadNumber(ctx.Transform((MetadataMember)ctx.core.module.DefaultImporter.ImportMethod(createarr)))
		.Sum().DerefI() // pop -> createarr function ptr
		.Calli(ctx, 2, true).Save(result).PushMem(ctx, result, buf).RegisterHandler(ctx, VMCodes.NewArr);

	private static void PrepareArray(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("PrepareArray").CilMethodBody.Instructions;
		i.NewLocal(ctx, out var buf)
		.NewLocal(ctx, out var offset)
		.NewLocal(ctx, out var value)
		.NewLocal(ctx, out var arr)
		.NewLocal(ctx, VMTypes.I1, out var switcher)
		.DecodeCode(1).Save(switcher).Load(switcher).IfTrue(() => i.PopMem(ctx, buf).Save(value))
		.PopMem(ctx, buf).Save(offset)
		.PopMem(ctx, buf).Save(arr)
		.Load(arr).Load(offset).DecodeCode(1).Mul().LoadNumber(16).Sum().Sum().Save(offset)
		.PushMem(ctx, offset, buf)
		.Load(switcher).IfTrue(() => i.PushMem(ctx, value, buf))
		.RegisterHandler(ctx, VMCodes.PrepareArr);
	}
}
