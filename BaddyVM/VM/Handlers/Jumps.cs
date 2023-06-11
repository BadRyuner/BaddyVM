using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;

namespace BaddyVM.VM.Handlers;
internal static class Jumps
{
	internal static void Handle(VMContext ctx)
	{
		Br(ctx);
		BrTrue(ctx);
		BrFalse(ctx);
	}

	internal static void Br(VMContext ctx) => ctx.AllocManagedMethod("Br_Handle").CilMethodBody.Instructions
		.NewLocal(ctx, VMTypes.I2, out var buf)
		.DecodeCode(2).Save(buf).SkipCode(buf).RegisterHandler(ctx, VMCodes.Br);

	internal static void BrTrue(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("BrTrue_Handle").CilMethodBody.Instructions
		.NewLocal(ctx, VMTypes.I2, out var offset)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf);
		var skip = new CilInstruction(CilOpCodes.Nop);
		i.Add(CilOpCodes.Brtrue, skip.CreateLabel());
		i.Add(CilOpCodes.Jmp, ctx.Router);
		i.Add(skip);
		i.DecodeCode(2).Save(offset).SkipCode(offset).RegisterHandler(ctx, VMCodes.Brtrue);
	}

	internal static void BrFalse(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("BrFalse_Handle").CilMethodBody.Instructions
		.NewLocal(ctx, VMTypes.I2, out var offset)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf);
		var skip = new CilInstruction(CilOpCodes.Nop);
		i.Add(CilOpCodes.Brfalse, skip.CreateLabel());
		i.Add(CilOpCodes.Jmp, ctx.Router);
		i.Add(skip);
		i.DecodeCode(2).Save(offset).SkipCode(offset).RegisterHandler(ctx, VMCodes.Brfalse);
	}
}
