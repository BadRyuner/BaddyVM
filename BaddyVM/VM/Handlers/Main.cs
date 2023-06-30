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
		foreach (var handler in ctx.Handlers.OrderBy(x => Random.Shared.Next()))
		{
			if (handler.Value.NativeMethodBody != null)
				i.SetJMPBack(ctx, i.Owner.Owner);
			i.Load(op).LoadNumber(handler.Key).Compare().IfTrue(() => i.Jmp(handler.Value));
		}
		i.Jmp(i.Owner.Owner);
	}

	internal static void Ret(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Ret").CilMethodBody.Instructions;
		i.NewLocal(ctx, out var buf);

		i.GetInstanceID(ctx).LoadNumber(0).LoadNumber(1).GetRCResovler(ctx).Calli(ctx, 3, false); // flush all gchandles

		i.CheckIfNoRet(ctx).IfBranch(
			() => /*if no ret*/
			{
				i.LoadNumber(-1)
				//.LoadLocalStackHeap(ctx).FreeGlobalHide(ctx).Arg1().FreeGlobalHide(ctx)
				.Ret();
			},
			() => /*if ret*/
			{
				i.PopMem(ctx, buf); // get result
				i.DecodeCode(2).Save(buf); // get size
				i.Load(buf).LoadNumber(0).Compare().IfBranch(() =>
				{
					//.LoadLocalStackHeap(ctx).FreeGlobalHide(ctx).Arg1().FreeGlobalHide(ctx); // free vm local method instance
					i.Ret(); // return 1/2/4/8 byte-sized values
				}, 
				() =>
				{
					i.Load(buf)
					.MoveToGlobalMem(ctx) // move to global mem
					//.LoadLocalStackHeap(ctx).FreeGlobalHide(ctx).Arg1().FreeGlobalHide(ctx) // free vm local method instance
					.Ret(); // return ptr to global mem
				});
			})
		.RegisterHandler(ctx, VMCodes.Ret);
	}
}
