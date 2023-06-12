using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;

namespace BaddyVM.VM.Handlers;
internal static class LoadStore
{
	internal static void Handle(VMContext ctx)
	{
		Load(ctx);
		LoadRef(ctx);
		Store(ctx);
	}

	internal static void Load(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Load").CilMethodBody.Instructions
	    .NewLocal(ctx, out var val)
	    .NewLocal(ctx, out var buf)
	    .Arg1().DecodeCode(2).Sum().Deref8().Save(val).PushMem(ctx, val, buf); 

		i.RegisterHandler(ctx, VMCodes.Load);
	}

	internal static void LoadRef(VMContext ctx) => ctx.AllocManagedMethod("LoadRef").CilMethodBody.Instructions
		.NewLocal(ctx, out var val)
		.NewLocal(ctx, out var buf)
		.Arg1().DecodeCode(2).Sum().Save(val).PushMem(ctx, val, buf)
		.RegisterHandler(ctx, VMCodes.LoadRef);

	internal static void Store(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Store").CilMethodBody.Instructions
		  .NewLocal(ctx, out var buf)
		  .NewLocal(ctx, out var val)
		  .NewLocal(ctx, out var size)
		  .NewLocal(ctx, VMTypes.U1, out var isvt)
		  .NewLocal(ctx, out var adr)
		  .Arg1().DecodeCode(2).Sum().Save(adr).DecodeCode(1).Save(isvt)
		  .PopMem(ctx, buf).Save(val);

		var end = new CilInstruction(CilOpCodes.Nop);

		i.Load(isvt).LoadNumber(1).Compare().IfTrue(() =>
			i.DecodeCode(2).Save(size).Load(val).Load(adr).Load(size).Load(size).Call(ctx.MemCpy).Br(end.CreateLabel())
		);

		i.Load(adr).Load(val).Set8();

		i.Add(end);

		i.RegisterHandler(ctx, VMCodes.Store);
	}
}
