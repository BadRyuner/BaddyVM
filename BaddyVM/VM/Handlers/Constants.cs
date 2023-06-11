using BaddyVM.VM.Utils;

namespace BaddyVM.VM.Handlers;
internal static class Constants
{
	internal static void Handle(VMContext ctx)
	{
		Handle4(ctx);
		Handle8(ctx);
	}

	private static void Handle4(VMContext ctx) =>
		ctx.AllocManagedMethod("Constant_Handle_4").CilMethodBody.Instructions
		.NewLocal(ctx, out var @int)
		.NewLocal(ctx, out var buf)
		.DecodeCode(4).Save(@int).PushMem(ctx, @int, buf).RegisterHandler(ctx, VMCodes.Push4);

	private static void Handle8(VMContext ctx) =>
		ctx.AllocManagedMethod("Constant_Handle_8").CilMethodBody.Instructions
		.NewLocal(ctx, out var @int)
		.NewLocal(ctx, out var buf)
		.DecodeCode(8).Save(@int).PushMem(ctx, @int, buf).RegisterHandler(ctx, VMCodes.Push8);
}
