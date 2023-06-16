using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using System;

namespace BaddyVM.VM.Handlers;
internal static class Main
{
	internal static void Handle(VMContext ctx)
	{
		Ret(ctx);

		var i = ctx.Router.CilMethodBody.Instructions.NewLocal(ctx, out var op).DecodeCode(1).Save(op);
		foreach (var handler in ctx.Handlers)
			i.Load(op).LoadNumber(handler.Key).Compare().IfTrue(() => i.Jmp(handler.Value));
		//i.LoadNumber(0);
		//i.Add(CilOpCodes.Br, i[0].CreateLabel());
		i.Jmp(i.Owner.Owner);
	}

	internal static void Ret(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Ret").CilMethodBody.Instructions;
		i.NewLocal(ctx, out var buf)
		.CheckIfNoRet(ctx).IfBranch(() => i.LoadNumber(-1).Ret() /*if no ret*/, () => i.PopMem(ctx, buf).Ret() /*if ret*/)
		.RegisterHandler(ctx, VMCodes.Ret);
	}
}
