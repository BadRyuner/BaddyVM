using BaddyVM.VM.Utils;
using System;

namespace BaddyVM.VM.Handlers;
internal static class Math
{
	internal static void Handle(VMContext ctx)
	{
		Add(ctx);
		Sub(ctx);
		Add_Ovf(ctx);
		Sub_Ovf(ctx);
		Add_Ovf_Un(ctx);
		Sub_Ovf_Un(ctx);
		IMul(ctx);
		UMul(ctx);
		IMul_Ovf(ctx);
		UMul_Ovf(ctx);
		Mul_Ovf_Un(ctx);
		IDiv(ctx);
		UDiv(ctx);
		Rem(ctx);
		RemUn(ctx);

		FAdd(ctx);
		FSub(ctx);
		FMul(ctx);
		FDiv(ctx);
		FRem(ctx);
	}

	private static void FAdd(VMContext ctx)
	{
		ctx.AllocManagedMethod("FAdd").CilMethodBody.Instructions
	   .NewLocal(ctx, VMTypes.R8, out var a)
	   .NewLocal(ctx, VMTypes.R8, out var b)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(b)
	   .PopMem(ctx, buf).Save(a).Load(a).Load(b).Sum().Save(a)
	   .PushMem(ctx, a, buf)
	   .RegisterHandler(ctx, VMCodes.FAdd);
	}

	private static void FSub(VMContext ctx)
	{
		ctx.AllocManagedMethod("FSub").CilMethodBody.Instructions
	   .NewLocal(ctx, VMTypes.R8, out var a)
	   .NewLocal(ctx, VMTypes.R8, out var b)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(b)
	   .PopMem(ctx, buf).Save(a).Load(a).Load(b).Sub().Save(a)
	   .PushMem(ctx, a, buf)
	   .RegisterHandler(ctx, VMCodes.FSub);
	}

	private static void FMul(VMContext ctx)
	{
		ctx.AllocManagedMethod("FMul").CilMethodBody.Instructions
	   .NewLocal(ctx, VMTypes.R8, out var a)
	   .NewLocal(ctx, VMTypes.R8, out var b)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(b)
	   .PopMem(ctx, buf).Save(a).Load(a).Load(b).Mul().Save(a)
	   .PushMem(ctx, a, buf)
	   .RegisterHandler(ctx, VMCodes.FMul);
	}

	private static void FDiv(VMContext ctx)
	{
		ctx.AllocManagedMethod("FDiv").CilMethodBody.Instructions
	   .NewLocal(ctx, VMTypes.R8, out var a)
	   .NewLocal(ctx, VMTypes.R8, out var b)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(b)
	   .PopMem(ctx, buf).Save(a).Load(a).Load(b).Div().Save(a)
	   .PushMem(ctx, a, buf)
	   .RegisterHandler(ctx, VMCodes.FDiv);
	}

	private static void FRem(VMContext ctx)
	{
		ctx.AllocManagedMethod("FRem").CilMethodBody.Instructions
	   .NewLocal(ctx, VMTypes.R8, out var a)
	   .NewLocal(ctx, VMTypes.R8, out var b)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(b)
	   .PopMem(ctx, buf).Save(a).Load(a).Load(b).Rem().Save(a)
	   .PushMem(ctx, a, buf)
	   .RegisterHandler(ctx, VMCodes.FRem);
	}

