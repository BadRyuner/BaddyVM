using BaddyVM.VM.Utils;
using System;

namespace BaddyVM.VM.Handlers;
internal class _Utils
{
	internal static void Handle(VMContext ctx)
	{
		DerefI(ctx);
		DerefI8(ctx);
		DerefI4(ctx);
		DerefI2(ctx);
		DerefI1(ctx);
		SetI(ctx);
		SetI1(ctx);
		SetI2(ctx);
		SetI4(ctx);
		VMTableLoad(ctx);
		SwapStack(ctx);
		Pop(ctx);
		Dup(ctx);
		Eat(ctx);
		Poop(ctx);
	}

	private static void Eat(VMContext ctx) => ctx.AllocManagedMethod("Eat").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(buf).SaveToLocalStorage(ctx, buf)
		.RegisterHandler(ctx, VMCodes.Eat);

	private static void Poop(VMContext ctx) => ctx.AllocManagedMethod("Poop").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var res)
		.LoadFromLocalStorage(ctx).Save(res).PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.Poop);

	private static void Pop(VMContext ctx) => ctx.AllocManagedMethod("Pop").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(buf)
		.RegisterHandler(ctx, VMCodes.Pop);

	private static void Dup(VMContext ctx) => ctx.AllocManagedMethod("Dup").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var r)
		.PopMem(ctx, buf).Save(r).PushMem(ctx, r, buf).PushMem(ctx, r, buf)
		.RegisterHandler(ctx, VMCodes.Dup);

	private static void VMTableLoad(VMContext ctx) => ctx.AllocManagedMethod("VMTableLoad").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var res)
		.AccessToVMTable(ctx).DecodeCode(2).Sum().DerefI().Save(res).PushMem(ctx, res, buf) // push mem[vmtableOffset][code]
		.RegisterHandler(ctx, VMCodes.VMTableLoad);

	private static void SwapStack(VMContext ctx) => ctx.AllocManagedMethod("SwapStack").CilMethodBody.Instructions
		.NewLocal(ctx, out var s1).NewLocal(ctx, out var s2)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(s1).PopMem(ctx, buf).Save(s2)
		.PushMem(ctx, s1, buf).PushMem(ctx, s2, buf)
		.RegisterHandler(ctx, VMCodes.SwapStack);

	private static void DerefI(VMContext ctx) => ctx.AllocManagedMethod("DerefI").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var res)
		.PopMem(ctx, buf).DerefI().Save(res).PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.DerefI);

	private static void DerefI8(VMContext ctx) => ctx.AllocManagedMethod("DerefI8").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var res)
		.PopMem(ctx, buf).DerefI8().Save(res).PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.DerefI8);

	private static void DerefI4(VMContext ctx) => ctx.AllocManagedMethod("DerefI4").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var res)
		.PopMem(ctx, buf).DerefI4().Save(res).PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.DerefI4);

	private static void DerefI2(VMContext ctx) => ctx.AllocManagedMethod("DerefI2").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var res)
		.PopMem(ctx, buf).DerefI2().Save(res).PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.DerefI2);

	private static void DerefI1(VMContext ctx) => ctx.AllocManagedMethod("DerefI1").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var res)
		.PopMem(ctx, buf).DerefI1().Save(res).PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.DerefI1);

	private static void SetI(VMContext ctx) => ctx.AllocManagedMethod("SetI").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var a)
		.PopMem(ctx, buf).Save(a).PopMem(ctx, buf).Load(a).Set8()
		.RegisterHandler(ctx, VMCodes.SetI);

	private static void SetI4(VMContext ctx) => ctx.AllocManagedMethod("SetI4").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var a).NewLocal(ctx, out var b)
		.PopMem(ctx, buf).Save(a).PopMem(ctx, buf).Save(b).Load(b).Load(a).Set4()
		.RegisterHandler(ctx, VMCodes.SetI4);

	private static void SetI2(VMContext ctx) => ctx.AllocManagedMethod("SetI2").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var a)
		.PopMem(ctx, buf).Save(a).PopMem(ctx, buf).Load(a).Set2()
		.RegisterHandler(ctx, VMCodes.SetI2);

	private static void SetI1(VMContext ctx) => ctx.AllocManagedMethod("SetI1").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var a)
		.PopMem(ctx, buf).Save(a).PopMem(ctx, buf).Load(a).Set1()
		.RegisterHandler(ctx, VMCodes.SetI1);
}
