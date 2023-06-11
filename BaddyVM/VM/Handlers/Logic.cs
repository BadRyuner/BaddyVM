using BaddyVM.VM.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaddyVM.VM.Handlers;
internal class Logic
{
	internal static void Handle(VMContext ctx)
	{
		Xor(ctx);
		And(ctx);
		Or(ctx);
		Not(ctx);
		Neg(ctx);
		Shl(ctx); 
		Shr(ctx);
		ShrUn(ctx);
		Ceq(ctx);
		Cgt(ctx);
		Cgt_Un(ctx);
		Clt(ctx);
		Clt_Un(ctx);
	}

	private static void Xor(VMContext ctx)
	{
		ctx.AllocManagedMethod("Xor").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf)
	   .PopMem(ctx, buf).Xor().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Xor);
	}

	private static void And(VMContext ctx)
	{
		ctx.AllocManagedMethod("And").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf)
	   .PopMem(ctx, buf).And().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.And);
	}

	private static void Or(VMContext ctx)
	{
		ctx.AllocManagedMethod("Or").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf)
	   .PopMem(ctx, buf).Or().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Or);
	}

	private static void Not(VMContext ctx)
	{
		ctx.AllocManagedMethod("Not").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Not().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Not);
	}

	private static void Neg(VMContext ctx)
	{
		ctx.AllocManagedMethod("Neg").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Neg().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Neg);
	}

	private static void Shl(VMContext ctx)
	{
		ctx.AllocManagedMethod("Shl").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(res)
	   .PopMem(ctx, buf).Load(res).Shl().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Shl);
	}

	private static void Shr(VMContext ctx)
	{
		ctx.AllocManagedMethod("Shr").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(res)
	   .PopMem(ctx, buf).Load(res).Shr().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Shr);
	}

	private static void ShrUn(VMContext ctx)
	{
		ctx.AllocManagedMethod("Shr_Un").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(res)
	   .PopMem(ctx, buf).Load(res).ShrUn().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Shr_Un);
	}

	private static void Ceq(VMContext ctx) => ctx.AllocManagedMethod("Ceq").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(res)
	   .PopMem(ctx, buf).Load(res).Ceq().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Ceq);

	private static void Cgt(VMContext ctx) => ctx.AllocManagedMethod("Cgt").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(res)
	   .PopMem(ctx, buf).AsSigned().Load(res).AsSigned().Cgt().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Cgt);

	private static void Cgt_Un(VMContext ctx) => ctx.AllocManagedMethod("Cgt_Un").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(res)
	   .PopMem(ctx, buf).Load(res).Cgt_Un().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Cgt_Un);

	private static void Clt(VMContext ctx) => ctx.AllocManagedMethod("Clt").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(res)
	   .PopMem(ctx, buf).AsSigned().Load(res).AsSigned().Clt().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Clt);

	private static void Clt_Un(VMContext ctx) => ctx.AllocManagedMethod("Clt_Un").CilMethodBody.Instructions
	   .NewLocal(ctx, out var res)
	   .NewLocal(ctx, out var buf)
	   .PopMem(ctx, buf).Save(res)
	   .PopMem(ctx, buf).Load(res).Clt_Un().Save(res)
	   .PushMem(ctx, res, buf)
	   .RegisterHandler(ctx, VMCodes.Clt_Un);
}
