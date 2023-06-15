using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using System;
using System.Runtime.InteropServices;

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
		Initblk(ctx);
		Pushback(ctx);
		CreateDelegate(ctx);
	}

	private static void CreateDelegate(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("CreatedDelegate").CilMethodBody.Instructions.NewLocal(ctx, out var buf);
		i.NewLocal(ctx, out var DelForPtr).NewLocal(ctx, out var DelCtor)
		 .NewLocal(ctx, out var thus).NewLocal(ctx, out var fn).NewLocal(ctx, out var type);

		i.PopMem(ctx, buf).Save(DelCtor)
		 .PopMem(ctx, buf).Save(DelForPtr)
		 .PopMem(ctx, buf).Save(type)
		 .PopMem(ctx, buf).Save(fn)
		 .PopMem(ctx, buf).Save(thus);
		var alsoDelegate = DelForPtr;
		i.Load(fn).Load(type).Load(DelForPtr).Calli(ctx, 2, true).Save(alsoDelegate);
		i.Load(thus).LoadNumber(0).Compare().LoadNumber(0).Compare().IfTrue(() => i.Load(alsoDelegate).Load(thus).Load(fn).Load(DelCtor).Calli(ctx, 3, false));
		i.PushMem(ctx, alsoDelegate, buf);
		i.RegisterHandler(ctx, VMCodes.CreateDelegate);
	}

	private static void Pushback(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("PushBack").CilMethodBody.Instructions
		.NewLocal(ctx, out var offset).NewLocal(ctx, out var offsetCopy)
		.NewLocal(ctx, out var target)
		.NewLocal(ctx, out var copy)
		//.NewLocal(ctx, out var pseudostack)
		.NewLocal(ctx, out var buffer);
		/*
		var exit = new CilInstruction(CilOpCodes.Nop);
		i.DecodeCode(2).Dup().Save(offset).Save(offsetCopy)
		.PopMem(ctx, buffer).Save(target)
		.Load(offset).Stackalloc().Save(pseudostack)
		.While(() =>
		{
			i.Load(offset).LoadNumber(0).Compare().IfTrue(() => i.Br(exit.CreateLabel()));
			i.Load(pseudostack).Load(offset).Sum().PopMem(ctx, buffer).Set8();
			i.Load(offset).LoadNumber(8).Sub().Save(offset);
		});
		i.Add(exit);
		i.PushMem(ctx, target, buffer);
		exit = new CilInstruction(CilOpCodes.Nop);
		i.While(() =>
		 {
			 i.Load(offset).Load(offsetCopy).Compare().IfTrue(() => i.Br(exit.CreateLabel()));
			 i.Load(pseudostack).Load(offset).Sum().DerefI().Save(target).PushMem(ctx, target, buffer);
			 i.Load(offset).LoadNumber(8).Sum().Save(offset);
		 });
		i.Add(exit);
		*/

		// broken again AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA

		i.DecodeCode(2).Save(offset);
		i.PeekMem(ctx, target); // target = stack[0]
		i.PeekMem(ctx, offset, copy) // copy = stack[offset]
			.OverrideMem(ctx, offset, target); // xchg stack[offset], target
		i.Load(copy).Save(target); // target = copy

		var exit = new CilInstruction(CilOpCodes.Nop);

		i.While(() =>
		{
			i.Load(offset).LoadNumber(0).Compare().IfTrue(() => i.OverrideMem(ctx, offset, copy).Br(exit.CreateLabel()));
			i.Load(offset).LoadNumber(8).Sub().Save(offset)
				.PeekMem(ctx, offset, copy).OverrideMem(ctx, offset, target)
				.Load(copy).Save(target);
		});

		i.Add(exit);
		i.RegisterHandler(ctx, VMCodes.PushBack);
	}

	private static void Initblk(VMContext ctx) => ctx.AllocManagedMethod("Initblk").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var num).NewLocal(ctx, out var val)
		.PopMem(ctx, buf).Save(num).PopMem(ctx, buf).Save(val).PopMem(ctx, buf)
		.Load(val).Load(num).InitBlk()
		.RegisterHandler(ctx, VMCodes.Initblk);

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
