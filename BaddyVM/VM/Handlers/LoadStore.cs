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

	internal static void Load(VMContext ctx) => ctx.AllocManagedMethod("Load").CilMethodBody.Instructions
		.NewLocal(ctx, out var val)
		.NewLocal(ctx, out var buf)
		.Arg1().DecodeCode(2).Sum().Deref8().Save(val).PushMem(ctx, val, buf)
		.RegisterHandler(ctx, VMCodes.Load);

	internal static void LoadRef(VMContext ctx) => ctx.AllocManagedMethod("LoadRef").CilMethodBody.Instructions
		.NewLocal(ctx, out var val)
		.NewLocal(ctx, out var buf)
		.Arg1().DecodeCode(2).Sum().Save(val).PushMem(ctx, val, buf)
		.RegisterHandler(ctx, VMCodes.LoadRef);

	internal static void Store(VMContext ctx) => ctx.AllocManagedMethod("Store").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf)
		.Arg1().DecodeCode(2).Sum().PopMem(ctx, buf).Set8()
		.RegisterHandler(ctx, VMCodes.Store);
}
