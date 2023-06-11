using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using System;

namespace BaddyVM.VM.Handlers;
internal class Converters
{
	internal static void Handle(VMContext ctx)
	{
		ConvI(ctx);
		ConvI1(ctx);
		ConvI2(ctx);
		ConvI4(ctx);
		ConvI8(ctx);
		ConvU1(ctx);
		ConvU2(ctx);
		ConvU4(ctx);
		ConvU8(ctx);

		Conv_OvfI(ctx);
		Conv_OvfI1(ctx);
		Conv_OvfI2(ctx);
		Conv_OvfI4(ctx);
		Conv_OvfI8(ctx);
		Conv_OvfU1(ctx);
		Conv_OvfU2(ctx);
		Conv_OvfU4(ctx);
		Conv_OvfU8(ctx);

		Conv_OvfI_Un(ctx);
		Conv_OvfI1_Un(ctx);
		Conv_OvfI2_Un(ctx);
		Conv_OvfI4_Un(ctx);
		Conv_OvfI8_Un(ctx);
		Conv_OvfU1_Un(ctx);
		Conv_OvfU2_Un(ctx);
		Conv_OvfU4_Un(ctx);
		Conv_OvfU8_Un(ctx);

		ConvR(ctx);
		ConvR4(ctx);
		ConvR8(ctx);
	}

	private static void ConvR(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("ConvR").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.R8, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_R_Un);
		i.Add(CilOpCodes.Conv_R8);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_R_Un);
	}

	private static void ConvR4(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("ConvR4").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.R4, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_R4);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_R4);
	}
	
	private static void ConvR8(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("ConvR8").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.R8, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_R8);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_R8);
	}

	private static void ConvI(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("ConvI").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.PTR, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_I);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_I);
	}

	private static void ConvI8(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("ConvI8").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I8, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_I8);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_I8);
	}

	private static void ConvI4(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("ConvI4").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I4, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_I4);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_I4);
	}

	private static void ConvI2(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("ConvI2").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I2, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_I2);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_I2);
	}

	private static void ConvI1(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("ConvI1").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I1, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_I1);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_I1);
	}

	private static void ConvU8(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("ConvU8").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.U8, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_U8);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_U8);
	}

	private static void ConvU4(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("ConvU4").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I4, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_U4);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_U4);
	}

	private static void ConvU2(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("ConvI2").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.U2, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_U2);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_U2);
	}

	private static void ConvU1(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("ConvU1").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I1, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_I1);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_U1);
	}

	private static void Conv_OvfI(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfI").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.PTR, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_I);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_I);
	}

	private static void Conv_OvfI8(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfI8").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I8, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_I8);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_I8);
	}

	private static void Conv_OvfI4(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfI4").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I4, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_I4);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_I4);
	}

	private static void Conv_OvfI2(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfI2").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I2, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_I2);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_I2);
	}

	private static void Conv_OvfI1(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfI1").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I1, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_I1);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_I1);
	}

	private static void Conv_OvfU8(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfU8").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.U8, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_U8);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_U8);
	}

	private static void Conv_OvfU4(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfU4").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I4, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_U4);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_U4);
	}

	private static void Conv_OvfU2(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfI2").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.U2, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_U2);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_U2);
	}

	private static void Conv_OvfU1(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfU1").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I1, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_I1);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_U1);
	}

	private static void Conv_OvfI_Un(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfI_Un").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.PTR, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_I_Un);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_I_Un);
	}

	private static void Conv_OvfI8_Un(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfI8_Un").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I8, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_I8_Un);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_I8_Un);
	}

	private static void Conv_OvfI4_Un(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfI4_Un").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I4, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_I4_Un);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_I4_Un);
	}

	private static void Conv_OvfI2_Un(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfI2_Un").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I2, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_I2_Un);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_I2_Un);
	}

	private static void Conv_OvfI1_Un(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfI1_Un").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I1, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_I1_Un);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_I1_Un);
	}

	private static void Conv_OvfU8_Un(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfU8_Un").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.U8, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_U8_Un);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_U8_Un);
	}

	private static void Conv_OvfU4_Un(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfU4_Un").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I4, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_U4_Un);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_U4_Un);
	}

	private static void Conv_OvfU2_Un(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfI2_Un").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.U2, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_U2_Un);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_U2_Un);
	}

	private static void Conv_OvfU1_Un(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Conv_OvfU1_Un").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, VMTypes.I1, out var res)
			.PopMem(ctx, buf);
		i.Add(CilOpCodes.Conv_Ovf_I1_Un);
		i.Save(res).PushMem(ctx, res, buf).RegisterHandler(ctx, VMCodes.Conv_Ovf_U1_Un);
	}
}