	private static void Add(VMContext ctx)
	{
		ctx.AllocManagedMethod("Add").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(res)
	   .PopMem(ctx, buf).Load(res).Sum().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Add);
	}

	private static void Sub(VMContext ctx) =>
		ctx.AllocManagedMethod("Sub").CilMethodBody.Instructions
		.NewLocal(ctx, out var res)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(res)
		.PopMem(ctx, buf).Load(res).Sub().Save(res)
		.PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.Sub);

	private static void Add_Ovf(VMContext ctx)
	{
		ctx.AllocManagedMethod("Add_Ovf").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res).NewLocal(ctx, out var deb)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(res)
	   .PopMem(ctx, buf).Save(deb).Load(deb).Load(res).SumOvf().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Add_Ovf);
	}

	private static void Sub_Ovf(VMContext ctx) =>
		ctx.AllocManagedMethod("Sub_Ovf").CilMethodBody.Instructions
		.NewLocal(ctx, out var res)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(res)
		.PopMem(ctx, buf).Load(res).SubOvf().Save(res)
		.PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.Sub_Ovf);

	private static void Add_Ovf_Un(VMContext ctx)
	{
		ctx.AllocManagedMethod("Add_Ovf_Un").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res).NewLocal(ctx, out var deb)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(res)
	   .PopMem(ctx, buf).Save(deb).Load(deb).Load(res).SumOvfUn().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Add_Ovf_Un);
	}

	private static void Sub_Ovf_Un(VMContext ctx) =>
		ctx.AllocManagedMethod("Sub_Ovf_Un").CilMethodBody.Instructions
		.NewLocal(ctx, out var res)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(res)
		.PopMem(ctx, buf).Load(res).SubOvfUn().Save(res)
		.PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.Sub_Ovf_Un);

	private static void IMul(VMContext ctx) =>
		ctx.AllocManagedMethod("IMul").CilMethodBody.Instructions.EnterAntiDebug()
		.NewLocal(ctx, out var res)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(res)
		.PopMem(ctx, buf).Load(res).Mul().Save(res)
		.PushMem(ctx, res, buf)
		.ExitAntiDebug()
		.RegisterHandler(ctx, VMCodes.IMul);

	private static void UMul(VMContext ctx) =>
		ctx.AllocManagedMethod("UMul").CilMethodBody.Instructions
		.NewLocal(ctx, out var res)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(res)
		.PopMem(ctx, buf).AsUnsigned().Load(res).AsUnsigned().Mul().Save(res)
		.PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.UMul);

	private static void IMul_Ovf(VMContext ctx) =>
		ctx.AllocManagedMethod("IMul_Ovf").CilMethodBody.Instructions
		.NewLocal(ctx, out var res)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(res)
		.PopMem(ctx, buf).Load(res).MulOvf().Save(res)
		.PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.IMul_Ovf);

	private static void UMul_Ovf(VMContext ctx) =>
		ctx.AllocManagedMethod("UMul_Ovf").CilMethodBody.Instructions
		.NewLocal(ctx, out var res)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(res)
		.PopMem(ctx, buf).AsUnsigned().Load(res).AsUnsigned().MulOvf().Save(res)
		.PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.UMul_Ovf);

	private static void Mul_Ovf_Un(VMContext ctx) =>
		ctx.AllocManagedMethod("UMul_Ovf").CilMethodBody.Instructions
		.NewLocal(ctx, out var res)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(res)
		.PopMem(ctx, buf).AsUnsigned().Load(res).AsUnsigned().MulOvf_Un().Save(res)
		.PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.Mul_Ovf_Un);

	private static void IDiv(VMContext ctx) =>
		ctx.AllocManagedMethod("IDiv").CilMethodBody.Instructions
		.NewLocal(ctx, out var res)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(res)
		.PopMem(ctx, buf).Load(res).Div().Save(res)
		.PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.IDiv);

	private static void UDiv(VMContext ctx) =>
		ctx.AllocManagedMethod("UDiv").CilMethodBody.Instructions
		.NewLocal(ctx, out var res)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(res)
		.PopMem(ctx, buf).AsUnsigned().Load(res).AsUnsigned().DivUn().Save(res)
		.PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.UDiv);

	private static void Rem(VMContext ctx) =>
		ctx.AllocManagedMethod("Rem").CilMethodBody.Instructions
		.NewLocal(ctx, out var res)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(res)
		.PopMem(ctx, buf).Load(res).Rem().Save(res)
		.PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.Rem);

	private static void RemUn(VMContext ctx) =>
		ctx.AllocManagedMethod("RemUn").CilMethodBody.Instructions
		.NewLocal(ctx, out var res)
		.NewLocal(ctx, out var buf)
		.PopMem(ctx, buf).Save(res)
		.PopMem(ctx, buf).Load(res).RemUn().Save(res)
		.PushMem(ctx, res, buf)
		.RegisterHandler(ctx, VMCodes.Rem_Un);
}
