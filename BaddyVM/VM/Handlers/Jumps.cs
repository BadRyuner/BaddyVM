using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;

namespace BaddyVM.VM.Handlers;
internal static class Jumps
{
	internal static void Handle(VMContext ctx)
	{
		Jmp(ctx);
		Br(ctx);
		BrTrue(ctx);
		BrFalse(ctx);
		Switch(ctx);
	}

	internal static void Jmp(VMContext ctx) => ctx.AllocManagedMethod("Jmp").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).OverrideCodePos()
		.RegisterHandler(ctx, VMCodes.Jmp);

	internal static void Br(VMContext ctx) => ctx.AllocManagedMethod("Br_Handle").CilMethodBody.Instructions
		.NewLocal(ctx, VMTypes.I2, out var buf)
		.DecodeSignedCode(2).Save(buf).SkipCode(buf).RegisterHandler(ctx, VMCodes.Br);

	internal static void BrTrue(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("BrTrue_Handle").CilMethodBody.Instructions
		.NewLocal(ctx, VMTypes.I2, out var offset)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf);
		var skip = new CilInstruction(CilOpCodes.Nop);

		i.DecodeSignedCode(2).Save(offset);
		i.Add(CilOpCodes.Brtrue, skip.CreateLabel());
		i.Add(CilOpCodes.Jmp, ctx.Router);
		i.Add(skip);
		i.SkipCode(offset).RegisterHandler(ctx, VMCodes.Brtrue);
	}

	internal static void BrFalse(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("BrFalse_Handle").CilMethodBody.Instructions
		.NewLocal(ctx, VMTypes.I2, out var offset)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf);
		var skip = new CilInstruction(CilOpCodes.Nop);

		i.DecodeSignedCode(2).Save(offset);
		i.Add(CilOpCodes.Brfalse, skip.CreateLabel());
		i.Add(CilOpCodes.Jmp, ctx.Router);
		i.Add(skip);
		i.SkipCode(offset).RegisterHandler(ctx, VMCodes.Brfalse);
	}

	internal static void Switch(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Switch").CilMethodBody.Instructions;
		i.NewLocal(ctx, out var id).NewLocal(ctx, out var length);

		i.PopMem(ctx, id).Save(id);
		i.DecodeCode(4).Save(length);

		var suc = new CilInstruction(CilOpCodes.Nop);

		i.Load(id).Load(length).LessOrEq().IfTrue(() =>
		{
			var offset = length;
			i.CodePtr().LoadNumber(2).Load(id).Mul().Sum().OverrideCodePos();
			i.CodePtr().DecodeCode(2).Sum().OverrideCodePos();
			i.Br(suc.CreateLabel());
		});

		i.CodePtr().Load(length).LoadNumber(1).Sum().LoadNumber(2).Mul().Sum().OverrideCodePos();

		i.Add(suc);
		i.RegisterHandler(ctx, VMCodes.Switch);
	}
}
