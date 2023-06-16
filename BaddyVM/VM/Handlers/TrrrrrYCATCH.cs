using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using System;

namespace BaddyVM.VM.Handlers;
internal class TrrrrrYCATCH
{
	internal static void Handle(VMContext ctx)
	{
		FinTry(ctx);
		Leave(ctx);
		NoRet(ctx);
	}

	private static void FinTry(VMContext ctx)
	{
		var b = ctx.AllocManagedMethod("FinTry").CilMethodBody;
		var i = b.Instructions;
		i.NewLocal(ctx, VMTypes.I1, out var ok)
		 .NewLocal(ctx, out var next)
		 .NewLocal(ctx, out var finallybegin);

		var jmp = new CilInstruction(CilOpCodes.Nop);
		var finstart = new CilInstruction(CilOpCodes.Nop);
		var finend = new CilInstruction(CilOpCodes.Nop);
		var trystart = new CilInstruction(CilOpCodes.Nop);
		var handler = new CilExceptionHandler() { 
			HandlerType = CilExceptionHandlerType.Finally,
			TryStart = trystart.CreateLabel(),
			TryEnd = finstart.CreateLabel(),
			HandlerStart = finstart.CreateLabel(),
			HandlerEnd = finend.CreateLabel()
		};

		i.DecodeSignedCode(2).CodePtr().Sum().Save(finallybegin);
		i.LoadNumber(0).Save(ok);
		i.LoadNumber(0).Save(next);

		i.Add(trystart);
		i.CodePtr();
		i.Arg1();
		i.Call(ctx.Router);
		i.Save(next);
		i.LoadNumber(1).Save(ok);
		i.Add(CilOpCodes.Leave_S, jmp.CreateLabel());
		i.Add(finstart);
		i.Load(finallybegin);
		i.Arg1();
		i.Call(ctx.Router);
		i.Add(CilOpCodes.Pop);
		i.Add(CilOpCodes.Endfinally);
		i.Add(finend);

		i.Add(jmp);
		i.Load(ok).LoadNumber(1).Compare().IfTrue(() => i.Load(next).OverrideCodePos());
		b.ExceptionHandlers.Add(handler);
		i.RegisterHandler(ctx, VMCodes.FinTry);
	}

	private static void Leave(VMContext ctx) => ctx.AllocManagedMethod("Leave").CilMethodBody.Instructions
		.NewLocal(ctx, out var what).DecodeSignedCode(2).CodePtr().Sum().Ret()
		.RegisterHandlerNoJmp(ctx, VMCodes.Leave);

	private static void NoRet(VMContext ctx) => ctx.AllocManagedMethod("NoRet").CilMethodBody.Instructions
		.LoadNumber(0).Ret()
		.RegisterHandlerNoJmp(ctx, VMCodes.NoRet);
}
